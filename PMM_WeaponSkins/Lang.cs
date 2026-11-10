using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>Plugin texts from lang/ru.json, en.json, tr.json. Item names come from WeaponSkins in the server language.</summary>
internal static class Lang
{
	/// <summary>Menu language (config "Language"): ru, en, tr or any code with a lang/&lt;code&gt;.json file.</summary>
	public static string Code { get; private set; } = "ru";

	/// <summary>Latin menu (en, tr): the on-screen keyboard starts in the Latin layout.</summary>
	public static bool En => Code != "ru";

	public static bool Tr => Code == "tr";

	public static void Set(string? language)
	{
		var code = new string((language ?? "").Trim().ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
		Code = code.Length > 0 ? code : "ru";
	}

	private static Dictionary<string, string> _texts = new(StringComparer.Ordinal);
	private static Dictionary<string, string> _english = new(StringComparer.Ordinal);

	/// <summary>Reads lang/&lt;Code&gt;.json and lang/en.json (the fallback) from the plugin folder.</summary>
	public static void Load(string pluginDir, ILogger logger)
	{
		_english = Read(Path.Combine(pluginDir, "lang", "en.json"), logger);
		_texts = Code == "en" ? _english : Read(Path.Combine(pluginDir, "lang", Code + ".json"), logger);
	}

	private static Dictionary<string, string> Read(string file, ILogger logger)
	{
		try
		{
			if (!File.Exists(file))
			{
				logger.LogError("Translation file {File} is missing.", file);
				return new Dictionary<string, string>(StringComparer.Ordinal);
			}
			var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
			var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file), options);
			return data != null ? new Dictionary<string, string>(data, StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal);
		}
		catch (Exception ex)
		{
			logger.LogError("Translation file {File} could not be read: {Error}", file, ex.Message);
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}
	}

	/// <summary>Text in the menu language, else English, else the key itself.</summary>
	public static string T(string key) =>
		_texts.TryGetValue(key, out var text) ? text : _english.TryGetValue(key, out var en) ? en : key;

	public static string F(string key, params object[] args)
	{
		try
		{
			return string.Format(System.Globalization.CultureInfo.InvariantCulture, T(key), args);
		}
		catch (FormatException)
		{
			return T(key);
		}
	}

	/// <summary>Static labels in the layout exist in ru, en and tr; other languages show the English ones.</summary>
	public static bool LayoutEn => Code is not ("ru" or "tr");

	/// <summary>Number with the decimal mark the menu language uses.</summary>
	public static string Num(double value, string format)
	{
		var text = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
		return Code == "en" ? text : text.Replace('.', ','); // ru and tr use a decimal comma
	}
}
