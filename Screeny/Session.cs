using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.UI.Dispatching;

namespace Screeny
{
	/// <summary>
	/// Stato condiviso tra le pagine: catalogo, profili salvati, processi scrcpy in esecuzione e log.
	/// Eventi e modifiche allo stato avvengono sempre sul thread UI.
	/// </summary>
	public static class Session
	{
		private static readonly Dictionary<Profile, Process> _running = [];
		private static readonly StringBuilder _log = new();
		private static DispatcherQueue _dispatcher = null!;

		public static Catalog Catalog { get; private set; } = null!;
		public static ObservableCollection<Profile> Profiles { get; private set; } = null!;
		public static AppSettings Settings { get; private set; } = new();
		public static string ScrcpyPath => Settings.ScrcpyPath;
		public static string LogText => _log.ToString();

		public static event Action<string>? LineLogged;
		public static event Action? ThemeChanged;
		public static event Action? LanguageChanged;

		/// <summary>scrcpy avviato da Screeny (non scatta per le sessioni riprese all'avvio).</summary>
		public static event Action<Profile>? Started;

		/// <summary>scrcpy terminato: profilo, exit code, ultima riga di errore (se c'è).</summary>
		public static event Action<Profile, int, string?>? Exited;

		public static void Init(DispatcherQueue dispatcher)
		{
			_dispatcher = dispatcher;
			Settings = ProfileStore.LoadSettings();
			Loc.Load(Settings.Language);
			Catalog = Catalog.Load(Loc.Language);
			ScrcpyCommand.SelfCheck(Catalog);
			Profiles = new(ProfileStore.LoadAll(message => Log("Screeny", message)));
			AdoptRunning();
		}

		/// <summary>null = lingua di Windows. Ricarica stringhe e catalogo; le pagine si rigenerano su LanguageChanged.</summary>
		public static void SetLanguage(string? language)
		{
			Settings.Language = language;
			SaveSettings();
			Loc.Load(language);
			Catalog = Catalog.Load(Loc.Language);
			LanguageChanged?.Invoke();
		}

		/// <summary>
		/// Le sessioni scrcpy sopravvivono alla chiusura di Screeny: all'avvio quelle con il titolo finestra
		/// di un profilo vengono riprese come se le avessimo avviate noi (stato, Arresta, notifica di uscita).
		/// Il loro stdout/stderr però resta scollegato: non è possibile riagganciarsi alle pipe di un altro processo.
		/// </summary>
		private static void AdoptRunning()
		{
			foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ScrcpyPath)))
			{
				var title = process.MainWindowTitle; // vuoto se scrcpy non ha finestra (es. --no-window): non riconoscibile
				var profile = title.Length == 0 ? null : Profiles.FirstOrDefault(p => !p.IsRunning && ScrcpyCommand.WindowTitle(p) == title);
				if (profile is null)
				{
					process.Dispose();
					continue;
				}
				process.Exited += (_, _) => OnExited(profile, process, () => null);
				try
				{
					process.EnableRaisingEvents = true; // se nel frattempo è già uscito, Exited scatta subito
				}
				catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
				{
					process.Dispose(); // processo non accessibile (es. avviato da un altro utente o elevato)
					continue;
				}
				_running[profile] = process;
				profile.IsRunning = true;
				Log(profile.Name, Loc.T("Log_Adopted", process.Id));
			}
		}

		public static void SetScrcpyPath(string path)
		{
			Settings.ScrcpyPath = path.Trim() is { Length: > 0 } trimmed ? trimmed : "scrcpy";
			SaveSettings();
		}

		public static void SetTheme(Microsoft.UI.Xaml.ElementTheme theme)
		{
			Settings.Theme = theme;
			SaveSettings();
			ThemeChanged?.Invoke();
		}

		/// <summary>Ultimi profili salvati avviati, il più recente per primo.</summary>
		public static IEnumerable<Profile> RecentProfiles(int count) =>
			Settings.RecentProfiles
				.Select(name => Profiles.FirstOrDefault(p => p.Name == name))
				.OfType<Profile>()
				.Take(count);

		/// <summary>Mantiene la lista dei recenti quando un profilo viene rinominato.</summary>
		public static void RenameRecent(string oldName, string newName)
		{
			var index = Settings.RecentProfiles.IndexOf(oldName);
			if (index < 0 || oldName == newName)
				return;
			Settings.RecentProfiles[index] = newName;
			SaveSettings();
		}

		// Ne teniamo qualcuno in più dei 4 mostrati: se un recente viene eliminato, il menu resta pieno.
		private static void MarkUsed(Profile profile)
		{
			if (profile.FilePath is null)
				return;
			var recent = Settings.RecentProfiles;
			recent.Remove(profile.Name);
			recent.Insert(0, profile.Name);
			if (recent.Count > 10)
				recent.RemoveRange(10, recent.Count - 10);
			SaveSettings();
		}

		private static void SaveSettings()
		{
			try
			{
				ProfileStore.SaveSettings(Settings);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				Log("Screeny", Loc.T("Log_SettingsSaveFailed", ex.Message));
			}
		}

		/// <summary>Avvia scrcpy per il profilo con gli argomenti dati (l'editor passa quelli della copia non ancora salvata).</summary>
		public static void Start(Profile profile, IReadOnlyList<string> args)
		{
			if (profile.IsRunning)
				return;

			var info = new ProcessStartInfo(ScrcpyPath)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
			};
			foreach (var arg in args)
				info.ArgumentList.Add(arg);

			string? lastError = null;
			void OnLine(string? line)
			{
				if (line is null)
					return;
				if (line.StartsWith("ERROR", StringComparison.Ordinal))
					lastError = line;
				Log(profile.Name, line);
			}

			var process = new Process { StartInfo = info, EnableRaisingEvents = true };
			process.OutputDataReceived += (_, e) => OnLine(e.Data);
			process.ErrorDataReceived += (_, e) => OnLine(e.Data);
			process.Exited += (_, _) => OnExited(profile, process, () => lastError);

			Log(profile.Name, ScrcpyCommand.Format(ScrcpyPath, args));
			try
			{
				process.Start();
			}
			catch (Win32Exception ex)
			{
				process.Dispose();
				var message = Loc.T("Log_StartFailed", ScrcpyPath, ex.Message);
				Log(profile.Name, message);
				Exited?.Invoke(profile, -1, message);
				return;
			}
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			_running[profile] = process;
			profile.IsRunning = true;
			MarkUsed(profile);
			Started?.Invoke(profile);
		}

		// Chiamato su un thread del pool. lastError va letto dopo WaitForExit, quando stdout/stderr sono svuotati.
		private static void OnExited(Profile profile, Process process, Func<string?> lastErrorLine)
		{
			process.WaitForExit();
			var lastError = lastErrorLine();
			int code;
			try
			{
				code = process.ExitCode;
			}
			catch (InvalidOperationException)
			{
				code = -1; // codice non leggibile (processo ripreso all'avvio senza i diritti necessari)
			}
			process.Dispose();
			_dispatcher.TryEnqueue(() =>
			{
				_running.Remove(profile);
				profile.IsRunning = false;
				Log(profile.Name, Loc.T("Log_Exited", ScrcpyCommand.DescribeExitCode(code)));
				Exited?.Invoke(profile, code, lastError);
			});
		}

		public static void Stop(Profile profile)
		{
			if (!_running.TryGetValue(profile, out var process))
				return;
			// Chiudere la finestra (WM_CLOSE) fa uscire scrcpy pulito, ripristinando lo stato del dispositivo.
			// Kill solo se non c'è una finestra (es. --no-window).
			if (!process.CloseMainWindow())
				process.Kill();
		}

		// ponytail: log in memoria senza limite, da troncare se le sessioni diventano lunghissime
		public static void Log(string source, string message) =>
			_dispatcher.TryEnqueue(() =>
			{
				var line = $"[{DateTime.Now:HH:mm:ss}] [{source}] {message}";
				_log.AppendLine(line);
				LineLogged?.Invoke(line);
			});
	}
}
