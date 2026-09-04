using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace SensitivityRandomizer
{
    public sealed partial class MainWindow : Window
    {
        private const int WmHotKey = 0x0312;
        private const int HotKeyToggle = 0x5101;
        private const int HotKeyPanic = 0x5102;
        private const int HotKeyPause = 0x5103;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkF8 = 0x77;
        private const uint VkF9 = 0x78;
        private const uint VkF10 = 0x79;

        private static SolidColorBrush BackgroundBrush { get { return UiPalette.Current.Background; } }
        private static SolidColorBrush SurfaceBrush { get { return UiPalette.Current.Surface; } }
        private static SolidColorBrush SurfaceRaisedBrush { get { return UiPalette.Current.SurfaceRaised; } }
        private static SolidColorBrush InputBrush { get { return UiPalette.Current.Input; } }
        private static SolidColorBrush OutlineBrush { get { return UiPalette.Current.Outline; } }
        private static SolidColorBrush BorderSoftBrush { get { return UiPalette.Current.BorderSoft; } }
        private static SolidColorBrush TextPrimaryBrush { get { return UiPalette.Current.TextPrimary; } }
        private static SolidColorBrush TextSecondaryBrush { get { return UiPalette.Current.TextSecondary; } }
        private static SolidColorBrush AccentBrush { get { return UiPalette.Current.Accent; } }
        private static SolidColorBrush PurpleBrush { get { return UiPalette.Current.Purple; } }
        private static SolidColorBrush BlueBrush { get { return UiPalette.Current.Blue; } }
        private static SolidColorBrush DangerBrush { get { return UiPalette.Current.Danger; } }
        private static SolidColorBrush WarningBrush { get { return UiPalette.Current.Warning; } }

        private readonly RawAccelController _controller;
        private readonly RandomizerEngine _engine;
        private readonly string _settingsPath;
        private readonly string _loadWarning;
        private readonly string _guardArmPath;
        private readonly bool _guardAvailable;
        private readonly DispatcherTimer _uiTimer;
        private readonly List<Control> _editableControls = new List<Control>();
        private readonly List<NumericField> _numericControls = new List<NumericField>();

        private AppSettings _settings;
        private TextBlock _statusLabel;
        private TextBlock _statusDetailLabel;
        private TextBlock _multiplierLabel;
        private TextBlock _effectiveSensitivityLabel;
        private TextBlock _effectiveEdpiLabel;
        private TextBlock _driverLabel;
        private TextBlock _timerLabel;
        private TextBlock _nextChangeLabel;
        private TextBlock _changeCountLabel;
        private TextBlock _hotkeyLabel;
        private TextBlock _modeDescriptionLabel;
        private TextBlock _validationLabel;
        private TextBlock _minimumIntervalTitle;
        private WpfMultiplierChart _chart;

        private ComboBox _presetBox;
        private ComboBox _modeBox;
        private ComboBox _intervalModeBox;
        private ComboBox _languageBox;
        private ComboBox _themeBox;
        private NumericField _dpiBox;
        private NumericField _baseSensitivityBox;
        private NumericField _minimumMultiplierBox;
        private NumericField _maximumMultiplierBox;
        private NumericField _maximumStepBox;
        private NumericField _returnStrengthBox;
        private NumericField _gaussianMeanBox;
        private NumericField _gaussianSigmaBox;
        private NumericField _minimumIntervalBox;
        private NumericField _maximumIntervalBox;
        private NumericField _durationMinutesBox;
        private NumericField _recalibrationMinutesBox;
        private NumericField _seedBox;
        private ToggleSwitch _reciprocalBoundsBox;
        private ToggleSwitch _useSeedBox;
        private ToggleSwitch _loggingBox;

        private Button _startButton;
        private Button _stopButton;
        private Button _pauseButton;
        private Button _resetButton;
        private Button _verifyButton;
        private Button _openLogsButton;
        private Button _importButton;
        private Button _exportButton;
        private Button _essentialTabButton;
        private Button _optionalTabButton;
        private Button _guideTabButton;

        private FrameworkElement _essentialPage;
        private FrameworkElement _optionalPage;
        private FrameworkElement _guidePage;
        private FrameworkElement _randomWalkStepRow;
        private FrameworkElement _randomWalkReturnRow;
        private FrameworkElement _gaussianMeanRow;
        private FrameworkElement _gaussianSigmaRow;
        private FrameworkElement _reciprocalBoundsRow;
        private FrameworkElement _modeNote;
        private FrameworkElement _maximumIntervalRow;
        private FrameworkElement _seedValueRow;
        private FrameworkElement _recalibrationRow;

        private MultiplierReadback _lastReadback;
        private HwndSource _windowSource;
        private IntPtr _windowHandle;
        private bool _updatingControls;
        private bool _syncingReciprocalBounds;
        private bool _rawAccelReady;
        private bool _startupInspectionStarted;
        private bool _shutdownStarted;
        private bool _allowClose;
        private bool _safetyResetRequired;
        private bool _settingsValid;
        private bool _rebuildingInterface;
        private bool _disposed;
        private int _selectedPage;
        private double _displayedMultiplier = 1.0;

        public MainWindow(
            RawAccelController controller,
            RandomizerEngine engine,
            AppSettings settings,
            string settingsPath,
            string loadWarning,
            string guardArmPath,
            bool guardAvailable)
        {
            _controller = controller;
            _engine = engine;
            _settings = settings;
            _settingsPath = settingsPath;
            _loadWarning = loadWarning;
            _guardArmPath = guardArmPath;
            _guardAvailable = guardAvailable;

            UiText.SetLanguage(settings.Language);
            UiPalette.Select(settings.Theme);
            Title = "Sensitivity Randomizer 1.2.0 RC2";
            Width = 1120;
            Height = 760;
            MinWidth = 900;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BackgroundBrush;
            Foreground = TextPrimaryBrush;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            BuildInterface();
            ApplySettingsToControls(_settings);
            UpdateEffectiveValues();
            UpdateControlAvailability();

            _engine.StatusChanged += EngineOnStatusChanged;
            _engine.SampleApplied += EngineOnSampleApplied;
            _engine.SessionCompleted += EngineOnSessionCompleted;

            _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _uiTimer.Tick += (sender, args) =>
            {
                UpdateSessionClock();
                var state = _engine.State;
                if (state == EngineState.Running || state == EngineState.Paused)
                    _chart?.InvalidateVisual();
            };
            _uiTimer.Start();

            Loaded += MainWindowOnLoaded;
            Closing += MainWindowOnClosing;
            Closed += (sender, args) => _disposed = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            _windowSource = HwndSource.FromHwnd(_windowHandle);
            _windowSource?.AddHook(WindowMessageHook);
            RegisterGlobalHotKeys();
            Program.LogStartup("WPF window source initialized. Hotkeys registered.");
        }

        private async void MainWindowOnLoaded(object sender, RoutedEventArgs args)
        {
            if (_startupInspectionStarted) return;
            _startupInspectionStarted = true;
            Activate();

            if (!string.IsNullOrWhiteSpace(_loadWarning))
                MessageBox.Show(this, _loadWarning, UiText.T("Настройки", "Settings"), MessageBoxButton.OK, MessageBoxImage.Warning);

            await InspectRawAccelAtStartupAsync();
            Program.LogStartup("Raw Accel startup inspection finished in WPF UI.");
        }

        private void MainWindowOnClosing(object sender, CancelEventArgs args)
        {
            if (_allowClose)
            {
                UnregisterGlobalHotKeys();
                _windowSource?.RemoveHook(WindowMessageHook);
                _uiTimer.Stop();
                return;
            }

            args.Cancel = true;
            if (_shutdownStarted) return;
            _shutdownStarted = true;
            BeginShutdownAsync();
        }

        private void BuildInterface()
        {
            _editableControls.Clear();
            _numericControls.Clear();

            var shell = new Grid
            {
                Background = new LinearGradientBrush(
                    UiPalette.Current.BackgroundColor,
                    UiPalette.Current.BackgroundAltColor,
                    90),
                Margin = new Thickness(0),
                Opacity = 0
            };
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });
            shell.Margin = new Thickness(18, 10, 18, 10);

            AddToGrid(shell, BuildHeader(), 0, 0);
            AddToGrid(shell, BuildLiveStrip(), 1, 0);
            AddToGrid(shell, BuildWorkspace(), 2, 0);
            AddToGrid(shell, BuildCommandDock(), 3, 0);
            Content = shell;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                shell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }));
        }

        private UIElement BuildHeader()
        {
            var header = new Grid { Background = Brushes.Transparent };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var identity = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var mark = new LogoMark { Width = 36, Height = 36 };
            var name = new StackPanel { Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock
            {
                Text = "SENSITIVITY RANDOMIZER",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimaryBrush
            });
            var subtitle = new TextBlock
            {
                FontSize = 11,
                Foreground = TextSecondaryBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };
            subtitle.Inlines.Add(new Run(UiText.T("constant gain через Raw Accel", "constant gain via Raw Accel")));
            subtitle.Inlines.Add(new Run("  ·  v1.2 RC2  ·  " + UiText.T("Автор", "Author") + ": "));
            var authorLink = new Hyperlink(new Run("dinati.ru"))
            {
                NavigateUri = new Uri("https://dinati.ru/"),
                Foreground = AccentBrush,
                TextDecorations = null,
                ToolTip = UiText.T("Открыть сайт автора", "Open the author's website")
            };
            authorLink.RequestNavigate += AuthorLinkOnRequestNavigate;
            subtitle.Inlines.Add(authorLink);
            name.Children.Add(subtitle);
            identity.Children.Add(mark);
            identity.Children.Add(name);

            var tools = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _importButton = CreateButton(UiText.T("Импорт", "Import"), ButtonTone.Ghost, 82, 34);
            _exportButton = CreateButton(UiText.T("Экспорт", "Export"), ButtonTone.Ghost, 82, 34);
            _importButton.Margin = new Thickness(0, 0, 7, 0);
            _exportButton.Margin = new Thickness(0, 0, 7, 0);
            _importButton.ToolTip = UiText.T("Импорт проверенной JSON-конфигурации", "Import a validated JSON configuration");
            _exportButton.ToolTip = UiText.T("Экспорт текущих настроек в JSON", "Export current settings to JSON");
            _importButton.Click += (sender, args) => ImportConfiguration();
            _exportButton.Click += (sender, args) => ExportConfiguration();

            _themeBox = CreateCombo(UiText.ThemeDisplayNames(), 108);
            _themeBox.SelectedIndex = UiText.IndexOfTheme(_settings.Theme);
            _themeBox.ToolTip = UiText.T("Тема интерфейса", "Interface theme");
            _themeBox.Margin = new Thickness(0, 0, 7, 0);
            _themeBox.SelectionChanged += ThemeBoxOnSelectionChanged;

            _languageBox = CreateCombo(UiText.LanguageDisplayNames(), 118);
            _languageBox.SelectedIndex = UiText.IndexOfLanguage(UiText.Current);
            _languageBox.ToolTip = UiText.T("Язык интерфейса", "Interface language");
            _languageBox.SelectionChanged += LanguageBoxOnSelectionChanged;
            tools.Children.Add(_importButton);
            tools.Children.Add(_exportButton);
            tools.Children.Add(_themeBox);
            tools.Children.Add(_languageBox);

            AddToGrid(header, identity, 0, 0);
            AddToGrid(header, tools, 0, 1);
            return header;
        }

        private void AuthorLinkOnRequestNavigate(object sender, RequestNavigateEventArgs args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Program.LogStartup("Could not open author website: " + ex);
                MessageBox.Show(this,
                    UiText.T("Не удалось открыть сайт автора в браузере.", "Could not open the author's website in a browser."),
                    UiText.T("Ошибка открытия ссылки", "Could not open link"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            args.Handled = true;
        }

        private UIElement BuildLiveStrip()
        {
            var card = CreateCard(new Thickness(18, 12, 18, 12), 15);
            card.Margin = new Thickness(0, 4, 0, 10);
            card.Background = new LinearGradientBrush(
                UiPalette.Current.LiveStartColor,
                UiPalette.Current.LiveEndColor,
                15);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31, GridUnitType.Star) });

            var status = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 14, 0) };
            status.Children.Add(CreateCaption(UiText.T("СОСТОЯНИЕ", "STATUS"), AccentBrush));
            _statusLabel = new TextBlock
            {
                Text = UiText.T("ВЫКЛ", "OFF"),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextSecondaryBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _statusDetailLabel = new TextBlock
            {
                Text = UiText.T("Raw Accel ещё не проверен.", "Raw Accel has not been checked yet."),
                FontSize = 11,
                Foreground = TextSecondaryBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            status.Children.Add(_statusLabel);
            status.Children.Add(_statusDetailLabel);

            var multiplier = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
            multiplier.Children.Add(new TextBlock
            {
                Text = UiText.T("ТЕКУЩИЙ МНОЖИТЕЛЬ", "CURRENT MULTIPLIER"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = PurpleBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _multiplierLabel = new TextBlock
            {
                Text = "1.000",
                FontSize = 34,
                FontWeight = FontWeights.SemiBold,
                Foreground = AccentBrush,
                LineHeight = 38,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            multiplier.Children.Add(_multiplierLabel);

            var effective = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 4, 0) };
            effective.Children.Add(CreateCaption(UiText.T("ЭФФЕКТИВНАЯ ЧУВСТВИТЕЛЬНОСТЬ", "EFFECTIVE SENSITIVITY"), BlueBrush));
            _effectiveSensitivityLabel = new TextBlock
            {
                Text = "sens 1.600",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimaryBrush,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _effectiveEdpiLabel = new TextBlock
            {
                Text = "800.0 eDPI",
                FontSize = 12,
                Foreground = BlueBrush,
                Margin = new Thickness(0, 1, 0, 0)
            };
            effective.Children.Add(_effectiveSensitivityLabel);
            effective.Children.Add(_effectiveEdpiLabel);

            AddToGrid(grid, status, 0, 0);
            AddToGrid(grid, CreateDivider(), 0, 1);
            AddToGrid(grid, multiplier, 0, 2);
            AddToGrid(grid, CreateDivider(), 0, 3);
            AddToGrid(grid, effective, 0, 4);
            card.Child = grid;
            return card;
        }

        private UIElement BuildWorkspace()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(57, GridUnitType.Star), MinWidth = 450 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(43, GridUnitType.Star), MinWidth = 320 });
            AddToGrid(grid, BuildSettingsPanel(), 0, 0);
            AddToGrid(grid, BuildMonitorPanel(), 0, 2);
            return grid;
        }

        private UIElement BuildSettingsPanel()
        {
            var card = CreateCard(new Thickness(10), 15);
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var navigation = new Border
            {
                Background = InputBrush,
                BorderBrush = BorderSoftBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var navigationButtons = new StackPanel { Orientation = Orientation.Horizontal };
            _essentialTabButton = CreateButton(UiText.T("Основное", "Essential"), ButtonTone.Tab, 116, 34);
            _optionalTabButton = CreateButton(UiText.T("Дополнительно", "Optional"), ButtonTone.Tab, 124, 34);
            _guideTabButton = CreateButton(UiText.T("Справка", "Guide"), ButtonTone.Tab, 100, 34);
            _essentialTabButton.Click += (sender, args) => SelectPage(0);
            _optionalTabButton.Click += (sender, args) => SelectPage(1);
            _guideTabButton.Click += (sender, args) => SelectPage(2);
            navigationButtons.Children.Add(_essentialTabButton);
            navigationButtons.Children.Add(_optionalTabButton);
            navigationButtons.Children.Add(_guideTabButton);
            navigation.Child = navigationButtons;

            var pageHost = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            _essentialPage = WrapPage(BuildEssentialPage());
            _optionalPage = WrapPage(BuildOptionalPage());
            _guidePage = WrapPage(BuildGuidePage());
            pageHost.Children.Add(_essentialPage);
            pageHost.Children.Add(_optionalPage);
            pageHost.Children.Add(_guidePage);
            AddToGrid(layout, navigation, 0, 0);
            AddToGrid(layout, pageHost, 1, 0);
            card.Child = layout;
            SelectPage(_selectedPage);
            return card;
        }

        private StackPanel BuildEssentialPage()
        {
            var page = new StackPanel();
            page.Children.Add(CreateCallout(
                UiText.T("ОБЯЗАТЕЛЬНО", "REQUIRED"),
                UiText.T("Здесь только параметры, влияющие на multiplier и момент его смены.",
                    "Only settings that affect multiplier values and change timing are shown here."),
                AccentBrush,
                FrozenBrush(UiPalette.Current.RequiredBackgroundColor)));

            _presetBox = CreateCombo(TrainingPreset.All.Select(item => item.Name)
                .Concat(new[] { UiText.T("Пользовательский", "Custom") }), 174);
            _modeBox = CreateCombo(new[]
            {
                "Balanced Coverage",
                "Smooth Random Walk",
                UiText.T("Центрированный Gaussian", "Centered Gaussian"),
                UiText.T("Линейный Uniform", "Linear Uniform")
            }, 174);
            _minimumMultiplierBox = CreateNumeric(0.10m, 10.00m, 0.50m, 5, 0.01m, 82);
            _maximumMultiplierBox = CreateNumeric(0.10m, 10.00m, 2.00m, 5, 0.01m, 82);
            _reciprocalBoundsBox = CreateToggle(UiText.T("Связать min/max", "Link min/max"), 174);
            _maximumStepBox = CreateNumeric(0.001m, 9.90m, 0.25m, 3, 0.005m, 174);
            _returnStrengthBox = CreateNumeric(0m, 1m, 0.03m, 3, 0.01m, 174);
            _gaussianMeanBox = CreateNumeric(0.10m, 10.00m, 1.00m, 3, 0.01m, 174);
            _gaussianSigmaBox = CreateNumeric(0.001m, 9.90m, 0.30m, 3, 0.005m, 174);

            var range = new Grid { Width = 174 };
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddToGrid(range, _minimumMultiplierBox, 0, 0);
            AddToGrid(range, _maximumMultiplierBox, 0, 2);

            var randomRows = new List<FrameworkElement>
            {
                CreateSettingRow(UiText.T("Готовый профиль", "Preset"),
                    UiText.T("Стартовый набор. Любую настройку можно изменить.", "A starting set. Every setting remains editable."),
                    _presetBox),
                CreateSettingRow(UiText.T("Режим", "Mode"),
                    UiText.T("Скорость движения мыши не используется ни в одном режиме.", "Mouse movement speed is never used by any mode."),
                    _modeBox),
                CreateSettingRow(UiText.T("Диапазон multiplier", "Multiplier range"),
                    UiText.T("Постоянные значения от 0.10000 до 10.00000.", "Constant values from 0.10000 to 10.00000."),
                    range)
            };
            _reciprocalBoundsRow = CreateSettingRow(
                UiText.T("Симметрия вокруг 1.000", "Symmetry around 1.000"),
                UiText.T("Поддерживает min × max = 1 и меняет вторую границу автоматически.",
                    "Maintains min × max = 1 and updates the opposite bound automatically."),
                _reciprocalBoundsBox);
            _randomWalkStepRow = CreateSettingRow(UiText.T("Максимальный шаг", "Maximum step"),
                UiText.T("Наибольшее изменение за один переход.", "Largest change in one transition."), _maximumStepBox);
            _randomWalkReturnRow = CreateSettingRow(UiText.T("Возврат к 1.000", "Return toward 1.000"),
                UiText.T("Сила притяжения Random Walk к базовой sensitivity.", "Pull of Random Walk toward base sensitivity."), _returnStrengthBox);
            _gaussianMeanRow = CreateSettingRow(UiText.T("Среднее значение", "Mean"),
                UiText.T("Центр Gaussian-распределения.", "Center of the Gaussian distribution."), _gaussianMeanBox);
            _gaussianSigmaRow = CreateSettingRow("Sigma",
                UiText.T("Разброс значений вокруг центра.", "Spread of values around the center."), _gaussianSigmaBox);
            randomRows.Add(_reciprocalBoundsRow);
            randomRows.Add(_randomWalkStepRow);
            randomRows.Add(_randomWalkReturnRow);
            randomRows.Add(_gaussianMeanRow);
            randomRows.Add(_gaussianSigmaRow);

            _modeDescriptionLabel = new TextBlock
            {
                Foreground = BlueBrush,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            _modeNote = CreateInlineNote(_modeDescriptionLabel, FrozenBrush(UiPalette.Current.ModeNoteBackgroundColor), BlueBrush);
            randomRows.Add(_modeNote);
            page.Children.Add(CreateSection(UiText.T("Рандомизация", "Randomization"),
                UiText.T("Constant gain между запланированными сменами", "Constant gain between scheduled changes"), randomRows));

            _intervalModeBox = CreateCombo(new[]
            {
                UiText.T("Случайный диапазон", "Random range"),
                UiText.T("Фиксированный", "Fixed")
            }, 174);
            _minimumIntervalBox = CreateNumeric(1.25m, 3600m, 2m, 2, 0.25m, 174);
            _maximumIntervalBox = CreateNumeric(1.25m, 3600m, 5m, 2, 0.25m, 174);
            _minimumIntervalTitle = new TextBlock();
            var minimumIntervalRow = CreateSettingRow(
                UiText.T("Минимальный интервал", "Minimum interval"),
                UiText.T("Секунды. Технический минимум Raw Accel равен 1.25.", "Seconds. Raw Accel's technical minimum is 1.25."),
                _minimumIntervalBox,
                _minimumIntervalTitle);
            _maximumIntervalRow = CreateSettingRow(UiText.T("Максимальный интервал", "Maximum interval"),
                UiText.T("Верхняя граница случайной паузы.", "Upper bound of the randomized delay."), _maximumIntervalBox);
            page.Children.Add(CreateSection(UiText.T("Частота смены", "Change timing"),
                UiText.T("Когда применяется следующий multiplier", "When the next multiplier is applied"),
                new[]
                {
                    CreateSettingRow(UiText.T("Режим интервала", "Interval mode"),
                        UiText.T("Фиксированная или случайная пауза.", "Fixed or randomized delay."), _intervalModeBox),
                    minimumIntervalRow,
                    _maximumIntervalRow
                }));

            _validationLabel = new TextBlock
            {
                Foreground = TextSecondaryBrush,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            page.Children.Add(CreateInlineNote(_validationLabel, SurfaceRaisedBrush, TextSecondaryBrush));

            _editableControls.AddRange(new Control[]
            {
                _presetBox, _modeBox, _minimumMultiplierBox, _maximumMultiplierBox, _reciprocalBoundsBox,
                _maximumStepBox, _returnStrengthBox, _gaussianMeanBox, _gaussianSigmaBox,
                _intervalModeBox, _minimumIntervalBox, _maximumIntervalBox
            });
            WireEssentialEvents();
            return page;
        }

        private StackPanel BuildOptionalPage()
        {
            var page = new StackPanel();
            page.Children.Add(CreateCallout(
                UiText.T("НЕОБЯЗАТЕЛЬНО", "OPTIONAL"),
                UiText.T("Расчёты, длительность, повторяемость и локальные логи.",
                    "Display calculations, duration, repeatability and local logs."),
                BlueBrush,
                FrozenBrush(UiPalette.Current.OptionalBackgroundColor)));

            _dpiBox = CreateNumeric(50m, 32000m, 500m, 0, 50m, 174);
            _baseSensitivityBox = CreateNumeric(0.001m, 100m, 1.6m, 3, 0.05m, 174);
            page.Children.Add(CreateSection(UiText.T("Расчёт eDPI", "eDPI display"),
                UiText.T("Не изменяет DPI мыши или базовую sensitivity", "Does not change mouse DPI or base sensitivity"),
                new[]
                {
                    CreateSettingRow("Mouse DPI", UiText.T("Используется только в расчётах интерфейса.", "Used only for UI calculations."), _dpiBox),
                    CreateSettingRow(UiText.T("Базовая sensitivity", "Base sensitivity"), UiText.T("Нужна только для effective sensitivity и eDPI.", "Base sensitivity for effective sensitivity and eDPI."), _baseSensitivityBox)
                }));

            _durationMinutesBox = CreateNumeric(0m, 1440m, 20m, 0, 5m, 174);
            _recalibrationMinutesBox = CreateNumeric(0m, 1440m, 10m, 0, 5m, 174);
            _useSeedBox = CreateToggle(UiText.T("Включить", "Enable"), 174);
            _seedBox = CreateNumeric(int.MinValue, int.MaxValue, 12345m, 0, 1m, 174);
            _loggingBox = CreateToggle(UiText.T("CSV + JSON", "CSV + JSON"), 174);
            _recalibrationRow = CreateSettingRow("Recalibration, min",
                UiText.T("После randomized phase удерживает multiplier 1.000.", "Keeps multiplier 1.000 after the randomized phase."),
                _recalibrationMinutesBox);
            _seedValueRow = CreateSettingRow("Seed",
                UiText.T("Одинаковый seed повторяет последовательность.", "The same seed repeats the sequence."), _seedBox);
            page.Children.Add(CreateSection(UiText.T("Сессия и данные", "Session and data"),
                UiText.T("Автостоп, повторяемость и локальная история", "Auto-stop, repeatability and local history"),
                new[]
                {
                    CreateSettingRow(UiText.T("Randomized phase, min", "Randomized phase, min"),
                        UiText.T("0 означает работу без лимита.", "0 means no time limit."), _durationMinutesBox),
                    _recalibrationRow,
                    CreateSettingRow(UiText.T("Повторяемая последовательность", "Repeatable sequence"),
                        UiText.T("Фиксированный seed для сравнимых сессий.", "Fixed seed for comparable sessions."), _useSeedBox),
                    _seedValueRow,
                    CreateSettingRow(UiText.T("Логирование", "Logging"),
                        UiText.T("Сохраняется только локально в LocalAppData.", "Stored locally in LocalAppData only."), _loggingBox)
                }));

            _editableControls.AddRange(new Control[]
            {
                _dpiBox, _baseSensitivityBox, _durationMinutesBox, _recalibrationMinutesBox,
                _useSeedBox, _seedBox, _loggingBox
            });
            WireOptionalEvents();
            return page;
        }

        private StackPanel BuildGuidePage()
        {
            var page = new StackPanel();
            page.Children.Add(CreateCallout(
                "CONSTANT GAIN ONLY",
                UiText.T("Accel Type должен быть Off/noaccel во всех профилях Raw Accel.",
                    "Accel Type must be Off/noaccel in every Raw Accel profile."),
                AccentBrush,
                FrozenBrush(UiPalette.Current.RequiredBackgroundColor)));
            page.Children.Add(CreateTextSection(UiText.T("Быстрый старт", "Quick start"), UiText.T(
                "1. В Raw Accel выбери Off/noaccel и нажми Apply.\n" +
                "2. Нажми «Проверить». Нужны constant gain и multiplier 1.000.\n" +
                "3. Настрой диапазон и интервалы.\n" +
                "4. Нажми Start. Stop или Reset возвращают 1.000.",
                "1. Select Off/noaccel in Raw Accel and click Apply.\n" +
                "2. Click Verify. Constant gain and multiplier 1.000 are required.\n" +
                "3. Configure the range and timing.\n" +
                "4. Click Start. Stop or Reset restores 1.000.")));
            page.Children.Add(CreateTextSection(UiText.T("Balanced Coverage", "Balanced Coverage"), UiText.T(
                "Каждый цикл посещает восемь логарифмических зон: четыре ниже и четыре выше 1.000. При связанных границах geometric mean полного цикла равен 1.000.",
                "Every cycle visits eight logarithmic zones: four below and four above 1.000. With linked bounds, a full cycle's geometric mean is 1.000.")));
            page.Children.Add(CreateTextSection(UiText.T("Полное отключение", "Complete shutdown"), UiText.T(
                "Stop, Reset, завершение таймера и штатное закрытие возвращают multiplier 1.000 с readback. После закрытия программа не остаётся в tray и не запускается с Windows.",
                "Stop, Reset, timer completion and normal close restore multiplier 1.000 with readback. The app does not remain in the tray or start with Windows.")));
            return page;
        }

        private UIElement BuildMonitorPanel()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(124) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

            var chartCard = CreateCard(new Thickness(12, 9, 12, 11), 15);
            chartCard.Margin = new Thickness(0, 0, 0, 10);
            var chartGrid = new Grid();
            chartGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
            chartGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            AddToGrid(chartGrid, new TextBlock
            {
                Text = UiText.T("●  LIVE МНОЖИТЕЛЬ", "●  LIVE MULTIPLIER"),
                Foreground = AccentBrush,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            }, 0, 0);
            _chart = new WpfMultiplierChart { Margin = new Thickness(0) };
            AddToGrid(chartGrid, _chart, 1, 0);
            chartCard.Child = chartGrid;

            var metricsCard = CreateCard(new Thickness(15, 9, 15, 9), 15);
            metricsCard.Margin = new Thickness(0, 0, 0, 10);
            var metrics = new Grid();
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(39, GridUnitType.Star) });
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(61, GridUnitType.Star) });
            for (var index = 0; index < 4; index++) metrics.RowDefinitions.Add(new RowDefinition());
            _driverLabel = CreateMetricValue(UiText.T("не проверен", "not checked"));
            _timerLabel = CreateMetricValue("RND 00:00");
            _nextChangeLabel = CreateMetricValue("-");
            _changeCountLabel = CreateMetricValue("0");
            AddMetric(metrics, 0, "Raw Accel", _driverLabel);
            AddMetric(metrics, 1, UiText.T("Таймер", "Timer"), _timerLabel);
            AddMetric(metrics, 2, UiText.T("Следующая смена", "Next change"), _nextChangeLabel);
            AddMetric(metrics, 3, UiText.T("Изменений", "Changes"), _changeCountLabel);
            metricsCard.Child = metrics;

            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            _verifyButton = CreateButton(UiText.T("Проверить", "Verify"), ButtonTone.Ghost, double.NaN, 36);
            _openLogsButton = CreateButton(UiText.T("Открыть логи", "Open logs"), ButtonTone.Ghost, double.NaN, 36);
            _verifyButton.Click += async (sender, args) => await VerifyRawAccelFromUiAsync();
            _openLogsButton.Click += (sender, args) => OpenLogsFolder();
            AddToGrid(actions, _verifyButton, 0, 0);
            AddToGrid(actions, _openLogsButton, 0, 2);

            AddToGrid(grid, chartCard, 0, 0);
            AddToGrid(grid, metricsCard, 1, 0);
            AddToGrid(grid, actions, 2, 0);
            return grid;
        }

        private UIElement BuildCommandDock()
        {
            var dock = CreateCard(new Thickness(12, 8, 10, 8), 15);
            dock.Margin = new Thickness(0, 10, 0, 0);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70, GridUnitType.Star) });

            _hotkeyLabel = new TextBlock
            {
                Text = (_guardAvailable
                    ? UiText.T("●  ResetGuard готов", "●  ResetGuard ready")
                    : UiText.T("●  ResetGuard недоступен", "●  ResetGuard unavailable")) +
                    UiText.T("  ·  constant gain only", "  ·  constant gain only"),
                Foreground = _guardAvailable ? AccentBrush : WarningBrush,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 14, 0),
                ToolTip = UiText.T("F8 Start/Stop  ·  F9 Reset  ·  F10 Pause", "F8 Start/Stop  ·  F9 Reset  ·  F10 Pause")
            };

            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(21, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28, GridUnitType.Star) });
            _startButton = CreateButton(UiText.T("Старт  F8", "Start  F8"), ButtonTone.Primary, double.NaN, 40);
            _stopButton = CreateButton("Stop  F8", ButtonTone.Ghost, double.NaN, 40);
            _pauseButton = CreateButton(UiText.T("Пауза  F10", "Pause  F10"), ButtonTone.Ghost, double.NaN, 40);
            _resetButton = CreateButton(UiText.T("Сброс  F9", "Reset  F9"), ButtonTone.Danger, double.NaN, 40);
            _startButton.Click += async (sender, args) => await StartFromUiAsync();
            _stopButton.Click += async (sender, args) => await StopFromUiAsync();
            _pauseButton.Click += (sender, args) => _engine.TogglePause();
            _resetButton.Click += async (sender, args) => await PanicFromUiAsync();
            AddToGrid(actions, _startButton, 0, 0);
            AddToGrid(actions, _stopButton, 0, 2);
            AddToGrid(actions, _pauseButton, 0, 4);
            AddToGrid(actions, _resetButton, 0, 6);
            AddToGrid(grid, _hotkeyLabel, 0, 0);
            AddToGrid(grid, actions, 0, 1);
            dock.Child = grid;
            return dock;
        }

        private FrameworkElement WrapPage(UIElement content)
        {
            return new SmoothScrollHost(content);
        }

        private void SelectPage(int index)
        {
            _selectedPage = Math.Max(0, Math.Min(2, index));
            if (_essentialPage == null) return;
            _essentialPage.Visibility = _selectedPage == 0 ? Visibility.Visible : Visibility.Collapsed;
            _optionalPage.Visibility = _selectedPage == 1 ? Visibility.Visible : Visibility.Collapsed;
            _guidePage.Visibility = _selectedPage == 2 ? Visibility.Visible : Visibility.Collapsed;
            var activePage = _selectedPage == 0 ? _essentialPage : _selectedPage == 1 ? _optionalPage : _guidePage;
            AnimatePageEntrance(activePage);
            SetTabState(_essentialTabButton, _selectedPage == 0);
            SetTabState(_optionalTabButton, _selectedPage == 1);
            SetTabState(_guideTabButton, _selectedPage == 2);
        }

        private static void SetTabState(Button button, bool selected)
        {
            if (button == null) return;
            button.Background = selected
                ? (Brush)new LinearGradientBrush(UiPalette.Current.TabStartColor, UiPalette.Current.TabEndColor, 12)
                : Brushes.Transparent;
            button.BorderBrush = selected ? AccentBrush : Brushes.Transparent;
            button.Foreground = selected ? TextPrimaryBrush : TextSecondaryBrush;
        }

        private static Border CreateCard(Thickness padding, double radius)
        {
            return new Border
            {
                Background = SurfaceBrush,
                BorderBrush = OutlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = padding,
                SnapsToDevicePixels = true
            };
        }

        private static Border CreateCallout(string title, string body, Brush accent, Brush background)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent
            });
            stack.Children.Add(new TextBlock
            {
                Text = body,
                FontSize = 11.5,
                Foreground = TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
            return new Border
            {
                Background = background,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(13, 9, 13, 9),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack
            };
        }

        private static Border CreateSection(string title, string subtitle, IEnumerable<FrameworkElement> rows)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextPrimaryBrush,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                content.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = TextSecondaryBrush,
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 7)
                });
            }
            foreach (var row in rows) content.Children.Add(row);
            return new Border
            {
                Background = SurfaceBrush,
                BorderBrush = BorderSoftBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15, 12, 15, 7),
                Margin = new Thickness(0, 0, 0, 10),
                Child = content
            };
        }

        private static Border CreateTextSection(string title, string body)
        {
            return CreateSection(title, string.Empty, new[]
            {
                new TextBlock
                {
                    Text = body,
                    Foreground = TextSecondaryBrush,
                    FontSize = 12,
                    LineHeight = 18,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 5)
                }
            });
        }

        private static Border CreateSettingRow(string title, string description, FrameworkElement editor, TextBlock titleTarget = null)
        {
            var grid = new Grid { MinHeight = 63 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(186) });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 14, 7) };
            var titleBlock = titleTarget ?? new TextBlock();
            titleBlock.Text = title;
            titleBlock.Foreground = TextPrimaryBrush;
            titleBlock.FontSize = 12.5;
            titleBlock.FontWeight = FontWeights.SemiBold;
            titleBlock.TextTrimming = TextTrimming.CharacterEllipsis;
            copy.Children.Add(titleBlock);
            copy.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = TextSecondaryBrush,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            editor.HorizontalAlignment = HorizontalAlignment.Right;
            editor.VerticalAlignment = VerticalAlignment.Center;
            AddToGrid(grid, copy, 0, 0);
            AddToGrid(grid, editor, 0, 1);
            return new Border
            {
                BorderBrush = BorderSoftBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = grid
            };
        }

        private static Border CreateInlineNote(TextBlock label, Brush background, Brush border)
        {
            return new Border
            {
                Background = background,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 1, 0, 8),
                Child = label
            };
        }

        private NumericField CreateNumeric(decimal minimum, decimal maximum, decimal value, int decimals, decimal increment, double width)
        {
            var numeric = new NumericField
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals,
                Increment = increment,
                Width = width,
                Height = 36
            };
            numeric.Value = value;
            numeric.RefreshText();
            _numericControls.Add(numeric);
            return numeric;
        }

        private static ComboBox CreateCombo(IEnumerable<string> items, double width)
        {
            var combo = new ComboBox
            {
                Width = width,
                Height = 36,
                Background = InputBrush,
                Foreground = TextPrimaryBrush,
                BorderBrush = OutlineBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 3, 7, 3),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxDropDownHeight = 280,
                Template = CreateComboTemplate()
            };
            foreach (var item in items) combo.Items.Add(item);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, InputBrush));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            itemStyle.Triggers.Add(new Trigger
            {
                Property = ComboBoxItem.IsHighlightedProperty,
                Value = true,
                Setters = { new Setter(Control.BackgroundProperty, SurfaceRaisedBrush) }
            });
            combo.ItemContainerStyle = itemStyle;
            combo.IsEnabledChanged += (sender, args) => combo.Opacity = combo.IsEnabled ? 1.0 : 0.42;
            return combo;
        }

        private static ControlTemplate CreateComboTemplate()
        {
            var root = new FrameworkElementFactory(typeof(Grid));

            var border = new FrameworkElementFactory(typeof(Border), "ComboBorder");
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

            var inside = new FrameworkElementFactory(typeof(Grid));
            var selection = new FrameworkElementFactory(typeof(ContentPresenter));
            selection.SetBinding(ContentPresenter.ContentProperty,
                new Binding("SelectionBoxItem") { RelativeSource = RelativeSource.TemplatedParent });
            selection.SetBinding(ContentPresenter.ContentTemplateProperty,
                new Binding("SelectionBoxItemTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            selection.SetBinding(ContentPresenter.ContentStringFormatProperty,
                new Binding("SelectionBoxItemStringFormat") { RelativeSource = RelativeSource.TemplatedParent });
            selection.SetBinding(TextElement.ForegroundProperty,
                new Binding("Foreground") { RelativeSource = RelativeSource.TemplatedParent });
            selection.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 0, 32, 0));
            selection.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            selection.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            selection.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);
            inside.AppendChild(selection);

            var toggle = new FrameworkElementFactory(typeof(ToggleButton), "ComboToggle");
            toggle.SetValue(ToggleButton.BackgroundProperty, Brushes.Transparent);
            toggle.SetValue(ToggleButton.BorderThicknessProperty, new Thickness(0));
            toggle.SetValue(ToggleButton.FocusableProperty, false);
            toggle.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
            toggle.SetValue(ToggleButton.TemplateProperty, CreateComboToggleTemplate());
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.TwoWay
            });
            inside.AppendChild(toggle);
            border.AppendChild(inside);
            root.AppendChild(border);

            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.StaysOpenProperty, false);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.TwoWay
            });

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, InputBrush);
            popupBorder.SetValue(Border.BorderBrushProperty, OutlineBrush);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(3));
            popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            popupBorder.SetBinding(Border.MinWidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = RelativeSource.TemplatedParent
            });

            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scroll.SetValue(ScrollViewer.MaxHeightProperty, 280.0);
            var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(presenter);
            popupBorder.AppendChild(scroll);
            popup.AppendChild(popupBorder);
            root.AppendChild(popup);
            return new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
        }

        private static ControlTemplate CreateComboToggleTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var arrow = new FrameworkElementFactory(typeof(TextBlock));
            arrow.SetValue(TextBlock.TextProperty, "▾");
            arrow.SetValue(TextBlock.FontSizeProperty, 12.0);
            arrow.SetValue(TextBlock.ForegroundProperty, TextSecondaryBrush);
            arrow.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 11, 1));
            border.AppendChild(arrow);
            return new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
        }

        private static ToggleSwitch CreateToggle(string text, double width)
        {
            return new ToggleSwitch
            {
                Text = text,
                Width = width,
                Height = 28
            };
        }

        private static Button CreateButton(string text, ButtonTone tone, double width, double height)
        {
            var normal = tone == ButtonTone.Primary
                ? (Brush)new LinearGradientBrush(UiPalette.Current.PrimaryStartColor, UiPalette.Current.PrimaryEndColor, 12)
                : tone == ButtonTone.Danger ? FrozenBrush(UiPalette.Current.DangerSurfaceColor) : tone == ButtonTone.Tab ? Brushes.Transparent : SurfaceRaisedBrush;
            var hover = tone == ButtonTone.Primary
                ? (Brush)new LinearGradientBrush(UiPalette.Current.PrimaryHoverStartColor, UiPalette.Current.PrimaryHoverEndColor, 12)
                : tone == ButtonTone.Danger ? FrozenBrush(UiPalette.Current.DangerHoverColor) : FrozenBrush(UiPalette.Current.GhostHoverColor);
            var border = tone == ButtonTone.Primary ? AccentBrush : tone == ButtonTone.Danger ? DangerBrush : OutlineBrush;
            var foreground = tone == ButtonTone.Danger ? DangerBrush : TextPrimaryBrush;

            var button = new Button
            {
                Content = text,
                Height = height,
                Background = normal,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Foreground = foreground,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Padding = new Thickness(11, 0, 11, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template = CreateButtonTemplate(),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };
            if (!double.IsNaN(width)) button.Width = width;
            if (tone != ButtonTone.Tab)
            {
                button.MouseEnter += (sender, args) =>
                {
                    if (!button.IsEnabled) return;
                    button.Background = hover;
                    AnimateButtonScale(button, 1.015);
                };
                button.MouseLeave += (sender, args) =>
                {
                    button.Background = normal;
                    AnimateButtonScale(button, 1.0);
                };
            }
            button.IsEnabledChanged += (sender, args) => button.Opacity = button.IsEnabled ? 1.0 : 0.38;
            return button;
        }

        private static ControlTemplate CreateButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(TextElement.ForegroundProperty, new Binding("Foreground") { RelativeSource = RelativeSource.TemplatedParent });
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private static TextBlock CreateCaption(string text, Brush brush)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold
            };
        }

        private static Border CreateDivider()
        {
            return new Border
            {
                Width = 1,
                Background = OutlineBrush,
                Margin = new Thickness(0, 7, 0, 7)
            };
        }

        private static TextBlock CreateMetricValue(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = TextPrimaryBrush,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static void AddMetric(Grid grid, int row, string name, TextBlock value)
        {
            AddToGrid(grid, new TextBlock
            {
                Text = name,
                Foreground = TextSecondaryBrush,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            }, row, 0);
            AddToGrid(grid, value, row, 1);
        }

        private static void AddToGrid(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static SolidColorBrush FrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush FrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static void AnimateButtonScale(Button button, double target)
        {
            var scale = button.RenderTransform as ScaleTransform;
            if (scale == null) return;
            var duration = TimeSpan.FromMilliseconds(100);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, duration) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(target, duration) { EasingFunction = easing });
        }

        private static void AnimatePageEntrance(FrameworkElement page)
        {
            if (page == null || page.Visibility != Visibility.Visible) return;
            var translate = page.RenderTransform as TranslateTransform;
            if (translate == null)
            {
                translate = new TranslateTransform();
                page.RenderTransform = translate;
            }
            page.Opacity = 0;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private enum ButtonTone
        {
            Primary,
            Ghost,
            Danger,
            Tab
        }
    }
}
