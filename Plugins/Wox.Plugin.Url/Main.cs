using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;

namespace Wox.Plugin.UrlNew
{
	public class Main : IPlugin, IPluginI18n
	{
		private readonly string[] favTlds = new string[] { "com", "net", "org", "gov", "edu", "co", "io" };

		//based on https://gist.github.com/dperini/729294
		private const string urlPattern = "^" +
			// protocol identifier
			"(?:(?:\\w+)://|)" +
			// user:pass authentication
			"(?:\\S+(?::\\S*)?@)?" +
			"(?:" +
			// IP address exclusion
			// private & local networks
			"(?!(?:10|127)(?:\\.\\d{1,3}){3})" +
			"(?!(?:169\\.254|192\\.168)(?:\\.\\d{1,3}){2})" +
			"(?!172\\.(?:1[6-9]|2\\d|3[0-1])(?:\\.\\d{1,3}){2})" +
			// IP address dotted notation octets
			// excludes loopback network 0.0.0.0
			// excludes reserved space >= 224.0.0.0
			// excludes network & broacast addresses
			// (first & last IP address of each class)
			"(?:[1-9]\\d?|1\\d\\d|2[01]\\d|22[0-3])" +
			"(?:\\.(?:1?\\d{1,2}|2[0-4]\\d|25[0-5])){2}" +
			"(?:\\.(?:[1-9]\\d?|1\\d\\d|2[0-4]\\d|25[0-4]))" +
			"|" +
			// host name
			"(?:(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)" +
			// domain name
			"(?:\\.(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)*" +
			// TLD identifier
			"(?:\\.(?:[a-z\\u00a1-\\uffff]{2,}))" +
			")" +
			// port number
			"(?::\\d{2,5})?" +
			// resource path
			"(?:/\\S*)?" +
			"$";
		Regex reg = new Regex(urlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private PluginInitContext context;

		private Result GetResult(string raw, bool https)
		{
			int score = 2;


			int _int;
			var parsed = new UriBuilder(raw);
			if (raw.Split(':').Length == 2 && raw.Split(':')[0].Contains('.') && int.TryParse(raw.Split(':')[1], out _int))
				parsed = new UriBuilder("http://" + raw);

			Uri uri = parsed.Uri;
			if (string.IsNullOrEmpty(parsed.Scheme))
				parsed.Scheme = "http";

			if (parsed.Scheme == "http" && https)
			{
				parsed.Scheme = "https";
				parsed.Port = 443;
			}
			else if (parsed.Scheme == "https" && !https)
			{
				parsed.Scheme = "http";
				parsed.Port = 80;
			}
			else
				score = 6;

			var toReturn = new Result
			{
				Title = parsed.Uri.ToString(),
				SubTitle = (https ? "Securely open URL: " : "Open URL [insecure]: ") + parsed.Uri.ToString(),
				IcoPath = "Images/url.png",
				Score = score,
				Action = _ =>
				{
					try
					{
						Process.Start(parsed.ToString());
						return true;
					}
					catch (Exception ex)
					{
						context.API.ShowMsg(string.Format(context.API.GetTranslation("wox_plugin_url_canot_open_url"), raw));
						return false;
					}
				}
			};



			if (raw.StartsWith("http"))
				toReturn.Score += 10;

			if (raw == parsed.ToString())
				toReturn.Score += 20;

			if (favTlds.Contains(parsed.Host.Split('.').Last()))
				toReturn.Score += 5;

			return toReturn;
		}

	public List<Result> Query(Query query)
	{
		var raw = query.Search;
		Uri uri = new UriBuilder("example.cool").Uri;
		if (IsURL(raw))
		{
			return new List<Result>
				{
					GetResult(raw, true),
					GetResult(raw, false)
				};
		}
		return new List<Result>(0);
	}

	public bool IsURL(string raw)
	{
		raw = raw.ToLower();

		if (reg.Match(raw).Value == raw) return true;

		if (raw == "localhost" || raw.StartsWith("localhost:") ||
			raw == "http://localhost" || raw.StartsWith("http://localhost:") ||
			raw == "https://localhost" || raw.StartsWith("https://localhost:")
			)
		{
			return true;
		}

		return false;
	}

	public void Init(PluginInitContext context)
	{
		this.context = context;
	}

	public string GetTranslatedPluginTitle()
	{
		return context.API.GetTranslation("wox_plugin_url_plugin_name");
	}

	public string GetTranslatedPluginDescription()
	{
		return context.API.GetTranslation("wox_plugin_url_plugin_description");
	}
}
}