using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screeny
{
	/// <summary>
	/// Profilo di mirroring: contiene solo le opzioni impostate dall'utente (nome opzione → valore).
	/// I flag valgono "true"; un optionalValue senza valore vale "".
	/// </summary>
	public sealed class Profile : INotifyPropertyChanged
	{
		private string _name = "";
		private bool _isRunning;

		public string Name
		{
			get => _name;
			set
			{
				_name = value;
				OnChanged(nameof(Name));
			}
		}

		public Dictionary<string, string> Options { get; set; } = [];

		/// <summary>File da cui il profilo è stato letto o in cui è stato salvato (null = mai salvato).</summary>
		[JsonIgnore]
		public string? FilePath { get; set; }

		[JsonIgnore]
		public bool IsRunning
		{
			get => _isRunning;
			set
			{
				_isRunning = value;
				OnChanged(nameof(IsRunning));
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>Copia di lavoro per l'editor: le modifiche tornano nell'originale solo al salvataggio.</summary>
		public Profile Clone() => new() { Name = Name, Options = new(Options), FilePath = FilePath };

		public bool SameContentAs(Profile other) =>
			Name == other.Name
			&& Options.Count == other.Options.Count
			&& Options.All(o => other.Options.TryGetValue(o.Key, out var v) && v == o.Value);

		public void CopyFrom(Profile source)
		{
			Options = new(source.Options);
			Name = source.Name;
			OnChanged(nameof(Options));
		}

		private void OnChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
	}

	/// <summary>
	/// Profili come file JSON in Documenti\Screeny\Profili, un file per profilo con il nome del profilo.
	/// </summary>
	public static class ProfileStore
	{
		public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Screeny");
		public static readonly string Folder = Path.Combine(Root, "Profili");
		private static readonly string SettingsFile = Path.Combine(Root, "settings.json");

		public static string PathFor(string name)
		{
			var invalid = Path.GetInvalidFileNameChars();
			return Path.Combine(Folder, new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()) + ".json");
		}

		public static List<Profile> LoadAll(Action<string> onError)
		{
			if (!Directory.Exists(Folder))
				return [];

			var profiles = new List<Profile>();
			foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
			{
				try
				{
					var profile = JsonSerializer.Deserialize(File.ReadAllText(file), ScreenyJson.Default.Profile)!;
					if (string.IsNullOrWhiteSpace(profile.Name))
						profile.Name = Path.GetFileNameWithoutExtension(file);
					profile.FilePath = file;
					profiles.Add(profile);
				}
				catch (Exception ex) when (ex is JsonException or IOException)
				{
					onError(Loc.T("Log_ProfileUnreadable", file, ex.Message));
				}
			}
			return [.. profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
		}

		/// <summary>Salva il profilo; se è stato rinominato elimina il file con il vecchio nome.</summary>
		public static void Save(Profile profile)
		{
			Directory.CreateDirectory(Folder);
			var path = PathFor(profile.Name);
			File.WriteAllText(path, JsonSerializer.Serialize(profile, ScreenyJson.Default.Profile));
			if (profile.FilePath is not null && !string.Equals(profile.FilePath, path, StringComparison.OrdinalIgnoreCase))
				File.Delete(profile.FilePath);
			profile.FilePath = path;
		}

		public static void Delete(Profile profile)
		{
			if (profile.FilePath is not null)
				File.Delete(profile.FilePath);
		}

		public static AppSettings LoadSettings()
		{
			try
			{
				return File.Exists(SettingsFile)
					? JsonSerializer.Deserialize(File.ReadAllText(SettingsFile), ScreenyJson.Default.AppSettings) ?? new()
					: new();
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
				return new(); // impostazioni illeggibili: si riparte dai default, verranno riscritte al prossimo cambio
			}
		}

		public static void SaveSettings(AppSettings settings)
		{
			Directory.CreateDirectory(Root);
			File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, ScreenyJson.Default.AppSettings));
		}
	}

	/// <summary>Impostazioni dell'app (Documenti\Screeny\settings.json).</summary>
	public sealed class AppSettings
	{
		public string ScrcpyPath { get; set; } = "scrcpy";

		/// <summary>Default = segue il tema di Windows.</summary>
		public Microsoft.UI.Xaml.ElementTheme Theme { get; set; }

		/// <summary>Codice lingua (it, en, es, fr); null = lingua di Windows.</summary>
		public string? Language { get; set; }

		/// <summary>Nomi dei profili avviati di recente, il più recente per primo (per il menu della tray).</summary>
		public List<string> RecentProfiles { get; set; } = [];
	}

	public static class ScrcpyCommand
	{
		/// <summary>Argomenti nell'ordine del catalogo; se manca --window-title usa il nome del profilo.</summary>
		public static List<string> BuildArgs(Catalog catalog, Profile profile)
		{
			var args = new List<string>();
			foreach (var option in catalog.AllOptions())
			{
				if (!profile.Options.TryGetValue(option.Name, out var value))
					continue;
				var bare = option.Type == OptionType.Flag || (option.Type == OptionType.OptionalValue && value.Length == 0);
				args.Add(bare ? $"--{option.Name}" : $"--{option.Name}={value}");
			}
			if (!profile.Options.ContainsKey("window-title"))
				args.Add($"--window-title={profile.Name}");
			return args;
		}

		/// <summary>Titolo della finestra di scrcpy per il profilo: serve anche a riconoscere le sessioni già aperte.</summary>
		public static string WindowTitle(Profile profile) =>
			profile.Options.TryGetValue("window-title", out var title) ? title : profile.Name;

		/// <summary>Controlla requires/conflicts del catalogo. "opt=valore" è soddisfatto anche da opt non impostata con quel default.</summary>
		public static List<string> Validate(Catalog catalog, Profile profile)
		{
			var errors = new List<string>();
			foreach (var option in catalog.AllOptions())
			{
				if (!profile.Options.ContainsKey(option.Name))
					continue;
				foreach (var requirement in option.Requires)
				{
					var parts = requirement.Split('=', 2);
					var satisfied = parts.Length == 1
						? profile.Options.ContainsKey(parts[0])
						: (profile.Options.TryGetValue(parts[0], out var actual) ? actual : catalog.Find(parts[0])?.Default) == parts[1];
					if (!satisfied)
						errors.Add(Loc.T("Validate_Requires", option.Name, requirement));
				}
				foreach (var conflict in option.Conflicts)
				{
					if (profile.Options.ContainsKey(conflict))
						errors.Add(Loc.T("Validate_Conflict", option.Name, conflict));
				}
			}
			return errors;
		}

		public static string Format(string executable, IEnumerable<string> args) =>
			string.Join(' ', args.Prepend(executable).Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

		public static string DescribeExitCode(int code) => code switch
		{
			0 or 1 or 2 => Loc.T($"Exit_{code}"),
			_ => Loc.T("Exit_Other", code),
		};

		[Conditional("DEBUG")]
		public static void SelfCheck(Catalog catalog)
		{
			var profile = new Profile { Name = "Test 1" };
			profile.Options["fullscreen"] = "true";
			profile.Options["new-display"] = "";
			profile.Options["video-bit-rate"] = "4M";
			string[] expected = ["--video-bit-rate=4M", "--new-display", "--fullscreen", "--window-title=Test 1"];
			Debug.Assert(BuildArgs(catalog, profile).SequenceEqual(expected), "BuildArgs: ordine o formato errato");
			Debug.Assert(Validate(catalog, profile).Count == 0, "Validate: video-source=display dovrebbe valere per default");
			Debug.Assert(catalog.Find("shortcut-mod")?.Type == OptionType.MultiEnum, "Catalog: enum camelCase non letto");

			profile.Options["camera-torch"] = "true";
			profile.Options["no-video"] = "true";
			Debug.Assert(Validate(catalog, profile).Count == 2, "Validate: attesi requires camera + conflitto new-display/no-video");
		}
	}
}
