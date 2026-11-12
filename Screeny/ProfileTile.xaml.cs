using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Screeny
{
	/// <summary>
	/// Tile di un profilo nella home: nome, opzioni principali, stato; Avvia/Modifica compaiono all'hover o col focus.
	/// </summary>
	public sealed partial class ProfileTile : UserControl
	{
		public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
			nameof(Profile), typeof(Profile), typeof(ProfileTile), new PropertyMetadata(null, (d, _) => ((ProfileTile)d).Attach()));

		private Profile? _attached;
		private bool _pointerOver;

		public ProfileTile()
		{
			InitializeComponent();
			Loaded += (_, _) => Attach();
			Unloaded += (_, _) => Detach();
		}

		public Profile? Profile
		{
			get => (Profile?)GetValue(ProfileProperty);
			set => SetValue(ProfileProperty, value);
		}

		public event TypedEventHandler<ProfileTile, Profile>? EditRequested;
		public event TypedEventHandler<ProfileTile, Profile>? DeleteRequested;

		// Il profilo vive più della tile (riciclata dall'ItemsRepeater): sottoscrizione solo mentre è caricata.
		private void Attach()
		{
			Detach();
			if (!IsLoaded || Profile is null)
				return;
			_attached = Profile;
			_attached.PropertyChanged += Profile_PropertyChanged;
			Update();
		}

		private void Detach()
		{
			if (_attached is not null)
				_attached.PropertyChanged -= Profile_PropertyChanged;
			_attached = null;
		}

		private void Profile_PropertyChanged(object? sender, PropertyChangedEventArgs e) => Update();

		private void Update()
		{
			if (_attached is not { } profile)
				return;

			NameText.Text = profile.Name;
			RunningBadge.Visibility = profile.IsRunning ? Visibility.Visible : Visibility.Collapsed;
			PlayIcon.Glyph = profile.IsRunning ? "" : "";
			PlayText.Text = PlayMenuItem.Text = Loc.T(profile.IsRunning ? "Tile_Stop" : "Tile_Play");

			var errors = ScrcpyCommand.Validate(Session.Catalog, profile);
			PlayButton.IsEnabled = PlayMenuItem.IsEnabled = profile.IsRunning || errors.Count == 0;
			ToolTipService.SetToolTip(PlayButton, errors.Count > 0 ? string.Join("\n", errors) : null);

			var set = Session.Catalog.AllOptions().Where(o => profile.Options.ContainsKey(o.Name)).ToList();
			Footer.Text = set.Count switch
			{
				0 => Loc.T("Tile_Defaults"),
				1 => Loc.T("Tile_OneOption"),
				_ => Loc.T("Tile_ManyOptions", set.Count),
			};

			Chips.Children.Clear();
			foreach (var option in set.Take(3))
			{
				var value = profile.Options[option.Name];
				Chips.Children.Add(Chip(option.Type == OptionType.Flag || value.Length == 0 ? option.Label : $"{option.Label}: {value}"));
			}
			if (set.Count > 3)
				Chips.Children.Add(Chip($"+{set.Count - 3}"));
		}

		private static Border Chip(string text) => new()
		{
			Style = (Style)Application.Current.Resources["ChipStyle"],
			Child = new TextBlock { Text = text, Style = (Style)Application.Current.Resources["ChipTextStyle"] },
		};

		private void Play_Click(object sender, RoutedEventArgs e)
		{
			if (_attached is not { } profile)
				return;
			if (profile.IsRunning)
				Session.Stop(profile);
			else
				Session.Start(profile, ScrcpyCommand.BuildArgs(Session.Catalog, profile));
		}

		private void Edit_Click(object sender, RoutedEventArgs e)
		{
			if (_attached is not null)
				EditRequested?.Invoke(this, _attached);
		}

		private void Delete_Click(object sender, RoutedEventArgs e)
		{
			if (_attached is not null)
				DeleteRequested?.Invoke(this, _attached);
		}

		private void Root_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Edit_Click(sender, e);

		private void Root_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			_pointerOver = true;
			UpdateVisualState();
		}

		private void Root_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			_pointerOver = false;
			UpdateVisualState();
		}

		private void Root_FocusChanged(object sender, RoutedEventArgs e) => UpdateVisualState();

		// Le azioni restano raggiungibili da tastiera: visibili anche quando il focus è dentro la tile.
		private void UpdateVisualState()
		{
			var focusWithin = FocusManager.GetFocusedElement(XamlRoot) is DependencyObject focused && IsAncestorOf(focused);
			VisualStateManager.GoToState(this, _pointerOver || focusWithin ? "PointerOver" : "Normal", true);
		}

		private bool IsAncestorOf(DependencyObject element)
		{
			for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
			{
				if (current == this)
					return true;
			}
			return false;
		}
	}
}
