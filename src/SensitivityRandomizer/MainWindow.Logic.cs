using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SensitivityRandomizer
{
    public sealed partial class MainWindow
    {
        private async Task InspectRawAccelAtStartupAsync()
        {
            SetStatusVisual(EngineState.Starting, UiText.T(
                "Чтение активной конфигурации драйвера...",
                "Reading the active driver configuration..."));
            try
            {
                var readback = await _controller.InspectAsync(CancellationToken.None);
                UpdateDriverReadbackDisplay(readback);

                if (!readback.AllProfilesMatch(1.0, 0.0005))
                {
                    var answer = MessageBox.Show(
                        this,
                        UiText.T(
                            "Raw Accel сейчас использует multiplier, отличный от 1.000:\n\n",
                            "Raw Accel currently uses a multiplier other than 1.000:\n\n") +
                        readback.DescribeProfiles() +
                        UiText.T(
                            "\n\nСбросить Output DPI всех профилей к multiplier 1.000? Другие параметры Raw Accel не изменятся.",
                            "\n\nReset every profile Output DPI to multiplier 1.000? Other Raw Accel settings will remain unchanged."),
                        UiText.T("Нужен safety reset", "Safety reset required"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.Yes);

                    if (answer == MessageBoxResult.Yes)
                    {
                        if (!ArmGuard())
                            throw new InvalidOperationException(UiText.T(
                                "Не удалось активировать crash reset guard.",
                                "Could not arm the crash reset guard."));
                        await _engine.ResetOnlyAsync();
                        readback = await _controller.InspectAsync(CancellationToken.None);
                        UpdateDriverReadbackDisplay(readback);
                        DisarmGuard();
                    }
                    else
                    {
                        _rawAccelReady = false;
                        SetStatusVisual(EngineState.Off, UiText.T(
                            "Start заблокирован до reset к 1.000.",
                            "Start is blocked until reset to 1.000."));
                    }
                }

                if (readback.AccelerationDetected)
                {
                    BlockForSpeedAcceleration(readback, true);
                }
                else if (readback.AllProfilesMatch(1.0, 0.0005))
                {
                    _rawAccelReady = true;
                    SetStatusVisual(EngineState.Off, UiText.T(
                        "CONSTANT GAIN ПОДТВЕРЖДЁН. Acceleration = OFF, multiplier = 1.000.",
                        "CONSTANT GAIN VERIFIED. Acceleration = OFF, multiplier = 1.000."));
                }
                else
                {
                    _rawAccelReady = false;
                }

                var compatibilityWarning = readback.BuildCompatibilityWarning();
                if (!string.IsNullOrWhiteSpace(compatibilityWarning))
                {
                    MessageBox.Show(
                        this,
                        compatibilityWarning + UiText.T(
                            "\n\nПрограмма не изменяет эти параметры молча. Speed-based acceleration всегда блокирует Start.",
                            "\n\nThe app never changes these settings silently. Speed-based acceleration always blocks Start."),
                        UiText.T("Проверка Raw Accel", "Raw Accel setup check"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _rawAccelReady = false;
                _driverLabel.Text = "connection failed";
                SetStatusVisual(EngineState.Error, ex.Message);
                MessageBox.Show(
                    this,
                    UiText.T("Не удалось подключиться к Raw Accel 1.7.x.\n\n", "Could not connect to Raw Accel 1.7.x.\n\n") +
                    ex.Message + UiText.T(
                        "\n\nУстанови официальный Raw Accel v1.7.1, перезагрузи Windows и запусти программу снова.",
                        "\n\nInstall official Raw Accel v1.7.1, reboot Windows, then start this app again."),
                    UiText.T("Raw Accel недоступен", "Raw Accel unavailable"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UpdateControlAvailability();
            }
        }

        private async Task StartFromUiAsync()
        {
            if (!_rawAccelReady)
            {
                MessageBox.Show(this,
                    UiText.T("Сначала нужен подтверждённый Raw Accel multiplier 1.000.", "A verified Raw Accel multiplier of 1.000 is required first."),
                    UiText.T("Start заблокирован", "Start blocked"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _settings = ReadSettingsFromControls();
                _settings.Validate();
                _settings.Save(_settingsPath);

                var preflight = await _controller.InspectAsync(CancellationToken.None);
                UpdateDriverReadbackDisplay(preflight);
                if (preflight.AccelerationDetected)
                {
                    BlockForSpeedAcceleration(preflight, true);
                    UpdateControlAvailability();
                    return;
                }

                if (!preflight.AllProfilesMatch(1.0, 0.0005))
                {
                    var answer = MessageBox.Show(
                        this,
                        UiText.T(
                            "Перед Start активный Raw Accel multiplier уже не равен 1.000:\n\n",
                            "Before Start, the active Raw Accel multiplier is no longer 1.000:\n\n") +
                        preflight.DescribeProfiles() +
                        UiText.T("\n\nСбросить его и начать с базового состояния 1.000?", "\n\nReset it and start from the 1.000 baseline?"),
                        "Preflight check",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.Yes);
                    if (answer != MessageBoxResult.Yes)
                    {
                        _rawAccelReady = false;
                        SetStatusVisual(EngineState.Off, UiText.T(
                            "Start заблокирован: активный multiplier не равен 1.000. Используй F9 для reset.",
                            "Start blocked: active multiplier is not 1.000. Use F9 to reset."));
                        UpdateControlAvailability();
                        return;
                    }

                    if (!ArmGuard())
                        throw new InvalidOperationException(UiText.T(
                            "Не удалось активировать ResetGuard. Сессия не запущена.",
                            "Could not arm ResetGuard. The session was not started."));
                    await _engine.ResetOnlyAsync();
                    preflight = await _controller.InspectAsync(CancellationToken.None);
                    UpdateDriverReadbackDisplay(preflight);
                    if (preflight.AccelerationDetected)
                    {
                        BlockForSpeedAcceleration(preflight, true);
                        DisarmGuard();
                        UpdateControlAvailability();
                        return;
                    }
                    if (!preflight.AllProfilesMatch(1.0, 0.0005))
                        throw new InvalidOperationException(UiText.T(
                            "Reset выполнен, но readback не подтвердил multiplier 1.000.",
                            "Reset completed, but readback did not confirm multiplier 1.000."));
                }

                if (!ArmGuard())
                    throw new InvalidOperationException(UiText.T(
                        "Не удалось активировать ResetGuard. Сессия не запущена.",
                        "Could not arm ResetGuard. The session was not started."));
                _chart.ClearPoints();
                _chart.Configure(_settings.MinimumMultiplier, _settings.MaximumMultiplier, _settings.ChartWindowSeconds);
                _engine.Start(_settings);
                UpdateControlAvailability();
            }
            catch (Exception ex)
            {
                if (_engine.State == EngineState.Off) DisarmGuard();
                MessageBox.Show(this, ex.Message, UiText.T("Некорректные настройки", "Invalid settings"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task StopFromUiAsync()
        {
            try
            {
                await _engine.StopAsync();
                var readback = await _controller.InspectAsync(CancellationToken.None);
                UpdateDriverReadbackDisplay(readback);
                _rawAccelReady = !readback.AccelerationDetected && readback.AllProfilesMatch(1.0, 0.0005);
                DisarmGuard();
                if (readback.AccelerationDetected) BlockForSpeedAcceleration(readback, true);
            }
            catch (Exception ex)
            {
                _rawAccelReady = false;
                MessageBox.Show(this, ex.Message, UiText.T("Ошибка Stop / reset", "Stop / reset error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateControlAvailability();
            }
        }

        private async Task PanicFromUiAsync()
        {
            try
            {
                if (!ArmGuard())
                    throw new InvalidOperationException(UiText.T("Не удалось активировать ResetGuard.", "Could not arm ResetGuard."));
                await _engine.PanicAsync();
                var readback = await _controller.InspectAsync(CancellationToken.None);
                UpdateDriverReadbackDisplay(readback);
                _rawAccelReady = !readback.AccelerationDetected && readback.AllProfilesMatch(1.0, 0.0005);
                DisarmGuard();
                if (readback.AccelerationDetected) BlockForSpeedAcceleration(readback, true);
            }
            catch (Exception ex)
            {
                _rawAccelReady = false;
                MessageBox.Show(this,
                    UiText.T(
                        "Reset к 1.000 не подтверждён. Открой Raw Accel и нажми Reset/Apply вручную.\n\n",
                        "Reset to 1.000 was not confirmed. Open Raw Accel and use Reset/Apply manually.\n\n") + ex.Message,
                    UiText.T("Критическая ошибка reset", "Critical reset error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateControlAvailability();
            }
        }

        private async Task VerifyRawAccelFromUiAsync()
        {
            _verifyButton.IsEnabled = false;
            try
            {
                var readback = await _controller.InspectAsync(CancellationToken.None);
                UpdateDriverReadbackDisplay(readback);
                var state = _engine.State;
                var inactive = state == EngineState.Off || state == EngineState.Completed || state == EngineState.Error;
                var baseline = readback.AllProfilesMatch(1.0, 0.0005);
                var constantGain = !readback.AccelerationDetected;
                var disabled = inactive && baseline;
                if (inactive) _rawAccelReady = constantGain && baseline;
                if (!constantGain)
                    BlockForSpeedAcceleration(readback, false);
                else if (inactive && baseline)
                    SetStatusVisual(EngineState.Off, UiText.T(
                        "Проверено: constant gain, multiplier = 1.000, randomizer выключен.",
                        "Verified: constant gain, multiplier = 1.000, randomizer is off."));

                MessageBox.Show(this,
                    "Constant gain: " + (constantGain ? "VERIFIED" : "FAILED") +
                    "\nSpeed-based acceleration: " + (constantGain ? "OFF" : string.Join("; ", readback.AccelerationModes ?? new string[0])) +
                    "\nMultiplier readback: " + readback.DescribeProfiles() +
                    "\nRandomizer state: " + state +
                    UiText.T("\nОтключение подтверждено: ", "\nShutdown confirmed: ") + UiText.YesNo(disabled),
                    UiText.T("Проверка активного состояния", "Active state verification"),
                    MessageBoxButton.OK,
                    constantGain && (baseline || !inactive) ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _rawAccelReady = false;
                MessageBox.Show(this, ex.Message, UiText.T("Проверка не выполнена", "Verification failed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!_disposed) _verifyButton.IsEnabled = true;
                UpdateControlAvailability();
            }
        }

        private void UpdateDriverReadbackDisplay(MultiplierReadback readback)
        {
            _lastReadback = readback;
            _driverLabel.Text = "v" + readback.DriverVersion + "  ·  " +
                (readback.AccelerationDetected ? "ACCEL BLOCKED" : "CONST GAIN") +
                "  ·  " + readback.ProfileMultipliers.Count + " " + UiText.T("проф.", "profile(s)");
            _driverLabel.Foreground = readback.AccelerationDetected ? DangerBrush : AccentBrush;
            _displayedMultiplier = readback.RepresentativeMultiplier;
            UpdateEffectiveValues();
        }

        private void BlockForSpeedAcceleration(MultiplierReadback readback, bool showDialog)
        {
            _rawAccelReady = false;
            var modes = string.Join("; ", readback.AccelerationModes ?? new string[0]);
            SetStatusVisual(EngineState.Error, UiText.T(
                "START BLOCKED: обнаружена speed-based acceleration. Выставь Off/noaccel.",
                "START BLOCKED: speed-based acceleration detected. Set Off/noaccel."));
            if (!showDialog) return;

            MessageBox.Show(this,
                UiText.T(
                    "Randomization заблокирована: sensitivity сейчас зависит от скорости движения мыши.\n\n",
                    "Randomization is blocked: sensitivity currently depends on mouse movement speed.\n\n") +
                modes + UiText.T(
                    "\n\nВ Raw Accel выставь Accel Type = Off/noaccel во всех профилях, нажми Apply, затем «Проверить».",
                    "\n\nSet Accel Type = Off/noaccel in every Raw Accel profile, click Apply, then Verify."),
                UiText.T("Constant gain не подтверждён", "Constant gain not verified"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task HandleHotKeyAsync(int id)
        {
            if (_shutdownStarted) return;
            if (id == HotKeyPanic)
            {
                await PanicFromUiAsync();
                return;
            }
            if (id == HotKeyPause)
            {
                _engine.TogglePause();
                return;
            }
            if (id == HotKeyToggle)
            {
                var state = _engine.State;
                if (state == EngineState.Off || state == EngineState.Completed)
                    await StartFromUiAsync();
                else
                    await StopFromUiAsync();
            }
        }

        private async void BeginShutdownAsync()
        {
            SetStatusVisual(EngineState.Resetting, UiText.T(
                "Закрытие: обязательный reset к 1.000...",
                "Closing: mandatory reset to 1.000..."));
            try
            {
                var current = ReadSettingsFromControls();
                current.Save(_settingsPath);
            }
            catch
            {
                // Saving preferences must never block the safety reset.
            }

            try
            {
                if (_safetyResetRequired || (_engine.State != EngineState.Off && _engine.State != EngineState.Completed))
                {
                    await _engine.StopAsync();
                    DisarmGuard();
                }
            }
            catch
            {
                // ResetGuard independently retries after process exit.
            }
            finally
            {
                _allowClose = true;
                // Closing can complete synchronously when the engine is already
                // inactive. A second Close() from inside the first Closing event
                // is illegal in WPF, so always start the final close on the next
                // dispatcher turn.
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Close));
            }
        }

        private void EngineOnStatusChanged(object sender, EngineStatusEventArgs args)
        {
            SafeUi(() =>
            {
                SetStatusVisual(args.State, args.Detail);
                if (args.State == EngineState.Off && Math.Abs(_displayedMultiplier - 1.0) < 0.001)
                    _rawAccelReady = true;
                UpdateControlAvailability();
            });
        }

        private void EngineOnSampleApplied(object sender, AppliedSampleEventArgs args)
        {
            SafeUi(() =>
            {
                _displayedMultiplier = args.ActualMultiplier;
                _chart.AddPoint(args.TimestampUtc, args.ActualMultiplier);
                UpdateEffectiveValues();
            });
        }

        private void EngineOnSessionCompleted(object sender, SessionCompletedEventArgs args)
        {
            SafeUi(() =>
            {
                if (_shutdownStarted || args.Summary == null || args.Summary.ChangeCount == 0) return;
                var summary = args.Summary;
                var logLine = string.IsNullOrWhiteSpace(summary.CsvPath)
                    ? UiText.T("Логирование было отключено.", "Logging was disabled.")
                    : "CSV: " + summary.CsvPath;
                if (!string.IsNullOrWhiteSpace(summary.LoggingWarning)) logLine += "\nWARNING: " + summary.LoggingWarning;
                MessageBox.Show(this,
                    UiText.T("Изменений: ", "Changes: ") + summary.ChangeCount +
                    "\n" + UiText.T("Geometric mean по сменам: ", "Geometric mean by changes: ") + summary.GeometricMeanMultiplier.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\n" + UiText.T("Geometric mean по времени: ", "Geometric mean by time: ") + summary.TimeWeightedGeometricMeanMultiplier.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\n" + UiText.T("Ниже / выше 1.000, смены: ", "Below / above 1.000, changes: ") +
                    summary.BelowBaseChangePercent.ToString("0.0", CultureInfo.InvariantCulture) + "% / " + summary.AboveBaseChangePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%" +
                    "\n" + UiText.T("Ниже / выше 1.000, время: ", "Below / above 1.000, time: ") +
                    summary.BelowBaseTimePercent.ToString("0.0", CultureInfo.InvariantCulture) + "% / " + summary.AboveBaseTimePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%" +
                    "\n" + UiText.T("Арифметический mean: ", "Arithmetic mean: ") + summary.MeanMultiplier.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\nSD: " + summary.StandardDeviation.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\nMin / Max: " + summary.MinimumMultiplier.ToString("0.000", CultureInfo.InvariantCulture) + " / " + summary.MaximumMultiplier.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\nDuration: " + FormatTime(summary.RandomizedDuration) + "\n\n" + logLine,
                    UiText.T("Статистика сессии", "Session statistics"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void UpdateSessionClock()
        {
            var snapshot = _engine.GetSnapshot();
            if (Math.Abs(snapshot.CurrentMultiplier - _displayedMultiplier) > 0.0000001)
            {
                _displayedMultiplier = snapshot.CurrentMultiplier;
                UpdateEffectiveValues();
            }
            if (_safetyResetRequired &&
                (snapshot.State == EngineState.Off || snapshot.State == EngineState.Completed) &&
                Math.Abs(snapshot.CurrentMultiplier - 1.0) < 0.0005)
                DisarmGuard();

            var phase = snapshot.Phase == SessionPhase.Randomized ? "RND" : "RECAL";
            _timerLabel.Text = snapshot.PhaseRemaining.HasValue
                ? phase + " " + FormatTime(snapshot.PhaseElapsed) + " / -" + FormatTime(snapshot.PhaseRemaining.Value)
                : phase + " " + FormatTime(snapshot.PhaseElapsed);
            if (snapshot.NextChangeUtc.HasValue && snapshot.State == EngineState.Running)
            {
                var seconds = Math.Max(0, (snapshot.NextChangeUtc.Value - DateTime.UtcNow).TotalSeconds);
                _nextChangeLabel.Text = seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            }
            else
            {
                _nextChangeLabel.Text = snapshot.State == EngineState.Paused ? UiText.T("пауза", "paused") : "-";
            }
            _changeCountLabel.Text = snapshot.ChangeCount.ToString(CultureInfo.InvariantCulture);
        }

        private void SetStatusVisual(EngineState state, string detail)
        {
            _statusLabel.Text = StatusText(state);
            _statusLabel.Foreground = StatusBrush(state);
            _statusDetailLabel.Text = detail ?? string.Empty;
            _statusDetailLabel.ToolTip = detail ?? string.Empty;
            _pauseButton.Content = state == EngineState.Paused
                ? UiText.T("Продолжить  F10", "Resume  F10")
                : UiText.T("Пауза  F10", "Pause  F10");
        }

        private void UpdateEffectiveValues()
        {
            var dpi = _dpiBox == null ? _settings.Dpi : (int)_dpiBox.Value;
            var baseSensitivity = _baseSensitivityBox == null ? _settings.BaseSensitivity : (double)_baseSensitivityBox.Value;
            var effective = SensitivityMath.EffectiveSensitivity(baseSensitivity, _displayedMultiplier);
            var edpi = SensitivityMath.EffectiveEdpi(dpi, baseSensitivity, _displayedMultiplier);
            _multiplierLabel.Text = _displayedMultiplier.ToString("0.000", CultureInfo.InvariantCulture);
            _effectiveSensitivityLabel.Text = "sens " + effective.ToString("0.000", CultureInfo.InvariantCulture);
            _effectiveEdpiLabel.Text = edpi.ToString("0.0", CultureInfo.InvariantCulture) + " eDPI";
        }

        private void UpdateControlAvailability()
        {
            var editable = IsEditableState();
            foreach (var control in _editableControls) control.IsEnabled = editable;
            _startButton.IsEnabled = editable && _rawAccelReady && _settingsValid;
            _stopButton.IsEnabled = !editable;
            _pauseButton.IsEnabled = _engine.State == EngineState.Running || _engine.State == EngineState.Paused;
            _resetButton.IsEnabled = _engine.State != EngineState.Resetting && _engine.State != EngineState.Starting;
            if (_importButton != null) _importButton.IsEnabled = editable;
            if (_exportButton != null) _exportButton.IsEnabled = editable;
            if (_languageBox != null) _languageBox.IsEnabled = editable;
            if (_themeBox != null) _themeBox.IsEnabled = editable;
            UpdateModeAvailability();
            UpdateIntervalAvailability();
            UpdateOptionalVisibility();
            ValidateSettingsPreview();
        }

        private bool IsEditableState()
        {
            var state = _engine.State;
            return state == EngineState.Off || state == EngineState.Completed || state == EngineState.Error;
        }

        private void UpdateModeAvailability()
        {
            if (_modeBox == null) return;
            var editable = IsEditableState();
            var mode = ModeFromIndex(_modeBox.SelectedIndex);
            _maximumStepBox.IsEnabled = editable && mode == RandomizerMode.RandomWalk;
            _returnStrengthBox.IsEnabled = editable && mode == RandomizerMode.RandomWalk;
            _gaussianMeanBox.IsEnabled = editable && mode == RandomizerMode.Gaussian;
            _gaussianSigmaBox.IsEnabled = editable && mode == RandomizerMode.Gaussian;
            _randomWalkStepRow.Visibility = mode == RandomizerMode.RandomWalk ? Visibility.Visible : Visibility.Collapsed;
            _randomWalkReturnRow.Visibility = mode == RandomizerMode.RandomWalk ? Visibility.Visible : Visibility.Collapsed;
            _gaussianMeanRow.Visibility = mode == RandomizerMode.Gaussian ? Visibility.Visible : Visibility.Collapsed;
            _gaussianSigmaRow.Visibility = mode == RandomizerMode.Gaussian ? Visibility.Visible : Visibility.Collapsed;
            _reciprocalBoundsRow.Visibility = mode == RandomizerMode.BalancedCoverage ? Visibility.Visible : Visibility.Collapsed;
            _reciprocalBoundsBox.IsEnabled = editable && mode == RandomizerMode.BalancedCoverage;
            UpdateReciprocalBoundsState();
            _modeNote.Visibility = mode == RandomizerMode.BalancedCoverage || mode == RandomizerMode.Gaussian || mode == RandomizerMode.Uniform
                ? Visibility.Visible : Visibility.Collapsed;
            if (mode == RandomizerMode.BalancedCoverage)
                _modeDescriptionLabel.Text = UiText.T(
                    "8 логарифмических зон за цикл: ровно 4 ниже и 4 выше 1.000.",
                    "8 logarithmic zones per cycle: exactly 4 below and 4 above 1.000.");
            else if (mode == RandomizerMode.Gaussian)
                _modeDescriptionLabel.Text = UiText.T(
                    "Значения концентрируются около центра; края намеренно редкие.",
                    "Values concentrate near the center; the edges are deliberately rare.");
            else
                _modeDescriptionLabel.Text = UiText.T(
                    "Каждая точка линейного диапазона равновероятна.",
                    "Every point of the linear range is equally likely.");
        }

        private void UpdateIntervalAvailability()
        {
            if (_intervalModeBox == null) return;
            var editable = IsEditableState();
            var randomized = _intervalModeBox.SelectedIndex != 1;
            _maximumIntervalBox.IsEnabled = editable && randomized;
            _maximumIntervalRow.Visibility = randomized ? Visibility.Visible : Visibility.Collapsed;
            _minimumIntervalTitle.Text = randomized
                ? UiText.T("Минимальный интервал", "Minimum interval")
                : UiText.T("Фиксированный интервал", "Fixed interval");
            if (!randomized && _minimumIntervalBox.HasValidValue)
                _maximumIntervalBox.Value = _minimumIntervalBox.Value;
        }

        private void UpdateOptionalVisibility()
        {
            if (_durationMinutesBox == null || _useSeedBox == null) return;
            _recalibrationRow.Visibility = _durationMinutesBox.HasValidValue && _durationMinutesBox.Value > 0
                ? Visibility.Visible : Visibility.Collapsed;
            _seedValueRow.Visibility = _useSeedBox.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            _seedBox.IsEnabled = IsEditableState() && _useSeedBox.IsChecked;
        }

        private void UpdateChartConfiguration()
        {
            if (_chart == null || !_minimumMultiplierBox.HasValidValue || !_maximumMultiplierBox.HasValidValue) return;
            _chart.Configure((double)_minimumMultiplierBox.Value, (double)_maximumMultiplierBox.Value, _settings.ChartWindowSeconds);
        }

        private void WireEssentialEvents()
        {
            _presetBox.SelectionChanged += PresetBoxOnSelectionChanged;
            _modeBox.SelectionChanged += (sender, args) =>
            {
                MarkProfileCustom();
                UpdateModeAvailability();
                ValidateSettingsPreview();
            };
            foreach (var numeric in new[]
            {
                _minimumMultiplierBox, _maximumMultiplierBox, _maximumStepBox,
                _returnStrengthBox, _gaussianMeanBox, _gaussianSigmaBox
            })
                numeric.ValueChanged += (sender, args) => MarkProfileCustom();
            _minimumMultiplierBox.ValueChanged += (sender, args) => MinimumMultiplierOnValueChanged();
            _maximumMultiplierBox.ValueChanged += (sender, args) => MaximumMultiplierOnValueChanged();
            _reciprocalBoundsBox.CheckedChanged += (sender, args) => ReciprocalBoundsOnChanged();
            _intervalModeBox.SelectionChanged += (sender, args) =>
            {
                MarkProfileCustom();
                UpdateIntervalAvailability();
                ValidateSettingsPreview();
            };
            _minimumIntervalBox.ValueChanged += (sender, args) =>
            {
                MarkProfileCustom();
                if (_intervalModeBox.SelectedIndex == 1) _maximumIntervalBox.Value = _minimumIntervalBox.Value;
            };
            _maximumIntervalBox.ValueChanged += (sender, args) => MarkProfileCustom();

            foreach (var numeric in new[]
            {
                _minimumMultiplierBox, _maximumMultiplierBox, _maximumStepBox, _returnStrengthBox,
                _gaussianMeanBox, _gaussianSigmaBox, _minimumIntervalBox, _maximumIntervalBox
            }) numeric.InputStateChanged += (sender, args) => ValidateSettingsPreview();
            _reciprocalBoundsBox.CheckedChanged += (sender, args) => ValidateSettingsPreview();
        }

        private void WireOptionalEvents()
        {
            _dpiBox.ValueChanged += (sender, args) => UpdateEffectiveValues();
            _baseSensitivityBox.ValueChanged += (sender, args) => UpdateEffectiveValues();
            _durationMinutesBox.ValueChanged += (sender, args) =>
            {
                if (!_updatingControls && _durationMinutesBox.Value == 0) _recalibrationMinutesBox.Value = 0;
                UpdateOptionalVisibility();
            };
            _useSeedBox.CheckedChanged += (sender, args) => UpdateOptionalVisibility();
            foreach (var numeric in new[]
            {
                _dpiBox, _baseSensitivityBox, _durationMinutesBox, _recalibrationMinutesBox, _seedBox
            }) numeric.InputStateChanged += (sender, args) => ValidateSettingsPreview();
            _useSeedBox.CheckedChanged += (sender, args) => ValidateSettingsPreview();
            _loggingBox.CheckedChanged += (sender, args) => ValidateSettingsPreview();
        }

        private void ValidateSettingsPreview()
        {
            if (_updatingControls || _validationLabel == null) return;
            try
            {
                EnsureVisibleNumbersAreValid();
                var preview = ReadSettingsFromControls(false);
                preview.Validate();
                _settingsValid = true;
                _validationLabel.Text = UiText.T(
                    "✓ Настройки корректны. После проверки Raw Accel можно запускать.",
                    "✓ Settings are valid. Start is available after Raw Accel verification.");
                _validationLabel.Foreground = AccentBrush;
            }
            catch (Exception ex)
            {
                _settingsValid = false;
                _validationLabel.Text = "! " + ex.Message;
                _validationLabel.Foreground = DangerBrush;
            }
            if (_startButton != null) _startButton.IsEnabled = IsEditableState() && _rawAccelReady && _settingsValid;
        }

        private void EnsureVisibleNumbersAreValid()
        {
            RequireValidNumber(_minimumMultiplierBox, UiText.T("Нижняя граница", "Minimum multiplier"));
            RequireValidNumber(_maximumMultiplierBox, UiText.T("Верхняя граница", "Maximum multiplier"));
            RequireValidNumber(_minimumIntervalBox, UiText.T("Интервал", "Interval"));

            var mode = ModeFromIndex(_modeBox.SelectedIndex);
            if (mode == RandomizerMode.RandomWalk)
            {
                RequireValidNumber(_maximumStepBox, UiText.T("Максимальный шаг", "Maximum step"));
                RequireValidNumber(_returnStrengthBox, UiText.T("Возврат к 1.000", "Return toward 1.000"));
            }
            else if (mode == RandomizerMode.Gaussian)
            {
                RequireValidNumber(_gaussianMeanBox, UiText.T("Среднее значение", "Mean"));
                RequireValidNumber(_gaussianSigmaBox, "Sigma");
            }

            if (_intervalModeBox.SelectedIndex != 1)
                RequireValidNumber(_maximumIntervalBox, UiText.T("Максимальный интервал", "Maximum interval"));

            RequireValidNumber(_dpiBox, "Mouse DPI");
            RequireValidNumber(_baseSensitivityBox, UiText.T("Базовая sensitivity", "Base sensitivity"));
            RequireValidNumber(_durationMinutesBox, UiText.T("Randomized phase", "Randomized phase"));
            if (_durationMinutesBox.HasValidValue && _durationMinutesBox.Value > 0)
                RequireValidNumber(_recalibrationMinutesBox, "Recalibration");
            if (_useSeedBox.IsChecked)
                RequireValidNumber(_seedBox, "Seed");
        }

        private static void RequireValidNumber(NumericField numeric, string fieldName)
        {
            if (numeric != null && numeric.HasValidValue) return;
            throw new InvalidOperationException(UiText.T(
                "Поле «" + fieldName + "» содержит недопустимое значение.",
                "The “" + fieldName + "” field contains an invalid value."));
        }

        private void ReciprocalBoundsOnChanged()
        {
            if (_updatingControls) return;
            MarkProfileCustom();
            UpdateReciprocalBoundsState();
        }

        private void MinimumMultiplierOnValueChanged()
        {
            if (!_syncingReciprocalBounds && ReciprocalBoundsAreActive())
                SynchronizeReciprocalBound(_minimumMultiplierBox, _maximumMultiplierBox);
            UpdateChartConfiguration();
        }

        private void MaximumMultiplierOnValueChanged()
        {
            if (!_syncingReciprocalBounds && ReciprocalBoundsAreActive())
                SynchronizeReciprocalBound(_maximumMultiplierBox, _minimumMultiplierBox);
            UpdateChartConfiguration();
        }

        private bool ReciprocalBoundsAreActive()
        {
            return _reciprocalBoundsBox != null && _reciprocalBoundsBox.IsChecked &&
                _modeBox != null && ModeFromIndex(_modeBox.SelectedIndex) == RandomizerMode.BalancedCoverage;
        }

        private void UpdateReciprocalBoundsState()
        {
            if (_minimumMultiplierBox == null || _maximumMultiplierBox == null) return;
            if (!ReciprocalBoundsAreActive())
            {
                SetUnlinkedMultiplierEditorLimits();
                return;
            }

            _syncingReciprocalBounds = true;
            try
            {
                SetUnlinkedMultiplierEditorLimits();
                var upperMinimum = SmallestValueAboveOne(_maximumMultiplierBox.DecimalPlaces);
                var upper = ClampDecimal(_maximumMultiplierBox.Value, upperMinimum, (decimal)AppSettings.MaximumAllowedMultiplier);
                var lower = RoundedReciprocal(upper, _minimumMultiplierBox.DecimalPlaces);
                _maximumMultiplierBox.Value = upper;
                _minimumMultiplierBox.Value = ClampDecimal(
                    lower,
                    RoundedReciprocal((decimal)AppSettings.MaximumAllowedMultiplier, _minimumMultiplierBox.DecimalPlaces),
                    LargestValueBelowOne(_minimumMultiplierBox.DecimalPlaces));
                _minimumMultiplierBox.Minimum = RoundedReciprocal(
                    (decimal)AppSettings.MaximumAllowedMultiplier, _minimumMultiplierBox.DecimalPlaces);
                _minimumMultiplierBox.Maximum = LargestValueBelowOne(_minimumMultiplierBox.DecimalPlaces);
                _maximumMultiplierBox.Minimum = upperMinimum;
                _maximumMultiplierBox.Maximum = (decimal)AppSettings.MaximumAllowedMultiplier;
            }
            finally
            {
                _syncingReciprocalBounds = false;
            }
            UpdateChartConfiguration();
        }

        private void SynchronizeReciprocalBound(NumericField source, NumericField target)
        {
            _syncingReciprocalBounds = true;
            try
            {
                target.Value = ClampDecimal(
                    RoundedReciprocal(source.Value, target.DecimalPlaces),
                    target.Minimum,
                    target.Maximum);
            }
            finally
            {
                _syncingReciprocalBounds = false;
            }
        }

        private void SetUnlinkedMultiplierEditorLimits()
        {
            _minimumMultiplierBox.Minimum = (decimal)AppSettings.MinimumAllowedMultiplier;
            _minimumMultiplierBox.Maximum = (decimal)AppSettings.MaximumAllowedMultiplier;
            _maximumMultiplierBox.Minimum = (decimal)AppSettings.MinimumAllowedMultiplier;
            _maximumMultiplierBox.Maximum = (decimal)AppSettings.MaximumAllowedMultiplier;
        }

        private static decimal RoundedReciprocal(decimal value, int decimalPlaces)
        {
            return (decimal)ReciprocalRange.Counterpart((double)value, decimalPlaces);
        }

        private static decimal SmallestValueAboveOne(int decimalPlaces)
        {
            return 1m + 1m / DecimalPowerOfTen(decimalPlaces);
        }

        private static decimal LargestValueBelowOne(int decimalPlaces)
        {
            return 1m - 1m / DecimalPowerOfTen(decimalPlaces);
        }

        private static decimal DecimalPowerOfTen(int exponent)
        {
            var result = 1m;
            for (var index = 0; index < exponent; index++) result *= 10m;
            return result;
        }

        private void PresetBoxOnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (_updatingControls || _presetBox.SelectedIndex < 0 || _presetBox.SelectedIndex >= TrainingPreset.All.Count) return;
            var preset = TrainingPreset.All[_presetBox.SelectedIndex];
            _updatingControls = true;
            try
            {
                SetUnlinkedMultiplierEditorLimits();
                _modeBox.SelectedIndex = IndexFromMode(preset.Mode);
                _minimumMultiplierBox.Value = (decimal)preset.Minimum;
                _maximumMultiplierBox.Value = (decimal)preset.Maximum;
                _reciprocalBoundsBox.IsChecked = preset.Mode == RandomizerMode.BalancedCoverage &&
                    ReciprocalRange.AreSymmetric(preset.Minimum, preset.Maximum);
                _maximumStepBox.Value = (decimal)preset.Step;
                _returnStrengthBox.Value = (decimal)preset.ReturnStrength;
                _gaussianMeanBox.Value = (decimal)preset.Mean;
                _gaussianSigmaBox.Value = (decimal)preset.Sigma;
                _intervalModeBox.SelectedIndex = 0;
                _minimumIntervalBox.Value = (decimal)preset.IntervalMinimum;
                _maximumIntervalBox.Value = (decimal)preset.IntervalMaximum;
            }
            finally
            {
                _updatingControls = false;
            }
            UpdateModeAvailability();
            UpdateIntervalAvailability();
            UpdateChartConfiguration();
            ValidateSettingsPreview();
        }

        private void MarkProfileCustom()
        {
            if (_updatingControls || _presetBox == null || _presetBox.SelectedIndex < 0 ||
                _presetBox.SelectedIndex >= TrainingPreset.All.Count) return;
            _updatingControls = true;
            _presetBox.SelectedIndex = TrainingPreset.All.Count;
            _updatingControls = false;
        }

        private void ApplySettingsToControls(AppSettings settings)
        {
            _updatingControls = true;
            try
            {
                SetUnlinkedMultiplierEditorLimits();
                if (_languageBox != null) _languageBox.SelectedIndex = UiText.IndexOfLanguage(settings.Language);
                if (_themeBox != null) _themeBox.SelectedIndex = UiText.IndexOfTheme(settings.Theme);
                _dpiBox.Value = ClampDecimal(settings.Dpi, _dpiBox.Minimum, _dpiBox.Maximum);
                _baseSensitivityBox.Value = ClampDecimal((decimal)settings.BaseSensitivity, _baseSensitivityBox.Minimum, _baseSensitivityBox.Maximum);
                var presetIndex = -1;
                for (var index = 0; index < TrainingPreset.All.Count; index++)
                {
                    if (string.Equals(TrainingPreset.All[index].Name, settings.ProfileName, StringComparison.Ordinal))
                    {
                        presetIndex = index;
                        break;
                    }
                }
                _presetBox.SelectedIndex = presetIndex >= 0 ? presetIndex : TrainingPreset.All.Count;
                _modeBox.SelectedIndex = IndexFromMode(settings.Mode);
                _minimumMultiplierBox.Value = ClampDecimal((decimal)settings.MinimumMultiplier, _minimumMultiplierBox.Minimum, _minimumMultiplierBox.Maximum);
                _maximumMultiplierBox.Value = ClampDecimal((decimal)settings.MaximumMultiplier, _maximumMultiplierBox.Minimum, _maximumMultiplierBox.Maximum);
                _reciprocalBoundsBox.IsChecked = settings.UseReciprocalBounds;
                _maximumStepBox.Value = ClampDecimal((decimal)settings.MaximumStep, _maximumStepBox.Minimum, _maximumStepBox.Maximum);
                _returnStrengthBox.Value = ClampDecimal((decimal)settings.ReturnStrength, _returnStrengthBox.Minimum, _returnStrengthBox.Maximum);
                _gaussianMeanBox.Value = ClampDecimal((decimal)settings.GaussianMean, _gaussianMeanBox.Minimum, _gaussianMeanBox.Maximum);
                _gaussianSigmaBox.Value = ClampDecimal((decimal)settings.GaussianSigma, _gaussianSigmaBox.Minimum, _gaussianSigmaBox.Maximum);
                _intervalModeBox.SelectedIndex = settings.IntervalMode == IntervalMode.Fixed ? 1 : 0;
                _minimumIntervalBox.Value = ClampDecimal((decimal)settings.MinimumIntervalSeconds, _minimumIntervalBox.Minimum, _minimumIntervalBox.Maximum);
                _maximumIntervalBox.Value = ClampDecimal((decimal)settings.MaximumIntervalSeconds, _maximumIntervalBox.Minimum, _maximumIntervalBox.Maximum);
                _durationMinutesBox.Value = ClampDecimal(settings.SessionDurationSeconds / 60m, _durationMinutesBox.Minimum, _durationMinutesBox.Maximum);
                _recalibrationMinutesBox.Value = ClampDecimal(settings.RecalibrationDurationSeconds / 60m, _recalibrationMinutesBox.Minimum, _recalibrationMinutesBox.Maximum);
                _useSeedBox.IsChecked = settings.UseSeed;
                _seedBox.Value = ClampDecimal(settings.Seed, _seedBox.Minimum, _seedBox.Maximum);
                _loggingBox.IsChecked = settings.LoggingEnabled;
            }
            finally
            {
                _updatingControls = false;
            }
            UpdateChartConfiguration();
            UpdateModeAvailability();
            UpdateIntervalAvailability();
            UpdateOptionalVisibility();
            ValidateSettingsPreview();
        }

        private AppSettings ReadSettingsFromControls(bool requireValidInputs = true)
        {
            if (requireValidInputs) EnsureVisibleNumbersAreValid();
            var settings = _settings.Clone();
            settings.Language = _languageBox == null ? UiText.Current : UiText.LanguageFromIndex(_languageBox.SelectedIndex);
            settings.Theme = _themeBox == null ? _settings.Theme : UiText.ThemeFromIndex(_themeBox.SelectedIndex);
            settings.Dpi = (int)_dpiBox.Value;
            settings.BaseSensitivity = (double)_baseSensitivityBox.Value;
            settings.ProfileName = _presetBox.SelectedIndex >= 0 && _presetBox.SelectedIndex < TrainingPreset.All.Count
                ? TrainingPreset.All[_presetBox.SelectedIndex].Name : "Custom";
            settings.Mode = ModeFromIndex(_modeBox.SelectedIndex);
            settings.MinimumMultiplier = (double)_minimumMultiplierBox.Value;
            settings.MaximumMultiplier = (double)_maximumMultiplierBox.Value;
            settings.UseReciprocalBounds = _reciprocalBoundsBox.IsChecked;
            settings.MaximumStep = (double)_maximumStepBox.Value;
            settings.ReturnStrength = (double)_returnStrengthBox.Value;
            settings.GaussianMean = (double)_gaussianMeanBox.Value;
            settings.GaussianSigma = (double)_gaussianSigmaBox.Value;
            settings.IntervalMode = _intervalModeBox.SelectedIndex == 1 ? IntervalMode.Fixed : IntervalMode.Random;
            settings.MinimumIntervalSeconds = (double)_minimumIntervalBox.Value;
            settings.MaximumIntervalSeconds = settings.IntervalMode == IntervalMode.Fixed
                ? settings.MinimumIntervalSeconds : (double)_maximumIntervalBox.Value;
            settings.SessionDurationSeconds = (int)_durationMinutesBox.Value * 60;
            settings.RecalibrationDurationSeconds = (int)_recalibrationMinutesBox.Value * 60;
            settings.UseSeed = _useSeedBox.IsChecked;
            settings.Seed = decimal.ToInt32(_seedBox.Value);
            settings.LoggingEnabled = _loggingBox.IsChecked;
            return settings;
        }

        private void LanguageBoxOnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (_updatingControls || _rebuildingInterface || _languageBox == null) return;
            var selected = UiText.LanguageFromIndex(_languageBox.SelectedIndex);
            if (selected == UiText.Current) return;
            if (!_settingsValid)
            {
                _updatingControls = true;
                _languageBox.SelectedIndex = UiText.IndexOfLanguage(UiText.Current);
                _updatingControls = false;
                MessageBox.Show(this, UiText.T("Сначала исправь ошибку в настройках.", "Fix the settings error first."),
                    UiText.T("Настройки некорректны", "Invalid settings"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Dispatcher.BeginInvoke(new Action(() => RebuildForLanguage(selected, null, true)));
        }

        private void ThemeBoxOnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (_updatingControls || _rebuildingInterface || _themeBox == null) return;
            var selected = UiText.ThemeFromIndex(_themeBox.SelectedIndex);
            if (selected == _settings.Theme) return;
            if (!_settingsValid)
            {
                _updatingControls = true;
                _themeBox.SelectedIndex = UiText.IndexOfTheme(_settings.Theme);
                _updatingControls = false;
                MessageBox.Show(this, UiText.T("Сначала исправь ошибку в настройках.", "Fix the settings error first."),
                    UiText.T("Настройки некорректны", "Invalid settings"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppSettings replacement;
                try { replacement = ReadSettingsFromControls(); }
                catch { replacement = _settings.Clone(); }
                replacement.Theme = selected;
                RebuildForLanguage(replacement.Language, replacement, true);
            }));
        }

        private void RebuildForLanguage(AppLanguage language, AppSettings replacement, bool save)
        {
            if (_rebuildingInterface || !IsEditableState()) return;
            _rebuildingInterface = true;
            _uiTimer.Stop();
            try
            {
                var settings = replacement;
                if (settings == null)
                {
                    try { settings = ReadSettingsFromControls(); }
                    catch { settings = _settings.Clone(); }
                }
                settings.Language = language;
                _settings = settings;
                UiText.SetLanguage(language);
                UiPalette.Select(settings.Theme);
                Title = "Sensitivity Randomizer 1.2.0 RC2";
                Background = BackgroundBrush;
                Foreground = TextPrimaryBrush;
                Content = null;
                BuildInterface();
                ApplySettingsToControls(_settings);
                if (_lastReadback != null)
                {
                    UpdateDriverReadbackDisplay(_lastReadback);
                    if (_lastReadback.AccelerationDetected) BlockForSpeedAcceleration(_lastReadback, false);
                    else if (_rawAccelReady) SetStatusVisual(EngineState.Off, UiText.T(
                        "Готово: constant gain подтверждён, multiplier = 1.000.",
                        "Ready: constant gain verified, multiplier = 1.000."));
                }
                else
                {
                    SetStatusVisual(EngineState.Off, UiText.T("Raw Accel ещё не проверен.", "Raw Accel has not been checked yet."));
                }
                UpdateEffectiveValues();
                UpdateSessionClock();
                UpdateControlAvailability();
                if (save) _settings.Save(_settingsPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.T("Не удалось обновить интерфейс", "Theme change failed"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _uiTimer.Start();
                _rebuildingInterface = false;
            }
        }

        private void ImportConfiguration()
        {
            if (!IsEditableState()) return;
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = UiText.T("Импорт конфигурации", "Import configuration"),
                    Filter = UiText.T("Конфигурация JSON (*.json)|*.json|Все файлы (*.*)|*.*", "JSON configuration (*.json)|*.json|All files (*.*)|*.*"),
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) != true) return;
                var imported = ConfigurationExchange.Import(dialog.FileName);
                imported.Language = UiText.Current;
                imported.Theme = _settings.Theme;
                var summary = string.Format(CultureInfo.InvariantCulture,
                    UiText.T(
                        "Профиль: {0}\nРежим: {1}\nДиапазон: {2:0.000}-{3:0.000}\nИнтервал: {4:0.00}-{5:0.00} с\n\nПрименить? Raw Accel не изменится до Start.",
                        "Preset: {0}\nMode: {1}\nRange: {2:0.000}-{3:0.000}\nInterval: {4:0.00}-{5:0.00} s\n\nApply? Raw Accel is unchanged until Start."),
                    imported.ProfileName, ModeDisplayName(imported.Mode), imported.MinimumMultiplier, imported.MaximumMultiplier,
                    imported.MinimumIntervalSeconds,
                    imported.IntervalMode == IntervalMode.Fixed ? imported.MinimumIntervalSeconds : imported.MaximumIntervalSeconds);
                if (MessageBox.Show(this, summary, UiText.T("Проверка импорта", "Review import"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                _settings = imported;
                ApplySettingsToControls(imported);
                _settings.Save(_settingsPath);
                MessageBox.Show(this, UiText.T("Конфигурация импортирована и проверена.", "Configuration imported and validated."),
                    UiText.T("Импорт завершён", "Import complete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.T("Импорт не выполнен", "Import failed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportConfiguration()
        {
            if (!IsEditableState()) return;
            try
            {
                var settings = ReadSettingsFromControls();
                settings.Validate();
                var dialog = new SaveFileDialog
                {
                    Title = UiText.T("Экспорт конфигурации", "Export configuration"),
                    Filter = UiText.T("Конфигурация JSON (*.json)|*.json", "JSON configuration (*.json)|*.json"),
                    DefaultExt = ".json",
                    AddExtension = true,
                    FileName = "sensitivity-profile.json",
                    OverwritePrompt = true
                };
                if (dialog.ShowDialog(this) != true) return;
                ConfigurationExchange.Export(settings, dialog.FileName);
                _settings = settings;
                _settings.Save(_settingsPath);
                MessageBox.Show(this, UiText.T(
                        "Конфигурация экспортирована. Она не содержит логов или данных о системе.",
                        "Configuration exported. It contains no logs or system data."),
                    UiText.T("Экспорт завершён", "Export complete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.T("Экспорт не выполнен", "Export failed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterGlobalHotKeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            var toggle = RegisterHotKey(_windowHandle, HotKeyToggle, ModNoRepeat, VkF8);
            var panic = RegisterHotKey(_windowHandle, HotKeyPanic, ModNoRepeat, VkF9);
            var pause = RegisterHotKey(_windowHandle, HotKeyPause, ModNoRepeat, VkF10);
            if (!toggle || !panic || !pause)
            {
                _hotkeyLabel.Text = UiText.T(
                    "WARNING: одна или несколько глобальных клавиш заняты. Кнопки GUI работают.",
                    "WARNING: one or more global hotkeys are in use. GUI buttons still work.");
                _hotkeyLabel.Foreground = WarningBrush;
            }
        }

        private void UnregisterGlobalHotKeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            UnregisterHotKey(_windowHandle, HotKeyToggle);
            UnregisterHotKey(_windowHandle, HotKeyPanic);
            UnregisterHotKey(_windowHandle, HotKeyPause);
        }

        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmHotKey)
            {
                var id = wParam.ToInt32();
                Dispatcher.BeginInvoke(new Action(async () => await HandleHotKeyAsync(id)));
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OpenLogsFolder()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SensitivityRandomizer", "logs");
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.T("Не удалось открыть папку", "Could not open folder"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool ArmGuard()
        {
            _safetyResetRequired = true;
            if (!_guardAvailable) return true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_guardArmPath));
                File.WriteAllText(_guardArmPath, DateTime.UtcNow.ToString("O"));
                return true;
            }
            catch
            {
                _safetyResetRequired = false;
                return false;
            }
        }

        private void DisarmGuard()
        {
            _safetyResetRequired = false;
            if (!_guardAvailable) return;
            try { if (File.Exists(_guardArmPath)) File.Delete(_guardArmPath); }
            catch { }
        }

        private void SafeUi(Action action)
        {
            if (_disposed || Dispatcher.HasShutdownStarted) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(action);
                return;
            }
            action();
        }

        private static RandomizerMode ModeFromIndex(int index)
        {
            switch (index)
            {
                case 0: return RandomizerMode.BalancedCoverage;
                case 2: return RandomizerMode.Gaussian;
                case 3: return RandomizerMode.Uniform;
                default: return RandomizerMode.RandomWalk;
            }
        }

        private static int IndexFromMode(RandomizerMode mode)
        {
            switch (mode)
            {
                case RandomizerMode.BalancedCoverage: return 0;
                case RandomizerMode.Gaussian: return 2;
                case RandomizerMode.Uniform: return 3;
                default: return 1;
            }
        }

        private static string ModeDisplayName(RandomizerMode mode)
        {
            switch (mode)
            {
                case RandomizerMode.BalancedCoverage: return "Balanced Coverage";
                case RandomizerMode.Gaussian: return UiText.T("Центрированный Gaussian", "Centered Gaussian");
                case RandomizerMode.Uniform: return UiText.T("Линейный Uniform", "Linear Uniform");
                default: return "Smooth Random Walk";
            }
        }

        private static string StatusText(EngineState state)
        {
            switch (state)
            {
                case EngineState.Starting: return UiText.T("ЗАПУСК", "STARTING");
                case EngineState.Running: return UiText.T("РАБОТАЕТ", "RUNNING");
                case EngineState.Paused: return UiText.T("ПАУЗА", "PAUSED");
                case EngineState.Resetting: return UiText.T("СБРОС", "RESETTING");
                case EngineState.Recalibrating: return "RECAL";
                case EngineState.Completed: return UiText.T("ГОТОВО", "DONE");
                case EngineState.Error: return UiText.T("ОШИБКА", "ERROR");
                default: return UiText.T("ВЫКЛ", "OFF");
            }
        }

        private static Brush StatusBrush(EngineState state)
        {
            switch (state)
            {
                case EngineState.Running: return AccentBrush;
                case EngineState.Paused:
                case EngineState.Starting:
                case EngineState.Resetting:
                case EngineState.Recalibrating: return WarningBrush;
                case EngineState.Error: return DangerBrush;
                case EngineState.Completed: return BlueBrush;
                default: return TextSecondaryBrush;
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

        private static decimal ClampDecimal(decimal value, decimal minimum, decimal maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
    }
}
