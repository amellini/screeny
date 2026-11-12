using Microsoft.Windows.AppLifecycle;

namespace Screeny
{
	/// <summary>
	/// Main personalizzato (DISABLE_XAML_GENERATED_MAIN) per l'istanza singola: con Screeny nascosto nella tray,
	/// riaprirlo dal menu Start deve mostrare la finestra esistente, non avviare una seconda copia.
	/// </summary>
	public static class Program
	{
		[STAThread]
		private static int Main()
		{
			WinRT.ComWrappersSupport.InitializeComWrappers();

			var main = AppInstance.FindOrRegisterForKey("Screeny");
			if (!main.IsCurrent)
			{
				// Redirect su un thread del pool: attenderlo direttamente sul thread STA può andare in deadlock.
				var args = AppInstance.GetCurrent().GetActivatedEventArgs();
				Task.Run(() => main.RedirectActivationToAsync(args).AsTask()).Wait();
				return 0;
			}

			XamlGeneratedProgram.XamlGeneratedMain();
			return 0;
		}
	}
}
