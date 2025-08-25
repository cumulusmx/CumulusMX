using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using EmbedIO;


namespace CumulusMX
{
	public class DashboardLocalisationManager
	{
		private Dictionary<string, string> _cache = [];

		public async Task LoadLocalization(string lang)
		{
			//if (_cache.TryGetValue(lang, out var cached))
			//	return;

			var langFile = Path.Combine(System.AppContext.BaseDirectory, "locales", "dashboard", $"{lang}.json");
			if (!File.Exists(langFile))
				langFile = Path.Combine(System.AppContext.BaseDirectory, "locales", "dashboard", "en.json"); // fallback

			try
			{
				var json = await File.ReadAllTextAsync(langFile);
				_cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
			}
			catch
			{
				// do nothing
			}
			return;
		}

		public async Task LoadJsonLocalization(string lang, string filename)
		{
			//if (_cache.TryGetValue(lang, out var cached))
			//	return;

			var langFile = Path.Combine(System.AppContext.BaseDirectory, "locales", "dashboard", "json", lang, $"lang-{Path.GetFileNameWithoutExtension(filename)}.json");
			if (!File.Exists(langFile))
			{
				langFile = Path.Combine(System.AppContext.BaseDirectory, "locales", "dashboard", "json", "en", $"lang-{Path.GetFileNameWithoutExtension(filename)}.json"); // fallback
			}

			try
			{
				var json = await File.ReadAllTextAsync(langFile);
				_cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
			}
			catch
			{
				// do nothing
			}
			return;
		}


		public async Task ReplaceTokensToHttpResponseAsyncTokenStreaming(string inputFile, IHttpResponse response)
		{
			if (_cache.Count == 0)
			{
				using var fs = File.OpenRead(inputFile);
				await fs.CopyToAsync(response.OutputStream);

				return;
			}

			// Precompile the regex for speed
			var tokenRegex = new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

			using var reader = new StreamReader(File.OpenRead(inputFile), Encoding.UTF8, false, 8192);

			char[] buffer = new char[4096];
			StringBuilder carryOver = new();

			while (true)
			{
				int readCount = await reader.ReadAsync(buffer, 0, buffer.Length);
				if (readCount == 0) break;

				carryOver.Append(buffer, 0, readCount);

				int lastTokenEnd = carryOver.ToString().LastIndexOf("}}", StringComparison.Ordinal);
				if (lastTokenEnd == -1) continue;

				string processPart = carryOver.ToString(0, lastTokenEnd + 2);
				carryOver.Remove(0, lastTokenEnd + 2);

				string replaced = tokenRegex.Replace(processPart, m =>
				{
					var key = m.Groups[1].Value;
					return _cache.TryGetValue(key, out var value) ? value : m.Value;
				});

				await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(replaced));
				await response.OutputStream.FlushAsync(); // Push to the client progressively
			}

			if (carryOver.Length > 0)
			{
				string replaced = tokenRegex.Replace(carryOver.ToString(), m =>
				{
					var key = m.Groups[1].Value;
					return _cache.TryGetValue(key, out var value) ? value : m.Value;
				});
				await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(replaced));
			}

			await response.OutputStream.FlushAsync();
		}

		public static List<string> GetAvailableLocales()
		{
			var retVal = new List<string>();

			var files = Directory.GetFiles(Path.Combine(System.AppContext.BaseDirectory, "locales", "dashboard"));
			foreach (var file in files)
			{
				retVal.Add(Path.GetFileName(file).Split('.')[0]);
			}

			return retVal;
		}

		public static bool ThisLocaleAvailable(string lang)
		{
			var locales = GetAvailableLocales();

			return locales.Contains(lang);
		}

		public static string GetLocalesAndNames()
		{
			var locales = GetAvailableLocales();

			var list = new Dictionary<string, string>();

			var allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

			foreach (var locale in locales)
			{
				var foundLocale = allCultures.FirstOrDefault(c => c.Name == locale);
				var name = foundLocale is null ? locale : foundLocale.DisplayName;
				list.Add(locale, name);
			}

			return System.Text.Json.JsonSerializer.Serialize(list);
		}
	}
}
