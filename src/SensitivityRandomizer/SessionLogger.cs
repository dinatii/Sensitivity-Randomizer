using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace SensitivityRandomizer
{
    public sealed class SessionLogger : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly int _seed;
        private readonly DateTime _startedUtc;
        private readonly StreamWriter _csvWriter;
        private readonly SessionAccumulator _accumulator = new SessionAccumulator();
        private bool _disposed;
        private Exception _writeFailure;

        public string CsvPath { get; private set; }
        public string SummaryPath { get; private set; }

        private SessionLogger(AppSettings settings, int seed, string logDirectory)
        {
            _settings = settings.Clone();
            _seed = seed;
            _startedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(logDirectory);

            var baseName = "session_" + _startedUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            CsvPath = Path.Combine(logDirectory, baseName + ".csv");
            SummaryPath = Path.Combine(logDirectory, baseName + ".json");

            _csvWriter = new StreamWriter(CsvPath, false, new System.Text.UTF8Encoding(true));
            _csvWriter.WriteLine("timestamp_local,timestamp_utc,elapsed_seconds,phase,requested_multiplier,actual_multiplier,effective_sensitivity,effective_edpi");
            _csvWriter.Flush();
        }

        public static SessionLogger TryCreate(AppSettings settings, int seed, out string warning)
        {
            warning = null;
            if (!settings.LoggingEnabled)
                return null;

            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SensitivityRandomizer",
                    "logs");
                return new SessionLogger(settings, seed, root);
            }
            catch (Exception ex)
            {
                warning = UiText.T("Логирование отключено: ", "Logging disabled: ") + ex.Message;
                return null;
            }
        }

        public void Write(AppliedSampleEventArgs sample)
        {
            if (sample.Phase == SessionPhase.Randomized)
                _accumulator.Add(sample.ActualMultiplier, sample.EffectiveSensitivity, sample.EffectiveEdpi, sample.Elapsed);

            if (_disposed || _writeFailure != null) return;

            try
            {
                var local = sample.TimestampUtc.ToLocalTime();
                _csvWriter.WriteLine(string.Join(",", new[]
                {
                    local.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture),
                    sample.TimestampUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                    sample.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                    sample.Phase.ToString(),
                    sample.RequestedMultiplier.ToString("0.000000", CultureInfo.InvariantCulture),
                    sample.ActualMultiplier.ToString("0.000000", CultureInfo.InvariantCulture),
                    sample.EffectiveSensitivity.ToString("0.000000", CultureInfo.InvariantCulture),
                    sample.EffectiveEdpi.ToString("0.000", CultureInfo.InvariantCulture)
                }));
                _csvWriter.Flush();
            }
            catch (Exception ex)
            {
                _writeFailure = ex;
            }
        }

        public SessionSummary Complete(
            string completionReason,
            TimeSpan randomizedDuration,
            TimeSpan recalibrationDuration)
        {
            var summary = _accumulator.BuildSummary(
                _startedUtc,
                DateTime.UtcNow,
                completionReason,
                _seed,
                randomizedDuration,
                recalibrationDuration,
                CsvPath,
                SummaryPath);

            if (_writeFailure != null)
            {
                summary.SummaryPath = null;
                summary.LoggingWarning = "CSV logging stopped early: " + _writeFailure.Message;
                return summary;
            }

            if (!_disposed)
            {
                try
                {
                    _csvWriter.Flush();
                    File.WriteAllText(SummaryPath, JsonConvert.SerializeObject(new
                    {
                        app = "Sensitivity Randomizer",
                        version = "1.2.0-rc2",
                        settings = _settings,
                        summary = summary
                    }, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    summary.SummaryPath = null;
                    summary.LoggingWarning = "JSON summary was not written: " + ex.Message;
                }
            }

            return summary;
        }

        public SessionSummary BuildUnsavedSummary(
            string completionReason,
            TimeSpan randomizedDuration,
            TimeSpan recalibrationDuration)
        {
            return _accumulator.BuildSummary(
                _startedUtc,
                DateTime.UtcNow,
                completionReason,
                _seed,
                randomizedDuration,
                recalibrationDuration,
                CsvPath,
                SummaryPath);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _csvWriter.Dispose();
        }
    }

    public sealed class SessionAccumulator
    {
        private int _count;
        private double _sumMultiplier;
        private double _sumLogMultiplier;
        private double _sumSquares;
        private double _sumSensitivity;
        private double _sumEdpi;
        private double _minimum = double.PositiveInfinity;
        private double _maximum = double.NegativeInfinity;
        private int _belowBaseCount;
        private int _aboveBaseCount;
        private int _atBaseCount;
        private bool _hasPendingSample;
        private TimeSpan _pendingElapsed;
        private double _pendingMultiplier;
        private double _weightedSeconds;
        private double _weightedLogMultiplier;
        private double _belowBaseSeconds;
        private double _aboveBaseSeconds;
        private double _atBaseSeconds;

        public void Add(double multiplier, double sensitivity, double edpi, TimeSpan elapsed)
        {
            ClosePendingSample(elapsed);
            _count++;
            _sumMultiplier += multiplier;
            _sumLogMultiplier += Math.Log(multiplier);
            _sumSquares += multiplier * multiplier;
            _sumSensitivity += sensitivity;
            _sumEdpi += edpi;
            _minimum = Math.Min(_minimum, multiplier);
            _maximum = Math.Max(_maximum, multiplier);
            if (multiplier < 1.0) _belowBaseCount++;
            else if (multiplier > 1.0) _aboveBaseCount++;
            else _atBaseCount++;

            _hasPendingSample = true;
            _pendingElapsed = elapsed;
            _pendingMultiplier = multiplier;
        }

        public SessionSummary BuildSummary(
            DateTime startedUtc,
            DateTime finishedUtc,
            string reason,
            int seed,
            TimeSpan randomizedDuration,
            TimeSpan recalibrationDuration,
            string csvPath,
            string summaryPath)
        {
            var mean = _count == 0 ? 1.0 : _sumMultiplier / _count;
            var geometricMean = _count == 0 ? 1.0 : Math.Exp(_sumLogMultiplier / _count);
            var variance = _count == 0 ? 0.0 : Math.Max(0.0, _sumSquares / _count - mean * mean);
            var weightedSeconds = _weightedSeconds;
            var weightedLogMultiplier = _weightedLogMultiplier;
            var belowBaseSeconds = _belowBaseSeconds;
            var aboveBaseSeconds = _aboveBaseSeconds;
            var atBaseSeconds = _atBaseSeconds;
            if (_hasPendingSample)
            {
                var finalSeconds = Math.Max(0.0, (randomizedDuration - _pendingElapsed).TotalSeconds);
                weightedSeconds += finalSeconds;
                weightedLogMultiplier += Math.Log(_pendingMultiplier) * finalSeconds;
                if (_pendingMultiplier < 1.0) belowBaseSeconds += finalSeconds;
                else if (_pendingMultiplier > 1.0) aboveBaseSeconds += finalSeconds;
                else atBaseSeconds += finalSeconds;
            }

            return new SessionSummary
            {
                StartedUtc = startedUtc,
                FinishedUtc = finishedUtc,
                CompletionReason = reason,
                Seed = seed,
                RandomizedDuration = randomizedDuration,
                RecalibrationDuration = recalibrationDuration,
                ChangeCount = _count,
                MeanMultiplier = mean,
                GeometricMeanMultiplier = geometricMean,
                TimeWeightedGeometricMeanMultiplier = weightedSeconds > 0
                    ? Math.Exp(weightedLogMultiplier / weightedSeconds)
                    : geometricMean,
                StandardDeviation = Math.Sqrt(variance),
                MinimumMultiplier = _count == 0 ? 1.0 : _minimum,
                MaximumMultiplier = _count == 0 ? 1.0 : _maximum,
                MeanEffectiveSensitivity = _count == 0 ? 0.0 : _sumSensitivity / _count,
                MeanEffectiveEdpi = _count == 0 ? 0.0 : _sumEdpi / _count,
                BelowBaseChangePercent = _count == 0 ? 0.0 : 100.0 * _belowBaseCount / _count,
                AboveBaseChangePercent = _count == 0 ? 0.0 : 100.0 * _aboveBaseCount / _count,
                AtBaseChangePercent = _count == 0 ? 0.0 : 100.0 * _atBaseCount / _count,
                BelowBaseTimePercent = weightedSeconds <= 0 ? 0.0 : 100.0 * belowBaseSeconds / weightedSeconds,
                AboveBaseTimePercent = weightedSeconds <= 0 ? 0.0 : 100.0 * aboveBaseSeconds / weightedSeconds,
                AtBaseTimePercent = weightedSeconds <= 0 ? 0.0 : 100.0 * atBaseSeconds / weightedSeconds,
                CsvPath = csvPath,
                SummaryPath = summaryPath
            };
        }

        private void ClosePendingSample(TimeSpan nextElapsed)
        {
            if (!_hasPendingSample) return;
            var seconds = Math.Max(0.0, (nextElapsed - _pendingElapsed).TotalSeconds);
            _weightedSeconds += seconds;
            _weightedLogMultiplier += Math.Log(_pendingMultiplier) * seconds;
            if (_pendingMultiplier < 1.0) _belowBaseSeconds += seconds;
            else if (_pendingMultiplier > 1.0) _aboveBaseSeconds += seconds;
            else _atBaseSeconds += seconds;
        }
    }
}
