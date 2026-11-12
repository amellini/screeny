using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Screeny
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public partial class App : Application
	{
		private MainWindow? _window;

		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			InitializeComponent();

			// Senza debugger un'eccezione non gestita chiude l'app senza dettagli: li lasciamo in un file da allegare alle segnalazioni.
			UnhandledException += (_, e) =>
			{
				try
				{
					Directory.CreateDirectory(ProfileStore.Root);
					File.AppendAllText(System.IO.Path.Combine(ProfileStore.Root, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
				}
				catch (IOException)
				{
				}
			};
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
		{
			Session.Init(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
			_window = new MainWindow();
			_window.Activate();

			// Un secondo avvio viene rediretto qui da Program.Main (arriva su un thread in background).
			Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += (_, _) =>
				_window.DispatcherQueue.TryEnqueue(_window.ShowFromTray);
		}
	}
}
