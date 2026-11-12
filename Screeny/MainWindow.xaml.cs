using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Screeny
{
	/// <summary>
	/// The application window. Hosts the Frame (Home ↔ Editor) and the title bar back button.
	/// Closing the window hides it in the tray; the app really exits only from the tray menu (Esci).
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		private const int RecentInTray = 4;

		private readonly TrayIcon _tray;
		private bool _exiting;

		public MainWindow()
		{
			InitializeComponent();

			ExtendsContentIntoTitleBar = true;
			SetTitleBar(AppTitleBar);

			// Percorso assoluto: nella versione portabile (non MSIX) la cartella corrente può essere un'altra.
			AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
			AppWindow.Closing += AppWindow_Closing;

			_tray = new TrayIcon(WindowNative.GetWindowHandle(this), Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), "Screeny")
			{
				MenuProvider = BuildTrayMenu,
			};
			_tray.Invoked += ShowFromTray;

			ApplyTheme();
			Session.ThemeChanged += ApplyTheme;

			// Dopo Play si va nella tray; se scrcpy esce con errore la finestra torna, per mostrare l'avviso.
			Session.Started += _ => AppWindow.Hide();
			Session.Exited += (_, code, _) =>
			{
				if (code != 0)
					ShowFromTray();
			};

			// Le stringhe sono lette al caricamento delle pagine: al cambio di lingua si riparte da una home nuova.
			// (Il cambio avviene dalle impostazioni della home, quindi non c'è un editor con modifiche da perdere.)
			Session.LanguageChanged += () =>
			{
				RootFrame.Navigate(typeof(HomePage), null, new SuppressNavigationTransitionInfo());
				RootFrame.BackStack.Clear();
				AppTitleBar.IsBackButtonVisible = false;
			};

			RootFrame.Navigate(typeof(HomePage));
		}

		// Il tema va sul contenuto (pagine, Mica), sulla title bar di sistema (pulsanti min/max/chiudi) e sul menu della tray.
		private void ApplyTheme()
		{
			var theme = Session.Settings.Theme;
			((FrameworkElement)Content).RequestedTheme = theme;
			_tray.Theme = theme;
			AppWindow.TitleBar.PreferredTheme = theme switch
			{
				ElementTheme.Light => TitleBarTheme.Light,
				ElementTheme.Dark => TitleBarTheme.Dark,
				_ => TitleBarTheme.UseDefaultAppMode,
			};
		}

		private void RootFrame_Navigated(object sender, NavigationEventArgs e) =>
			AppTitleBar.IsBackButtonVisible = RootFrame.CanGoBack;

		private async void AppTitleBar_BackRequested(TitleBar sender, object args)
		{
			if (RootFrame.Content is EditorPage editor && !await editor.ConfirmLeaveAsync())
				return;
			if (RootFrame.CanGoBack)
				RootFrame.GoBack(new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
		}

		// La X nasconde nella tray: l'editor resta com'è, modifiche non salvate comprese.
		private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
		{
			if (_exiting)
				return;
			args.Cancel = true;
			AppWindow.Hide();
		}

		public void ShowFromTray()
		{
			AppWindow.Show();
			if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
				presenter.Restore();
			Activate();
		}

		private IReadOnlyList<TrayMenuItem> BuildTrayMenu()
		{
			var items = new List<TrayMenuItem> { new(Loc.T("Tray_Open"), ShowFromTray, IsDefault: true), TrayMenuItem.Separator };

			var recent = Session.RecentProfiles(RecentInTray).ToList();
			if (recent.Count == 0)
				items.Add(new(Loc.T("Tray_NoRecent"), Enabled: false));
			foreach (var profile in recent)
			{
				// Grigio se già in esecuzione o se le opzioni non sono valide (scrcpy fallirebbe subito).
				var canStart = !profile.IsRunning && ScrcpyCommand.Validate(Session.Catalog, profile).Count == 0;
				items.Add(new(
					profile.IsRunning ? Loc.T("Tray_Running", profile.Name) : profile.Name,
					() => Session.Start(profile, ScrcpyCommand.BuildArgs(Session.Catalog, profile)),
					canStart));
			}

			items.Add(TrayMenuItem.Separator);
			items.Add(new(Loc.T("Tray_Exit"), () => _ = ExitAsync()));
			return items;
		}

		private async Task ExitAsync()
		{
			if (RootFrame.Content is EditorPage { IsDirty: true } editor)
			{
				ShowFromTray();
				if (!await editor.ConfirmLeaveAsync())
					return;
			}
			_exiting = true;
			_tray.Dispose(); // senza, l'icona resta "fantasma" nella tray finché non ci passa sopra il mouse
			Application.Current.Exit();
		}
	}
}
