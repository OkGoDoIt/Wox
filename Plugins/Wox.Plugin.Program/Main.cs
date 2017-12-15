﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable
    {
        private static readonly object IndexLock = new object();
        private static Win32[] _win32s;
#if UWP
		private static UWP.Application[] _uwps;
#endif

        private static PluginInitContext _context;

        private static BinaryStorage<Win32[]> _win32Storage;
#if UWP
        private static BinaryStorage<UWP.Application[]> _uwpStorage;
#endif

        private static Settings _settings;
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Normal("|Wox.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
#if UWP
                _uwpStorage = new BinaryStorage<UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new UWP.Application[] { });
	            Log.Info($"|Wox.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");

#endif

			});
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Task.Run(() =>
            {
                Stopwatch.Normal("|Wox.Plugin.Program.Main|Program index cost", IndexPrograms);
            });
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
#if UWP
            _uwpStorage.Save(_uwps);
#endif
		}

		public List<Result> Query(Query query)
        {
            lock (IndexLock)
            {
                var results1 = _win32s.AsParallel().Select(p => p.Result(query.Search, _context.API));
#if UWP
                var results2 = _uwps.AsParallel().Select(p => p.Result(query.Search, _context.API));
				var result = results1.Concat(results2).Where(r => r.Score > 0).ToList();
#else
				var result = results1.Where(r => r.Score > 0).ToList();
#endif

				return result;
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexPrograms()
        {
            Win32[] w = { };
#if UWP
            UWP.Application[] u = { };
#endif
            var t1 = Task.Run(() =>
            {
                w = Win32.All(_settings);
            });
            var t2 = Task.Run(() =>
            {
#if UWP
                var windows10 = new Version(10, 0);
                var support = Environment.OSVersion.Version.Major >= windows10.Major;
                if (support)
                {
                    u = UWP.All();
                }
                else
                {
                    u = new UWP.Application[] { };
                }
#endif
            });
            Task.WaitAll(t1, t2);

            lock (IndexLock)
            {
                _win32s = w;
#if UWP
				_uwps = u;
#endif
			}
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                var menus = program.ContextMenus(_context.API);
                return menus;
            }
            else
            {
                return new List<Result>();
            }
        }

        public static bool StartProcess(ProcessStartInfo info)
        {
            bool hide;
            try
            {
                Process.Start(info);
                hide = true;
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Can't start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
                hide = false;
            }
            return hide;
        }
    }
}