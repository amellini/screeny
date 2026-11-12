using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screeny
{
	public enum OptionType
	{
		Flag,
		Enum,
		MultiEnum,
		Int,
		Float,
		Bitrate,
		Text,
		File,
		Color,
		OptionalValue,
	}

	/// <summary>
	/// Catalogo delle opzioni di scrcpy (scrcpy-options.json), da cui viene generato l'editor dei profili.
	/// </summary>
	public sealed class Catalog
	{
		private Dictionary<string, ScrcpyOption>? _byName;

		public string ScrcpyVersion { get; set; } = "";
		public List<GroupCategory> Categories { get; set; } = [];
		public List<OptionGroup> Groups { get; set; } = [];

		public IEnumerable<ScrcpyOption> AllOptions() => Groups.SelectMany(g => g.Options);

		public ScrcpyOption? Find(string name) =>
			(_byName ??= AllOptions().ToDictionary(o => o.Name)).GetValueOrDefault(name);

		/// <summary>
		/// Il catalogo è scritto in italiano; per le altre lingue i testi vengono sostituiti da
		/// i18n\scrcpy-options.&lt;lingua&gt;.json (quelli mancanti restano in italiano).
		/// </summary>
		public static Catalog Load(string language)
		{
			var catalog = JsonSerializer.Deserialize(
				File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scrcpy-options.json")),
				ScreenyJson.Default.Catalog)!;
			var overlay = Path.Combine(AppContext.BaseDirectory, "i18n", $"scrcpy-options.{language}.json");
			if (language != "it" && File.Exists(overlay))
				catalog.Apply(JsonSerializer.Deserialize(File.ReadAllText(overlay), ScreenyJson.Default.CatalogTranslation)!);
			return catalog;
		}

		private void Apply(CatalogTranslation translation)
		{
			foreach (var category in Categories)
				category.Title = translation.Categories.GetValueOrDefault(category.Id) ?? category.Title;
			foreach (var group in Groups)
			{
				if (translation.Groups.TryGetValue(group.Id, out var g))
				{
					group.Title = g.Title ?? group.Title;
					group.Description = g.Description ?? group.Description;
				}
				foreach (var option in group.Options)
				{
					if (!translation.Options.TryGetValue(option.Name, out var o))
						continue;
					option.Label = o.Label ?? option.Label;
					option.Description = o.Description ?? option.Description;
					option.Placeholder = o.Placeholder ?? option.Placeholder;
					foreach (var value in option.Values)
						value.Description = o.Values.GetValueOrDefault(value.Value) ?? value.Description;
				}
			}
		}
	}

	/// <summary>Testi tradotti del catalogo, per id di categoria/gruppo e nome di opzione.</summary>
	public sealed class CatalogTranslation
	{
		public Dictionary<string, string> Categories { get; set; } = [];
		public Dictionary<string, GroupTranslation> Groups { get; set; } = [];
		public Dictionary<string, OptionTranslation> Options { get; set; } = [];
	}

	public sealed class GroupTranslation
	{
		public string? Title { get; set; }
		public string? Description { get; set; }
	}

	public sealed class OptionTranslation
	{
		public string? Label { get; set; }
		public string? Description { get; set; }
		public string? Placeholder { get; set; }

		/// <summary>Descrizioni dei valori, per valore.</summary>
		public Dictionary<string, string> Values { get; set; } = [];
	}

	/// <summary>Categoria della barra di navigazione dell'editor: raccoglie più gruppi (per id).</summary>
	public sealed class GroupCategory
	{
		public string Id { get; set; } = "";
		public string Title { get; set; } = "";
		public string Icon { get; set; } = "";
		public List<string> Groups { get; set; } = [];
	}

	public sealed class OptionGroup
	{
		public string Id { get; set; } = "";
		public string Title { get; set; } = "";
		public string Icon { get; set; } = "";
		public string Description { get; set; } = "";
		public List<ScrcpyOption> Options { get; set; } = [];
	}

	public sealed class ScrcpyOption
	{
		public string Name { get; set; } = "";
		public string? Short { get; set; }
		public OptionType Type { get; set; }
		public string Label { get; set; } = "";
		public string Description { get; set; } = "";
		public string? Default { get; set; }
		public double? Min { get; set; }
		public double? Max { get; set; }
		public string? Placeholder { get; set; }
		public string? Separator { get; set; }
		public string? ListCommand { get; set; }
		public List<OptionValue> Values { get; set; } = [];
		public List<string> Requires { get; set; } = [];
		public List<string> Conflicts { get; set; } = [];
	}

	public sealed class OptionValue
	{
		public string Value { get; set; } = "";
		public string? Description { get; set; }
	}

	// Source generation: il Release ha PublishTrimmed, la serializzazione via reflection non sopravvive al trimming.
	[JsonSourceGenerationOptions(
		PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
		UseStringEnumConverter = true,
		WriteIndented = true,
		IndentCharacter = '\t',
		IndentSize = 1)]
	[JsonSerializable(typeof(Catalog))]
	[JsonSerializable(typeof(Profile))]
	[JsonSerializable(typeof(AppSettings))]
	[JsonSerializable(typeof(CatalogTranslation))]
	[JsonSerializable(typeof(Dictionary<string, string>))]
	internal partial class ScreenyJson : JsonSerializerContext
	{
	}
}
