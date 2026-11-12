using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml.Markup;

namespace Screeny
{
	/// <summary>
	/// Stringhe dell'interfaccia da Strings\&lt;lingua&gt;.json. Le chiavi mancanti ripiegano sull'inglese, poi sulla chiave stessa.
	/// </summary>
	public static class Loc
	{
		/// <summary>Lingue disponibili: codice e nome nella lingua stessa (per il selettore).</summary>
		public static readonly (string Code, string Name)[] Languages =
		[
			("it", "Italiano"),
			("en", "English"),
			("es", "Español"),
			("fr", "Français"),
		];

		private static Dictionary<string, string> _strings = [];
		private static Dictionary<string, string> _fallback = [];

		public static string Language { get; private set; } = "en";

		/// <summary>preferred vuoto o null = lingua di Windows, se disponibile, altrimenti inglese.</summary>
		public static void Load(string? preferred)
		{
			Language = Resolve(preferred);
			_fallback = Read("en");
			_strings = Language == "en" ? _fallback : Read(Language);
		}

		public static string Resolve(string? preferred)
		{
			var code = string.IsNullOrEmpty(preferred) ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : preferred;
			return Languages.Any(l => l.Code == code) ? code : "en";
		}

		public static string T(string key) =>
			_strings.GetValueOrDefault(key) ?? _fallback.GetValueOrDefault(key) ?? key;

		public static string T(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

		private static Dictionary<string, string> Read(string language)
		{
			var path = Path.Combine(AppContext.BaseDirectory, "Strings", language + ".json");
			return File.Exists(path)
				? JsonSerializer.Deserialize(File.ReadAllText(path), ScreenyJson.Default.DictionaryStringString) ?? []
				: [];
		}
	}

	/// <summary>Stringa localizzata in XAML: Text="{local:L Key=Home_Title}". Valutata al caricamento della pagina.</summary>
	[MarkupExtensionReturnType(ReturnType = typeof(string))]
	public sealed partial class L : MarkupExtension
	{
		public string Key { get; set; } = "";

		protected override object ProvideValue() => Loc.T(Key);
	}
}
