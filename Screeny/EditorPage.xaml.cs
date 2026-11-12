using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization.NumberFormatting;

namespace Screeny
{
	/// <summary>
	/// Editor di un profilo in stile Impostazioni/PowerToys: a sinistra categorie e gruppi del catalogo,
	/// a destra la descrizione del gruppo e una SettingsCard per opzione.
	/// Si lavora su una copia: l'originale cambia solo al salvataggio.
	/// </summary>
	public sealed partial class EditorPage : Page
	{
		private const string SummaryTag = "summary";

		private readonly Dictionary<OptionGroup, NavigationViewItem> _groupItems = [];
		private Profile _original = null!;
		private Profile _draft = null!;
		private bool _deleted;
		private TextBlock? _commandText;

		public EditorPage()
		{
			InitializeComponent();
		}

		public bool IsDirty => !_deleted && !_draft.SameContentAs(_original);

		private static Catalog Catalog => Session.Catalog;

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			_original = (Profile)e.Parameter;
			_draft = _original.Clone();
			_original.PropertyChanged += Original_PropertyChanged;
			Session.Exited += Session_Exited;
			BuildNavigation();
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			_original.PropertyChanged -= Original_PropertyChanged;
			Session.Exited -= Session_Exited;
		}

		private void Original_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Profile.IsRunning))
				Refresh();
		}

		private void Session_Exited(Profile profile, int code, string? lastError)
		{
			if (profile != _original || code == 0)
				return;
			StatusBar.Severity = InfoBarSeverity.Error;
			StatusBar.Title = $"scrcpy: {ScrcpyCommand.DescribeExitCode(code)}";
			StatusBar.Message = lastError ?? Loc.T("Editor_SeeLog");
			StatusBar.IsOpen = true;
		}

		// ── Navigazione ─────────────────────────────────────────────────────

		private void BuildNavigation()
		{
			var summary = new NavigationViewItem { Content = Loc.T("Editor_Summary"), Tag = SummaryTag, Icon = new FontIcon { Glyph = "" } };
			Nav.MenuItems.Add(summary);
			Nav.MenuItems.Add(new NavigationViewItemSeparator());

			foreach (var category in Catalog.Categories)
			{
				var categoryItem = new NavigationViewItem
				{
					Content = category.Title,
					Icon = new FontIcon { Glyph = category.Icon },
					SelectsOnInvoked = false,
					IsExpanded = true,
				};
				foreach (var group in Catalog.Groups.Where(g => category.Groups.Contains(g.Id)))
				{
					var item = new NavigationViewItem { Content = group.Title, Tag = group, Icon = new FontIcon { Glyph = group.Icon } };
					_groupItems[group] = item;
					categoryItem.MenuItems.Add(item);
				}
				Nav.MenuItems.Add(categoryItem);
			}
			Nav.SelectedItem = summary;
		}

		private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
		{
			Body.Children.Clear();
			_commandText = null;
			if ((args.SelectedItem as NavigationViewItem)?.Tag is OptionGroup group)
				ShowGroup(group);
			else
				ShowSummary();
			Refresh();
		}

		/// <summary>Aggiorna titolo, validazione, badge dei gruppi, anteprima comando e stato dei pulsanti.</summary>
		private void Refresh()
		{
			PaneTitle.Text = _draft.Name;
			var errors = ScrcpyCommand.Validate(Catalog, _draft);
			ValidationBar.Message = string.Join("\n", errors);
			ValidationBar.IsOpen = errors.Count > 0;

			foreach (var (group, item) in _groupItems)
			{
				var count = group.Options.Count(o => _draft.Options.ContainsKey(o.Name));
				item.InfoBadge = count > 0 ? new InfoBadge { Value = count } : null;
			}

			if (_commandText is not null)
				_commandText.Text = ScrcpyCommand.Format(Session.ScrcpyPath, ScrcpyCommand.BuildArgs(Catalog, _draft));

			SaveButton.IsEnabled = IsDirty || _original.FilePath is null;
			PlayIcon.Glyph = _original.IsRunning ? "" : "";
			PlayText.Text = Loc.T(_original.IsRunning ? "Tile_Stop" : "Tile_Play");
			PlayButton.IsEnabled = _original.IsRunning || errors.Count == 0;
		}

		// ── Pagine ──────────────────────────────────────────────────────────

		private void ShowSummary()
		{
			PageTitle.Text = Loc.T("Editor_Summary");
			AddDescription(Loc.T("Editor_SummaryDescription"));

			var nameBox = new TextBox { Text = _draft.Name, MinWidth = 280 };
			nameBox.TextChanged += (_, _) =>
			{
				_draft.Name = nameBox.Text;
				Refresh();
			};
			Body.Children.Add(new SettingsCard
			{
				Header = Loc.T("Editor_Name"),
				Description = Loc.T("Editor_NameDescription"),
				HeaderIcon = new FontIcon { Glyph = "" },
				Content = nameBox,
			});

			_commandText = new TextBlock { FontFamily = new FontFamily("Consolas"), IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
			var copy = new Button { Content = Loc.T("Editor_Copy") };
			copy.Click += (_, _) =>
			{
				var package = new DataPackage();
				package.SetText(_commandText.Text);
				Clipboard.SetContent(package);
			};
			Body.Children.Add(new SettingsCard
			{
				Header = Loc.T("Editor_CommandLine"),
				Description = _commandText,
				HeaderIcon = new FontIcon { Glyph = "" },
				Content = copy,
			});

			AddSectionHeader(Loc.T("Editor_SetOptions"));
			var set = Catalog.Groups.SelectMany(g => g.Options.Select(o => (Group: g, Option: o)))
				.Where(x => _draft.Options.ContainsKey(x.Option.Name))
				.ToList();
			if (set.Count == 0)
			{
				Body.Children.Add(new TextBlock
				{
					Text = Loc.T("Editor_NoOptions"),
					Style = Resource<Style>("SecondaryTextStyle"),
				});
			}
			foreach (var (group, option) in set)
			{
				var value = _draft.Options[option.Name];
				var card = new SettingsCard
				{
					Header = option.Label,
					Description = $"{group.Title} · --{option.Name}",
					HeaderIcon = new FontIcon { Glyph = group.Icon },
					Content = new TextBlock
					{
						Text = option.Type == OptionType.Flag ? Loc.T("Editor_FlagOn") : value.Length == 0 ? Loc.T("Editor_NoValue") : value,
						Style = Resource<Style>("SecondaryTextStyle"),
						TextWrapping = TextWrapping.NoWrap,
						MaxWidth = 320,
						TextTrimming = TextTrimming.CharacterEllipsis,
					},
					IsClickEnabled = true,
				};
				card.Click += (_, _) => Nav.SelectedItem = _groupItems[group];
				Body.Children.Add(card);
			}

			if (_original.FilePath is not null)
			{
				AddSectionHeader(Loc.T("Editor_Profile"));
				var delete = new Button { Content = Loc.T("Common_Delete") };
				delete.Click += Delete_Click;
				Body.Children.Add(new SettingsCard
				{
					Header = Loc.T("Editor_DeleteProfile"),
					Description = Loc.T("Editor_DeleteDescription", ProfileStore.Folder),
					HeaderIcon = new FontIcon { Glyph = "" },
					Content = delete,
				});
			}
		}

		private void ShowGroup(OptionGroup group)
		{
			PageTitle.Text = group.Title;
			AddDescription(group.Description);
			foreach (var option in group.Options)
				Body.Children.Add(BuildCard(option));
		}

		private void AddDescription(string text) =>
			Body.Children.Add(new TextBlock
			{
				Text = text,
				Style = Resource<Style>("SecondaryTextStyle"),
				Margin = new Thickness(0, 0, 0, 16),
			});

		private void AddSectionHeader(string text) =>
			Body.Children.Add(new TextBlock
			{
				Text = text,
				Style = Resource<Style>("BodyStrongTextBlockStyle"),
				Margin = new Thickness(1, 24, 0, 6),
			});

		private static T Resource<T>(string key) => (T)Application.Current.Resources[key];

		// ── Card delle opzioni ──────────────────────────────────────────────

		private SettingsCard BuildCard(ScrcpyOption option)
		{
			_draft.Options.TryGetValue(option.Name, out var current);

			var resetButton = new Button
			{
				Content = new FontIcon { Glyph = "", FontSize = 14 },
				Style = Resource<Style>("SubtleIconButtonStyle"),
				Visibility = current is null ? Visibility.Collapsed : Visibility.Visible,
			};
			ToolTipService.SetToolTip(resetButton, Loc.T("Editor_Reset"));

			void Set(string? value)
			{
				if (value is null)
					_draft.Options.Remove(option.Name);
				else
					_draft.Options[option.Name] = value;
				resetButton.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
				Refresh();
			}

			var (control, reset) = BuildControl(option, current, Set);
			resetButton.Click += (_, _) => reset();

			var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
			content.Children.Add(resetButton);
			content.Children.Add(control);

			var description = new StackPanel { Spacing = 2 };
			description.Children.Add(new TextBlock { Text = option.Description, TextWrapping = TextWrapping.Wrap });
			description.Children.Add(new TextBlock
			{
				Text = "--" + option.Name + (option.Short is null ? "" : $"  ·  -{option.Short}"),
				Style = Resource<Style>("FlagTextStyle"),
			});

			return new SettingsCard { Header = option.Label, Description = description, Content = content };
		}

		/// <summary>Crea il controllo per il tipo di opzione; reset lo riporta a "non impostato" (che rimuove l'opzione).</summary>
		private (FrameworkElement Control, Action Reset) BuildControl(ScrcpyOption option, string? current, Action<string?> set)
		{
			var hint = option.Default is not null ? Loc.T("Editor_DefaultHint", option.Default) : option.Placeholder ?? Loc.T("Editor_NotSet");

			switch (option.Type)
			{
				case OptionType.Flag:
				{
					var toggle = new ToggleSwitch { IsOn = current is not null, MinWidth = 0 };
					toggle.Toggled += (_, _) => set(toggle.IsOn ? "true" : null);
					return (toggle, () => toggle.IsOn = false);
				}

				case OptionType.Enum:
				{
					var combo = new ComboBox { PlaceholderText = hint, MinWidth = 220, MaxWidth = 360 };
					foreach (var value in option.Values)
						combo.Items.Add(new ComboBoxItem { Content = value.Description is null ? value.Value : $"{value.Value} — {value.Description}", Tag = value.Value });
					combo.SelectedIndex = option.Values.FindIndex(v => v.Value == current);
					combo.SelectionChanged += (_, _) => set((combo.SelectedItem as ComboBoxItem)?.Tag as string);
					return (combo, () => combo.SelectedIndex = -1);
				}

				case OptionType.MultiEnum:
				{
					var separator = option.Separator ?? ",";
					var selected = current?.Split(separator) ?? [];
					var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
					var boxes = new List<CheckBox>();
					void Update()
					{
						var values = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToList();
						set(values.Count > 0 ? string.Join(separator, values) : null);
					}
					foreach (var value in option.Values)
					{
						var box = new CheckBox { Content = value.Value, Tag = value.Value, IsChecked = selected.Contains(value.Value), MinWidth = 0 };
						ToolTipService.SetToolTip(box, value.Description);
						box.Click += (_, _) => Update();
						boxes.Add(box);
						panel.Children.Add(box);
					}
					return (panel, () =>
					{
						foreach (var box in boxes)
							box.IsChecked = false;
						Update();
					});
				}

				case OptionType.Int:
				case OptionType.Float:
				{
					var isInt = option.Type == OptionType.Int;
					var formatter = new DecimalFormatter { IntegerDigits = 1, FractionDigits = 0, IsGrouped = false };
					if (isInt)
						formatter.NumberRounder = new IncrementNumberRounder { Increment = 1, RoundingAlgorithm = RoundingAlgorithm.RoundHalfAwayFromZero };
					var box = new NumberBox
					{
						PlaceholderText = hint,
						Minimum = option.Min ?? double.MinValue,
						Maximum = option.Max ?? double.MaxValue,
						SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
						NumberFormatter = formatter,
						MinWidth = 220,
						Value = double.TryParse(current, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN,
					};
					box.ValueChanged += (_, e) => set(double.IsNaN(e.NewValue)
						? null
						: (isInt ? Math.Round(e.NewValue) : e.NewValue).ToString(CultureInfo.InvariantCulture));
					return (box, () => box.Value = double.NaN);
				}

				case OptionType.File:
				{
					var box = new TextBox { Text = current ?? "", PlaceholderText = hint, Width = 260 };
					box.TextChanged += (_, _) => set(box.Text.Length == 0 ? null : box.Text);
					var browse = new Button { Content = Loc.T("Editor_Browse") };
					browse.Click += async (_, _) =>
					{
						var picker = new FileSavePicker(XamlRoot.ContentIslandEnvironment.AppWindowId) { SuggestedFileName = Loc.T("Editor_RecordingFileName") };
						picker.FileTypeChoices.Add("Video MP4", [".mp4"]);
						picker.FileTypeChoices.Add("Video MKV", [".mkv"]);
						picker.FileTypeChoices.Add(Loc.T("Editor_FileAudio"), [".m4a", ".mka", ".opus", ".aac", ".flac", ".wav"]);
						if (await picker.PickSaveFileAsync() is { } result)
							box.Text = result.Path;
					};
					var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
					panel.Children.Add(box);
					panel.Children.Add(browse);
					return (panel, () => box.Text = "");
				}

				case OptionType.OptionalValue:
				{
					var toggle = new ToggleSwitch { IsOn = current is not null, MinWidth = 0 };
					var box = new TextBox { Text = current ?? "", PlaceholderText = option.Placeholder ?? Loc.T("Editor_OptionalValue"), Width = 220, IsEnabled = current is not null };
					void Update()
					{
						box.IsEnabled = toggle.IsOn;
						set(toggle.IsOn ? box.Text : null);
					}
					toggle.Toggled += (_, _) => Update();
					box.TextChanged += (_, _) => Update();
					var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
					panel.Children.Add(box);
					panel.Children.Add(toggle);
					return (panel, () =>
					{
						box.Text = "";
						toggle.IsOn = false;
					});
				}

				default: // Text, Bitrate, Color
				{
					// ponytail: Color è un TextBox #RRGGBB, ColorPicker se serve davvero
					var box = new TextBox { Text = current ?? "", PlaceholderText = hint, Width = 260 };
					box.TextChanged += (_, _) => set(box.Text.Length == 0 ? null : box.Text);
					return (box, () => box.Text = "");
				}
			}
		}

		// ── Azioni ──────────────────────────────────────────────────────────

		private void Save_Click(object sender, RoutedEventArgs e) => Save();

		/// <summary>Salva la copia su file e la riporta nell'originale. Ritorna false se il salvataggio non è avvenuto.</summary>
		private bool Save()
		{
			var name = _draft.Name.Trim();
			string? error = null;
			if (name.Length == 0)
				error = Loc.T("Editor_NameRequired");
			else if (Session.Profiles.Any(p => p != _original && string.Equals(ProfileStore.PathFor(p.Name), ProfileStore.PathFor(name), StringComparison.OrdinalIgnoreCase)))
				error = Loc.T("Editor_NameExists", name);

			if (error is null)
			{
				_draft.Name = name;
				try
				{
					ProfileStore.Save(_draft);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					error = Loc.T("Editor_SaveFailed", ex.Message);
				}
			}

			if (error is not null)
			{
				StatusBar.Severity = InfoBarSeverity.Error;
				StatusBar.Title = Loc.T("Editor_NotSaved");
				StatusBar.Message = error;
				StatusBar.IsOpen = true;
				return false;
			}

			Session.RenameRecent(_original.Name, _draft.Name);
			_original.CopyFrom(_draft);
			_original.FilePath = _draft.FilePath;
			if (!Session.Profiles.Contains(_original))
				Session.Profiles.Add(_original);
			StatusBar.IsOpen = false;
			Refresh();
			return true;
		}

		private void Play_Click(object sender, RoutedEventArgs e)
		{
			StatusBar.IsOpen = false;
			if (_original.IsRunning)
				Session.Stop(_original);
			else
				Session.Start(_original, ScrcpyCommand.BuildArgs(Catalog, _draft));
		}

		private async void Delete_Click(object sender, RoutedEventArgs e)
		{
			if (!await HomePage.ConfirmDeleteAsync(XamlRoot, _original) || !HomePage.DeleteProfile(_original))
				return;
			_deleted = true;
			Frame.GoBack(new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
		}

		/// <summary>Chiede cosa fare delle modifiche non salvate. True se si può uscire dalla pagina.</summary>
		public async Task<bool> ConfirmLeaveAsync()
		{
			if (!IsDirty)
				return true;
			var dialog = new ContentDialog
			{
				XamlRoot = XamlRoot,
				RequestedTheme = HomePage.DialogTheme(XamlRoot),
				Title = Loc.T("Unsaved_Title"),
				Content = Loc.T("Unsaved_Content", _draft.Name),
				PrimaryButtonText = Loc.T("Editor_Save"),
				SecondaryButtonText = Loc.T("Unsaved_DontSave"),
				CloseButtonText = Loc.T("Common_Cancel"),
				DefaultButton = ContentDialogButton.Primary,
			};
			return await dialog.ShowAsync() switch
			{
				ContentDialogResult.Primary => Save(),
				ContentDialogResult.Secondary => true,
				_ => false,
			};
		}
	}
}
