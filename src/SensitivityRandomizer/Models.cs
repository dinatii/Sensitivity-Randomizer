using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SensitivityRandomizer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RandomizerMode
    {
        RandomWalk,
        Gaussian,
        Uniform,
        BalancedCoverage
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum IntervalMode
    {
        Random,
        Fixed
    }

    public enum EngineState
    {
        Off,
        Starting,
        Running,
        Paused,
        Resetting,
        Recalibrating,
        Completed,
        Error
    }

    public enum SessionPhase
    {
        Randomized,
        Recalibration
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AppTheme
    {
        Dark,
        Light,
        Pastel
    }

    public sealed class AppSettings
    {
        public const int CurrentSettingsVersion = 7;
        public const double MinimumTechnicalIntervalSeconds = 1.25;
        public const double MinimumAllowedMultiplier = 0.10;
        public const double MaximumAllowedMultiplier = 10.00;
        public const double ReciprocalBoundsTolerance = 0.00002;

        public int SettingsVersion { get; set; }
        public AppLanguage Language { get; set; }
        public AppTheme Theme { get; set; }
        public int Dpi { get; set; }
        public double BaseSensitivity { get; set; }

        // Reads pre-1.2 settings without writing the retired property name back.
        [JsonProperty("C" + "sSensitivity")]
        private double LegacyBaseSensitivity
        {
            set
            {
                if (BaseSensitivity <= 0 && value > 0)
                    BaseSensitivity = value;
            }
        }
        public string ProfileName { get; set; }
        public RandomizerMode Mode { get; set; }
        public double MinimumMultiplier { get; set; }
        public double MaximumMultiplier { get; set; }
        public bool UseReciprocalBounds { get; set; }
        public double MaximumStep { get; set; }
        public double ReturnStrength { get; set; }
        public double GaussianMean { get; set; }
        public double GaussianSigma { get; set; }
        public IntervalMode IntervalMode { get; set; }
        public double MinimumIntervalSeconds { get; set; }
        public double MaximumIntervalSeconds { get; set; }
        public int SessionDurationSeconds { get; set; }
        public int RecalibrationDurationSeconds { get; set; }
        public bool UseSeed { get; set; }
        public int Seed { get; set; }
        public bool LoggingEnabled { get; set; }
        public int ChartWindowSeconds { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                SettingsVersion = CurrentSettingsVersion,
                Language = UiText.SystemLanguage,
                Theme = AppTheme.Dark,
                Dpi = 500,
                BaseSensitivity = 1.6,
                ProfileName = TrainingPreset.FullRangeName,
                Mode = RandomizerMode.BalancedCoverage,
                MinimumMultiplier = 0.50,
                MaximumMultiplier = 2.00,
                UseReciprocalBounds = true,
                MaximumStep = 0.25,
                ReturnStrength = 0.03,
                GaussianMean = 1.00,
                GaussianSigma = 0.30,
                IntervalMode = IntervalMode.Random,
                MinimumIntervalSeconds = 2.0,
                MaximumIntervalSeconds = 5.0,
                SessionDurationSeconds = 20 * 60,
                RecalibrationDurationSeconds = 10 * 60,
                UseSeed = false,
                Seed = 12345,
                LoggingEnabled = true,
                ChartWindowSeconds = 120
            };
        }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        public void Validate()
        {
            if (Dpi < 50 || Dpi > 32000)
                throw new SettingsValidationException(UiText.T(
                    "DPI должен быть в диапазоне 50-32000.",
                    "DPI must be between 50 and 32000."));

            if (!IsFinite(BaseSensitivity) || BaseSensitivity <= 0 || BaseSensitivity > 100)
                throw new SettingsValidationException(UiText.T(
                    "Базовая чувствительность должна быть больше 0 и не больше 100.",
                    "Base sensitivity must be greater than 0 and no greater than 100."));

            if (!IsFinite(MinimumMultiplier) || !IsFinite(MaximumMultiplier) ||
                MinimumMultiplier < MinimumAllowedMultiplier || MaximumMultiplier > MaximumAllowedMultiplier)
            {
                throw new SettingsValidationException(string.Format(
                    CultureInfo.InvariantCulture,
                    UiText.T(
                        "Multiplier должен оставаться в защитном диапазоне {0:0.00}-{1:0.00}.",
                        "Multiplier must stay within the safety range {0:0.00}-{1:0.00}."),
                    MinimumAllowedMultiplier,
                    MaximumAllowedMultiplier));
            }

            if (MinimumMultiplier >= MaximumMultiplier)
                throw new SettingsValidationException(UiText.T(
                    "Минимальный multiplier должен быть меньше максимального.",
                    "Minimum multiplier must be lower than maximum multiplier."));

            if (Mode == RandomizerMode.BalancedCoverage &&
                (MinimumMultiplier >= 1.0 || MaximumMultiplier <= 1.0))
            {
                throw new SettingsValidationException(UiText.T(
                    "Balanced Coverage требует диапазон, содержащий 1.000 между границами.",
                    "Balanced Coverage requires a range with 1.000 strictly between its bounds."));
            }

            if (Mode == RandomizerMode.BalancedCoverage && UseReciprocalBounds &&
                !ReciprocalRange.AreSymmetric(MinimumMultiplier, MaximumMultiplier))
            {
                throw new SettingsValidationException(UiText.T(
                    "При включённой мультипликативной симметрии должно выполняться min × max = 1. Измени одну границу в GUI или отключи связь.",
                    "With multiplicative symmetry enabled, min × max must equal 1. Change either bound in the UI or disable linking."));
            }

            if (Mode == RandomizerMode.RandomWalk &&
                (!IsFinite(MaximumStep) || MaximumStep <= 0 || MaximumStep > MaximumMultiplier - MinimumMultiplier))
            {
                throw new SettingsValidationException(UiText.T(
                    "Максимальный шаг должен быть больше 0 и не шире выбранного диапазона.",
                    "Maximum step must be greater than 0 and no wider than the selected range."));
            }

            if (Mode == RandomizerMode.RandomWalk &&
                (!IsFinite(ReturnStrength) || ReturnStrength < 0 || ReturnStrength > 1))
            {
                throw new SettingsValidationException(UiText.T(
                    "Сила возврата должна быть в диапазоне 0-1.",
                    "Return strength must be between 0 and 1."));
            }

            if (Mode == RandomizerMode.Gaussian &&
                (!IsFinite(GaussianMean) || GaussianMean <= MinimumMultiplier || GaussianMean >= MaximumMultiplier))
            {
                throw new SettingsValidationException(UiText.T(
                    "Среднее Gaussian должно находиться строго внутри диапазона multiplier.",
                    "Gaussian mean must be strictly inside the multiplier range."));
            }

            if (Mode == RandomizerMode.Gaussian &&
                (!IsFinite(GaussianSigma) || GaussianSigma <= 0 || GaussianSigma > MaximumMultiplier - MinimumMultiplier))
            {
                throw new SettingsValidationException(UiText.T(
                    "Gaussian sigma должна быть больше 0 и не шире выбранного диапазона.",
                    "Gaussian sigma must be greater than 0 and no wider than the multiplier range."));
            }

            if (!IsFinite(MinimumIntervalSeconds) || !IsFinite(MaximumIntervalSeconds) ||
                MinimumIntervalSeconds < MinimumTechnicalIntervalSeconds)
            {
                throw new SettingsValidationException(string.Format(
                    CultureInfo.InvariantCulture,
                    UiText.T(
                        "Интервал не может быть меньше {0:0.00} с: Raw Accel намеренно задерживает каждое применение на 1 секунду.",
                        "Interval cannot be shorter than {0:0.00} s because Raw Accel intentionally delays each apply by 1 second."),
                    MinimumTechnicalIntervalSeconds));
            }

            if (IntervalMode == IntervalMode.Random &&
                (MaximumIntervalSeconds < MinimumIntervalSeconds || MaximumIntervalSeconds > 3600))
            {
                throw new SettingsValidationException(UiText.T(
                    "Максимальный интервал должен быть не меньше минимального и не больше 3600 с.",
                    "Maximum interval must not be shorter than minimum interval or exceed 3600 s."));
            }

            if (IntervalMode == IntervalMode.Fixed && Math.Abs(MaximumIntervalSeconds - MinimumIntervalSeconds) > 0.000001)
                MaximumIntervalSeconds = MinimumIntervalSeconds;

            if (SessionDurationSeconds < 0 || SessionDurationSeconds > 24 * 60 * 60)
                throw new SettingsValidationException(UiText.T(
                    "Длительность randomized phase должна быть от 0 (без лимита) до 24 часов.",
                    "Randomized phase duration must be between 0 (unlimited) and 24 hours."));

            if (RecalibrationDurationSeconds < 0 || RecalibrationDurationSeconds > 24 * 60 * 60)
                throw new SettingsValidationException(UiText.T(
                    "Длительность recalibration должна быть от 0 до 24 часов.",
                    "Recalibration duration must be between 0 and 24 hours."));

            if (SessionDurationSeconds == 0 && RecalibrationDurationSeconds > 0)
                throw new SettingsValidationException(UiText.T(
                    "Recalibration недоступна для бесконечной randomized phase.",
                    "Recalibration is unavailable when the randomized phase is unlimited."));

            if (ChartWindowSeconds < 30 || ChartWindowSeconds > 600)
                throw new SettingsValidationException(UiText.T(
                    "Окно графика должно быть в диапазоне 30-600 секунд.",
                    "Chart window must be between 30 and 600 seconds."));
        }

        public static AppSettings Load(string path, out string warning)
        {
            warning = null;
            if (!File.Exists(path))
                return CreateDefault();

            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                warning = UiText.T(
                    "Не удалось загрузить сохранённые настройки. Использованы безопасные значения. Причина: ",
                    "Saved settings could not be loaded. Safe defaults are being used. Reason: ") + ex.Message;
                return CreateDefault();
            }
        }

        public static AppSettings FromJson(string json)
        {
            var settings = JsonConvert.DeserializeObject<AppSettings>(json);
            if (settings == null)
                throw new JsonException(UiText.T("Пустой JSON.", "Empty JSON."));
            settings.UpgradeAndValidate();
            return settings;
        }

        public void UpgradeAndValidate()
        {
            if (SettingsVersion > CurrentSettingsVersion)
            {
                throw new SettingsValidationException(UiText.T(
                    "Конфигурация создана более новой версией программы.",
                    "This configuration was created by a newer app version."));
            }
            if (SettingsVersion < 2 && IsUntouchedLegacyStandard(this))
                TrainingPreset.FullRange.ApplyTo(this);
            if (SettingsVersion < 4 && Mode == RandomizerMode.Uniform &&
                MinimumMultiplier < 1.0 && MaximumMultiplier > 1.0)
            {
                // RC4 changes the recommended behavior from linear Uniform (whose
                // mean is the interval midpoint) to a centered Gaussian. Migrate
                // existing Uniform sessions so custom 0.50-2.00-style ranges do not
                // silently retain the upward bias reported by users.
                Mode = RandomizerMode.Gaussian;
                GaussianMean = 1.0;
            }
            if (SettingsVersion < 5 && Mode == RandomizerMode.Gaussian &&
                MinimumMultiplier < 1.0 && MaximumMultiplier > 1.0 &&
                NearlyEqual(GaussianMean, 1.0))
            {
                // RC5 makes equal logarithmic exposure the recommended behavior.
                // This also carries forward RC3 Uniform settings that RC4 first
                // migrated to a centered Gaussian.
                Mode = RandomizerMode.BalancedCoverage;
            }
            if (SettingsVersion < 6)
            {
                // Older configs did not have an explicit linking preference.
                // Enable it only when their stored bounds are already reciprocal,
                // so custom asymmetric ranges are never silently changed.
                UseReciprocalBounds = Mode == RandomizerMode.BalancedCoverage &&
                    ReciprocalRange.AreSymmetric(MinimumMultiplier, MaximumMultiplier, 0.0000001);
            }

            if (SettingsVersion < 7)
                Theme = AppTheme.Dark;

            if (!Enum.IsDefined(typeof(AppLanguage), Language))
                Language = UiText.SystemLanguage;
            if (!Enum.IsDefined(typeof(AppTheme), Theme))
                Theme = AppTheme.Dark;
            SettingsVersion = CurrentSettingsVersion;
            Validate();
        }

        public void PrepareForSave()
        {
            SettingsVersion = CurrentSettingsVersion;
            if (!Enum.IsDefined(typeof(AppLanguage), Language))
                Language = AppLanguage.English;
            if (!Enum.IsDefined(typeof(AppTheme), Theme))
                Theme = AppTheme.Dark;
            Validate();
        }

        public void Save(string path)
        {
            PrepareForSave();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Copy(temporaryPath, path, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch { }
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsUntouchedLegacyStandard(AppSettings settings)
        {
            return string.Equals(settings.ProfileName, "C" + "S Standard", StringComparison.Ordinal) &&
                   settings.Mode == RandomizerMode.RandomWalk &&
                   NearlyEqual(settings.MinimumMultiplier, 0.80) &&
                   NearlyEqual(settings.MaximumMultiplier, 1.20) &&
                   NearlyEqual(settings.MaximumStep, 0.05) &&
                   NearlyEqual(settings.ReturnStrength, 0.10) &&
                   NearlyEqual(settings.GaussianMean, 1.00) &&
                   NearlyEqual(settings.GaussianSigma, 0.08) &&
                   settings.IntervalMode == IntervalMode.Random &&
                   NearlyEqual(settings.MinimumIntervalSeconds, 2.0) &&
                   NearlyEqual(settings.MaximumIntervalSeconds, 5.0);
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) < 0.0000001;
        }
    }

    public sealed class SettingsValidationException : Exception
    {
        public SettingsValidationException(string message) : base(message) { }
    }

    public sealed class TrainingPreset
    {
        public const string FullRangeName = "Full Range 0.50-2.00";

        public string Name { get; private set; }
        public RandomizerMode Mode { get; private set; }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public double Step { get; private set; }
        public double ReturnStrength { get; private set; }
        public double Mean { get; private set; }
        public double Sigma { get; private set; }
        public double IntervalMinimum { get; private set; }
        public double IntervalMaximum { get; private set; }

        public TrainingPreset(
            string name,
            RandomizerMode mode,
            double minimum,
            double maximum,
            double step,
            double returnStrength,
            double mean,
            double sigma,
            double intervalMinimum,
            double intervalMaximum)
        {
            Name = name;
            Mode = mode;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
            ReturnStrength = returnStrength;
            Mean = mean;
            Sigma = sigma;
            IntervalMinimum = Math.Max(intervalMinimum, AppSettings.MinimumTechnicalIntervalSeconds);
            IntervalMaximum = intervalMaximum;
        }

        public void ApplyTo(AppSettings settings)
        {
            settings.ProfileName = Name;
            settings.Mode = Mode;
            settings.MinimumMultiplier = Minimum;
            settings.MaximumMultiplier = Maximum;
            settings.UseReciprocalBounds = Mode == RandomizerMode.BalancedCoverage &&
                ReciprocalRange.AreSymmetric(Minimum, Maximum);
            settings.MaximumStep = Step;
            settings.ReturnStrength = ReturnStrength;
            settings.GaussianMean = Mean;
            settings.GaussianSigma = Sigma;
            settings.IntervalMode = IntervalMode.Random;
            settings.MinimumIntervalSeconds = IntervalMinimum;
            settings.MaximumIntervalSeconds = IntervalMaximum;
        }

        public static TrainingPreset FullRange { get; } =
            new TrainingPreset(FullRangeName, RandomizerMode.BalancedCoverage, 0.50, 2.00, 0.25, 0.03, 1.00, 0.30, 2.0, 5.0);

        public static IReadOnlyList<TrainingPreset> All { get; } = new List<TrainingPreset>
        {
            FullRange,
            new TrainingPreset("Mild", RandomizerMode.Gaussian, 0.90, 1.10, 0.03, 0.10, 1.00, 0.04, 3.0, 6.0),
            new TrainingPreset("Standard", RandomizerMode.RandomWalk, 0.80, 1.20, 0.05, 0.10, 1.00, 0.08, 2.0, 5.0),
            new TrainingPreset("Wide", RandomizerMode.RandomWalk, 0.65, 1.35, 0.08, 0.10, 1.00, 0.12, 2.0, 5.0),
            new TrainingPreset("Extreme", RandomizerMode.Uniform, 0.50, 1.50, 0.10, 0.00, 1.00, 0.18, AppSettings.MinimumTechnicalIntervalSeconds, 4.0)
        };
    }

    public static class ReciprocalRange
    {
        public static double Counterpart(double value, int decimalPlaces)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (decimalPlaces < 0 || decimalPlaces > 9)
                throw new ArgumentOutOfRangeException(nameof(decimalPlaces));

            return Math.Round(1.0 / value, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        public static bool AreSymmetric(
            double minimum,
            double maximum,
            double tolerance = AppSettings.ReciprocalBoundsTolerance)
        {
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) ||
                double.IsNaN(maximum) || double.IsInfinity(maximum) ||
                minimum <= 0 || maximum <= 0)
                return false;

            return Math.Abs(minimum * maximum - 1.0) <= tolerance;
        }
    }

    public sealed class MultiplierReadback
    {
        public string DriverVersion { get; set; }
        public double RequestedMultiplier { get; set; }
        public double RepresentativeMultiplier { get; set; }
        public IReadOnlyList<string> ProfileNames { get; set; }
        public IReadOnlyList<double> ProfileMultipliers { get; set; }
        public IReadOnlyList<string> AccelerationModes { get; set; }
        public bool AccelerationDetected { get; set; }
        public bool DpiNormalizationDetected { get; set; }
        public bool DirectionalRatioDetected { get; set; }
        public bool DisabledDeviceDetected { get; set; }

        public bool AllProfilesMatch(double target, double tolerance)
        {
            if (ProfileMultipliers == null || ProfileMultipliers.Count == 0)
                return false;

            for (var i = 0; i < ProfileMultipliers.Count; i++)
            {
                if (Math.Abs(ProfileMultipliers[i] - target) > tolerance)
                    return false;
            }
            return true;
        }

        public string DescribeProfiles()
        {
            if (ProfileMultipliers == null || ProfileMultipliers.Count == 0)
                return UiText.T("нет профилей", "no profiles");

            var values = new List<string>();
            for (var i = 0; i < ProfileMultipliers.Count; i++)
            {
                var name = ProfileNames != null && i < ProfileNames.Count ? ProfileNames[i] : "profile " + (i + 1);
                values.Add(name + " = " + ProfileMultipliers[i].ToString("0.000", CultureInfo.InvariantCulture));
            }
            return string.Join("; ", values);
        }

        public string BuildCompatibilityWarning()
        {
            var warnings = new List<string>();
            if (DpiNormalizationDetected)
            {
                warnings.Add(
                    UiText.T(
                        "Обнаружена Raw Accel DPI normalization. Относительная рандомизация работает, но показанный effective eDPI не описывает итоговый физический gain. Для прямого соответствия DPI × base sensitivity × multiplier отключи normalization (DPI = 0 в Device Menu).",
                        "Raw Accel DPI normalization is enabled. Relative randomization still works, but displayed effective eDPI no longer describes the final physical gain. Set DPI = 0 in Device Menu for a literal DPI × base sensitivity × multiplier result."));
            }

            if (DirectionalRatioDetected)
            {
                warnings.Add(
                    UiText.T(
                        "Обнаружены Y/X, L/R или U/D ratios, отличные от 1. Показанная effective sensitivity относится к базовой горизонтальной величине; по другим направлениям gain отличается.",
                        "Y/X, L/R or U/D ratios other than 1 were detected. Displayed effective sensitivity is the base horizontal value; gain differs in other directions."));
            }

            if (DisabledDeviceDetected)
            {
                warnings.Add(
                    UiText.T(
                        "В Raw Accel Device Menu обнаружен disabled driver path. Убедись, что Raw Accel не отключён именно для игровой мыши, иначе readback settings будет успешным, но её физический input не изменится.",
                        "A disabled driver path was detected in Raw Accel Device Menu. Make sure Raw Accel is not disabled for the gaming mouse, otherwise settings readback can succeed without changing its physical input."));
            }

            return string.Join("\n\n", warnings);
        }
    }

    public sealed class AppliedSampleEventArgs : EventArgs
    {
        public DateTime TimestampUtc { get; set; }
        public TimeSpan Elapsed { get; set; }
        public SessionPhase Phase { get; set; }
        public double RequestedMultiplier { get; set; }
        public double ActualMultiplier { get; set; }
        public double EffectiveSensitivity { get; set; }
        public double EffectiveEdpi { get; set; }
    }

    public sealed class EngineStatusEventArgs : EventArgs
    {
        public EngineState State { get; set; }
        public string Detail { get; set; }
    }

    public sealed class EngineSnapshot
    {
        public EngineState State { get; set; }
        public SessionPhase Phase { get; set; }
        public double CurrentMultiplier { get; set; }
        public TimeSpan PhaseElapsed { get; set; }
        public TimeSpan? PhaseRemaining { get; set; }
        public DateTime? NextChangeUtc { get; set; }
        public int ChangeCount { get; set; }
    }

    public sealed class SessionSummary
    {
        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public string CompletionReason { get; set; }
        public int Seed { get; set; }
        public TimeSpan RandomizedDuration { get; set; }
        public TimeSpan RecalibrationDuration { get; set; }
        public int ChangeCount { get; set; }
        public double MeanMultiplier { get; set; }
        public double GeometricMeanMultiplier { get; set; }
        public double TimeWeightedGeometricMeanMultiplier { get; set; }
        public double StandardDeviation { get; set; }
        public double MinimumMultiplier { get; set; }
        public double MaximumMultiplier { get; set; }
        public double MeanEffectiveSensitivity { get; set; }
        public double MeanEffectiveEdpi { get; set; }
        public double BelowBaseChangePercent { get; set; }
        public double AboveBaseChangePercent { get; set; }
        public double AtBaseChangePercent { get; set; }
        public double BelowBaseTimePercent { get; set; }
        public double AboveBaseTimePercent { get; set; }
        public double AtBaseTimePercent { get; set; }
        public string CsvPath { get; set; }
        public string SummaryPath { get; set; }
        public string LoggingWarning { get; set; }
    }

    public sealed class SessionCompletedEventArgs : EventArgs
    {
        public SessionSummary Summary { get; set; }
    }
}
