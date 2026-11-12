using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Screeny
{
	/// <summary>
	/// Home: profili salvati come tile, avvio diretto e accesso all'editor.
	/// </summary>
	public sealed partial class HomePage : Page
	{
		public HomePage()
		{
			InitializeComponent();
			Tiles.ItemsSource = Session.Profiles;
		}

		// Session è statica e vive più della pagina: sottoscrizioni solo mentre la pagina è visibile.
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			Session.Profiles.CollectionChanged += Profiles_CollectionChanged;
			Session.LineLogged += Session_LineLogged;
			Session.Exited += Session_Exited;
			LogText.Text = Session.LogText;
			ScrcpyPathBox.Text = Session.ScrcpyPath;
			ThemeChoice.SelectedIndex = (int)Session.Settings.Theme; // ordine dei RadioButton = ElementTheme

			// Voce 0 = lingua di Windows, poi le lingue disponibili (nel proprio nome, come d'uso nei selettori).
			LanguageChoice.Items.Add(Loc.T("Settings_LanguageSystem"));
			foreach (var (_, name) in Loc.Languages)
				LanguageChoice.Items.Add(name);
			var current = Array.FindIndex(Loc.Languages, l => l.Code == Session.Settings.Language);
			LanguageChoice.SelectedIndex = current + 1; // -1 + 1 = 0 se la lingua non è impostata
			UpdateCount();
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			Session.Profiles.CollectionChanged -= Profiles_CollectionChanged;
			Session.LineLogged -= Session_LineLogged;
			Session.Exited -= Session_Exited;
		}

		private void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCount();

		private void UpdateCount()
		{
			var count = Session.Profiles.Count;
			SubtitleText.Text = count switch
			{
				0 => Loc.T("Home_SubtitleEmpty"),
				1 => Loc.T("Home_SubtitleOne"),
				_ => Loc.T("Home_SubtitleMany", count),
			};
			EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}

		private void Session_LineLogged(string line)
		{
			LogText.Text += line + "\n";
			LogScroll.UpdateLayout();
			LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null, true);
		}

		private void Session_Exited(Profile profile, int code, string? lastError)
		{
			if (code == 0)
				return;
			ErrorBar.Title = $"{profile.Name}: {ScrcpyCommand.DescribeExitCode(code)}";
			ErrorBar.Message = lastError ?? "";
			ErrorBar.IsOpen = true;
		}

		private void ShowLog_Click(object sender, RoutedEventArgs e)
		{
			LogExpander.IsExpanded = true;
			ErrorBar.IsOpen = false;
		}

		private void New_Click(object sender, RoutedEventArgs e)
		{
			var n = 1;
			while (Session.Profiles.Any(p => p.Name == Loc.T("Profile_DefaultName", n)))
				n++;
			Edit(new Profile { Name = Loc.T("Profile_DefaultName", n) });
		}

		private void Tile_EditRequested(ProfileTile sender, Profile profile) => Edit(profile);

		private void Edit(Profile profile) =>
			Frame.Navigate(typeof(EditorPage), profile, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });

		private async void Tile_DeleteRequested(ProfileTile sender, Profile profile)
		{
			if (await ConfirmDeleteAsync(XamlRoot, profile))
				DeleteProfile(profile);
		}

		public static async Task<bool> ConfirmDeleteAsync(XamlRoot root, Profile profile)
		{
			var dialog = new ContentDialog
			{
				XamlRoot = root,
				RequestedTheme = DialogTheme(root),
				Title = Loc.T("Delete_Title"),
				Content = Loc.T("Delete_Content", profile.Name, ProfileStore.Folder),
				PrimaryButtonText = Loc.T("Common_Delete"),
				CloseButtonText = Loc.T("Common_Cancel"),
				DefaultButton = ContentDialogButton.Close,
			};
			return await dialog.ShowAsync() == ContentDialogResult.Primary;
		}

		/// <summary>I ContentDialog stanno nel popup root e non ereditano il tema impostato sul contenuto della finestra.</summary>
		public static ElementTheme DialogTheme(XamlRoot root) => ((FrameworkElement)root.Content).ActualTheme;

		public static bool DeleteProfile(Profile profile)
		{
			try
			{
				ProfileStore.Delete(profile);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				Session.Log(profile.Name, Loc.T("Log_DeleteFailed", ex.Message));
				return false;
			}
			Session.Profiles.Remove(profile);
			return true;
		}

		private void SettingsFlyout_Closed(object sender, object e) => Session.SetScrcpyPath(ScrcpyPathBox.Text);

		private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var theme = (ElementTheme)ThemeChoice.SelectedIndex;
			if (ThemeChoice.SelectedIndex >= 0 && theme != Session.Settings.Theme)
				Session.SetTheme(theme);
		}

		private void LanguageChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (LanguageChoice.SelectedIndex < 0)
				return;
			var language = LanguageChoice.SelectedIndex == 0 ? null : Loc.Languages[LanguageChoice.SelectedIndex - 1].Code;
			if (language != Session.Settings.Language)
				Session.SetLanguage(language);
		}

		private void OpenFolder_Click(object sender, RoutedEventArgs e)
		{
			Directory.CreateDirectory(ProfileStore.Folder);
			Process.Start(new ProcessStartInfo(ProfileStore.Folder) { UseShellExecute = true });
		}
	}
}
