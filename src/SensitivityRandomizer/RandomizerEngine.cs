using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SensitivityRandomizer
{
    public sealed class RandomizerEngine
    {
        private readonly IMultiplierController _controller;
        private readonly object _sync = new object();
        private CancellationTokenSource _runCancellation;
        private Task _runTask;
        private AppSettings _activeSettings;
        private EngineState _state = EngineState.Off;
        private SessionPhase _phase = SessionPhase.Randomized;
        private bool _pauseRequested;
        private DateTime? _pauseStartedUtc;
        private double _currentMultiplier = 1.0;
        private ActiveClock _phaseClock = new ActiveClock();
        private DateTime? _nextChangeUtc;
        private int _changeCount;

        public event EventHandler<EngineStatusEventArgs> StatusChanged;
        public event EventHandler<AppliedSampleEventArgs> SampleApplied;
        public event EventHandler<SessionCompletedEventArgs> SessionCompleted;

        public RandomizerEngine(IMultiplierController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public EngineState State
        {
            get { lock (_sync) return _state; }
        }

        public void Start(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();

            lock (_sync)
            {
                if (_runTask != null && !_runTask.IsCompleted)
                    throw new InvalidOperationException(UiText.T("Randomizer уже запущен.", "Randomizer is already running."));

                _activeSettings = settings.Clone();
                _runCancellation = new CancellationTokenSource();
                _pauseRequested = false;
                _pauseStartedUtc = null;
                _currentMultiplier = 1.0;
                _changeCount = 0;
                _nextChangeUtc = null;
                _phase = SessionPhase.Randomized;
                _phaseClock = new ActiveClock();
                _state = EngineState.Starting;
                _runTask = Task.Run(() => RunSessionAsync(_activeSettings, _runCancellation.Token));
            }

            var handler = StatusChanged;
            if (handler != null)
                handler(this, new EngineStatusEventArgs
                {
                    State = EngineState.Starting,
                    Detail = UiText.T("Запуск сессии...", "Starting session...")
                });
        }

        public async Task StopAsync()
        {
            Task runningTask;
            bool wasRunning;
            lock (_sync)
            {
                runningTask = _runTask;
                wasRunning = runningTask != null && !runningTask.IsCompleted;
                if (wasRunning)
                    _runCancellation.Cancel();
            }

            if (wasRunning)
            {
                await runningTask.ConfigureAwait(false);
                return;
            }

            await ResetOnlyAsync().ConfigureAwait(false);
        }

        public async Task PanicAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }

        public async Task ResetOnlyAsync()
        {
            SetStatus(EngineState.Resetting, UiText.T("Возврат multiplier к 1.000...", "Restoring multiplier to 1.000..."));
            try
            {
                var readback = await _controller.ResetAsync(CancellationToken.None).ConfigureAwait(false);
                SetCurrentMultiplier(readback.RepresentativeMultiplier);
                lock (_sync)
                {
                    _phase = SessionPhase.Randomized;
                    _phaseClock = new ActiveClock();
                    _nextChangeUtc = null;
                    _changeCount = 0;
                    _pauseRequested = false;
                    _pauseStartedUtc = null;
                }
                SetStatus(EngineState.Off, UiText.T("Multiplier подтверждён: 1.000", "Multiplier verified: 1.000"));
            }
            catch (Exception ex)
            {
                SetStatus(EngineState.Error, "RESET FAILED: " + ex.Message);
                throw;
            }
        }

        public void TogglePause()
        {
            lock (_sync)
            {
                if (_state != EngineState.Running && _state != EngineState.Paused)
                    return;

                _pauseRequested = !_pauseRequested;
                if (_pauseRequested)
                {
                    _pauseStartedUtc = DateTime.UtcNow;
                    _phaseClock.Pause();
                    _state = EngineState.Paused;
                }
                else
                {
                    if (_pauseStartedUtc.HasValue && _nextChangeUtc.HasValue)
                        _nextChangeUtc = _nextChangeUtc.Value + (DateTime.UtcNow - _pauseStartedUtc.Value);
                    _pauseStartedUtc = null;
                    _phaseClock.Resume();
                    _state = _phase == SessionPhase.Recalibration ? EngineState.Recalibrating : EngineState.Running;
                }
            }

            RaiseStatus(_pauseRequested
                ? UiText.T("Текущий multiplier заморожен.", "Current multiplier is frozen.")
                : UiText.T("Randomization продолжена.", "Randomization resumed."));
        }

        public EngineSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                var elapsed = _phaseClock.Elapsed;
                TimeSpan? remaining = null;
                if (_activeSettings != null)
                {
                    var totalSeconds = _phase == SessionPhase.Randomized
                        ? _activeSettings.SessionDurationSeconds
                        : _activeSettings.RecalibrationDurationSeconds;
                    if (totalSeconds > 0)
                        remaining = TimeSpan.FromSeconds(Math.Max(0, totalSeconds - elapsed.TotalSeconds));
                }

                return new EngineSnapshot
                {
                    State = _state,
                    Phase = _phase,
                    CurrentMultiplier = _currentMultiplier,
                    PhaseElapsed = elapsed,
                    PhaseRemaining = remaining,
                    NextChangeUtc = _nextChangeUtc,
                    ChangeCount = _changeCount
                };
            }
        }

        private async Task RunSessionAsync(AppSettings settings, CancellationToken token)
        {
            var actualSeed = settings.UseSeed ? settings.Seed : Pcg32Random.CreateUnseededSessionSeed();
            var generator = new RandomizerGenerator(settings, new Pcg32Random(unchecked((ulong)(long)actualSeed)), 1.0);
            string loggingWarning;
            var logger = SessionLogger.TryCreate(settings, actualSeed, out loggingWarning);
            var fallbackAccumulator = new SessionAccumulator();
            var randomizedElapsed = TimeSpan.Zero;
            var recalibrationElapsed = TimeSpan.Zero;
            var completionReason = "Stopped";
            var resetConfirmed = false;
            var hadError = false;
            string sessionError = null;

            try
            {
                SetStatus(EngineState.Starting, string.IsNullOrEmpty(loggingWarning)
                    ? UiText.T("Подготовка первой sensitivity...", "Preparing the first sensitivity...")
                    : loggingWarning);

                lock (_sync)
                {
                    _phase = SessionPhase.Randomized;
                    _phaseClock = new ActiveClock();
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    await WaitWhilePausedAsync(token).ConfigureAwait(false);

                    if (ShouldFinishPhase(settings.SessionDurationSeconds, _controller.WriteDelay))
                    {
                        completionReason = "Completed";
                        break;
                    }

                    var requested = generator.NextMultiplier();
                    SetStatus(EngineState.Running,
                        UiText.T("Применение ", "Applying ") + requested.ToString("0.000") + "...");
                    var readback = await _controller.ApplyAsync(requested, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    if (!_phaseClock.IsStarted)
                    {
                        _phaseClock.Start();
                        lock (_sync)
                        {
                            if (_pauseRequested) _phaseClock.Pause();
                        }
                    }

                    var sample = CreateSample(settings, SessionPhase.Randomized, requested, readback.RepresentativeMultiplier);
                    RecordSample(sample, logger, fallbackAccumulator);
                    resetConfirmed = false;

                    var interval = generator.NextIntervalSeconds();
                    lock (_sync)
                    {
                        _nextChangeUtc = DateTime.UtcNow.AddSeconds(interval);
                        if (_pauseRequested) _pauseStartedUtc = DateTime.UtcNow;
                    }

                    SetStatus(_pauseRequested ? EngineState.Paused : EngineState.Running,
                        UiText.T("Следующая смена примерно через ", "Next change in approximately ") +
                        interval.ToString("0.00") + UiText.T(" с.", " s."));

                    var preWriteWait = Math.Max(0, interval - _controller.WriteDelay.TotalSeconds);
                    if (settings.SessionDurationSeconds > 0)
                    {
                        var remainingBeforeReset = settings.SessionDurationSeconds - _phaseClock.Elapsed.TotalSeconds - _controller.WriteDelay.TotalSeconds;
                        preWriteWait = Math.Min(preWriteWait, Math.Max(0, remainingBeforeReset));
                    }

                    await WaitActiveSecondsAsync(preWriteWait, token).ConfigureAwait(false);
                }

                SetStatus(EngineState.Resetting, UiText.T(
                    "Randomized phase завершена. Возврат к 1.000...",
                    "Randomized phase complete. Restoring 1.000..."));
                var reset = await _controller.ResetAsync(CancellationToken.None).ConfigureAwait(false);
                // The previous randomized multiplier remains physically active
                // during Raw Accel's protected reset delay. Close its final
                // time-weighted statistics interval only after reset readback.
                randomizedElapsed = _phaseClock.Elapsed;
                SetCurrentMultiplier(reset.RepresentativeMultiplier);
                resetConfirmed = true;
                lock (_sync) _nextChangeUtc = null;

                if (settings.RecalibrationDurationSeconds > 0 && !token.IsCancellationRequested)
                {
                    lock (_sync)
                    {
                        _phase = SessionPhase.Recalibration;
                        _phaseClock = new ActiveClock();
                        _phaseClock.Start();
                    }
                    SetStatus(EngineState.Recalibrating, UiText.T(
                        "Recalibration на обычной sensitivity 1.000.",
                        "Recalibration at normal sensitivity 1.000."));
                    await WaitActiveSecondsAsync(settings.RecalibrationDurationSeconds, token).ConfigureAwait(false);
                    recalibrationElapsed = _phaseClock.Elapsed;
                }

                completionReason = token.IsCancellationRequested ? "Stopped" : "Completed";
            }
            catch (OperationCanceledException)
            {
                completionReason = "Stopped";
                randomizedElapsed = _phase == SessionPhase.Randomized ? _phaseClock.Elapsed : randomizedElapsed;
                recalibrationElapsed = _phase == SessionPhase.Recalibration ? _phaseClock.Elapsed : recalibrationElapsed;
            }
            catch (Exception ex)
            {
                completionReason = "Error";
                hadError = true;
                sessionError = ex.Message;
                randomizedElapsed = _phase == SessionPhase.Randomized ? _phaseClock.Elapsed : randomizedElapsed;
                recalibrationElapsed = _phase == SessionPhase.Recalibration ? _phaseClock.Elapsed : recalibrationElapsed;
                SetStatus(EngineState.Error, ex.Message);
            }
            finally
            {
                lock (_sync)
                {
                    _nextChangeUtc = null;
                    _phaseClock.Pause();
                    _pauseRequested = false;
                    _pauseStartedUtc = null;
                }

                if (!resetConfirmed)
                {
                    SetStatus(EngineState.Resetting, "Safety reset to 1.000...");
                    try
                    {
                        var reset = await ResetWithRetryAsync().ConfigureAwait(false);
                        SetCurrentMultiplier(reset.RepresentativeMultiplier);
                        resetConfirmed = true;
                    }
                    catch (Exception resetError)
                    {
                        hadError = true;
                        completionReason = "ResetError";
                        SetStatus(EngineState.Error,
                            UiText.T("КРИТИЧЕСКИ: не подтверждён reset к 1.000. ", "CRITICAL: reset to 1.000 was not confirmed. ") +
                            resetError.Message);
                    }
                }

                SessionSummary summary;
                try
                {
                    summary = logger != null
                        ? logger.Complete(completionReason, randomizedElapsed, recalibrationElapsed)
                        : BuildFallbackSummary(fallbackAccumulator, completionReason, actualSeed, randomizedElapsed, recalibrationElapsed, null, null);
                }
                catch (Exception loggingError)
                {
                    summary = BuildFallbackSummary(
                        fallbackAccumulator,
                        completionReason,
                        actualSeed,
                        randomizedElapsed,
                        recalibrationElapsed,
                        logger == null ? null : logger.CsvPath,
                        null);
                    RaiseStatus(UiText.T(
                        "Сессия завершена, но summary log не записан: ",
                        "Session finished, but the summary log could not be written: ") + loggingError.Message);
                }
                finally
                {
                    if (logger != null) logger.Dispose();
                }

                RaiseSessionCompleted(summary);

                if (!hadError)
                {
                    SetStatus(completionReason == "Completed" ? EngineState.Completed : EngineState.Off,
                        completionReason == "Completed"
                            ? UiText.T("Сессия завершена. Multiplier = 1.000.", "Session complete. Multiplier = 1.000.")
                            : UiText.T("Остановлено. Multiplier = 1.000.", "Stopped. Multiplier = 1.000."));
                    if (completionReason == "Completed")
                    {
                        await Task.Delay(800).ConfigureAwait(false);
                        SetStatus(EngineState.Off, UiText.T("Готово к новой сессии.", "Ready for a new session."));
                    }
                }
                else if (resetConfirmed)
                {
                    SetStatus(
                        EngineState.Error,
                        UiText.T(
                            "Сессия остановлена из-за ошибки, multiplier возвращён к 1.000. ",
                            "Session stopped because of an error; multiplier was restored to 1.000. ") +
                        (sessionError ?? UiText.T(
                            "Проверь активную конфигурацию Raw Accel.",
                            "Check the active Raw Accel configuration.")));
                }

                lock (_sync)
                {
                    if (_runCancellation != null)
                    {
                        _runCancellation.Dispose();
                        _runCancellation = null;
                    }
                }
            }
        }

        private bool ShouldFinishPhase(int durationSeconds, TimeSpan writeDelay)
        {
            if (durationSeconds <= 0 || !_phaseClock.IsStarted)
                return false;

            // Start the reset one driver-delay before the requested boundary so that
            // multiplier 1.000 becomes active close to the configured phase end.
            return _phaseClock.Elapsed.TotalSeconds >= durationSeconds - writeDelay.TotalSeconds;
        }

        private async Task<MultiplierReadback> ResetWithRetryAsync()
        {
            Exception last = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await _controller.ResetAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt == 0) await Task.Delay(200).ConfigureAwait(false);
                }
            }
            throw last ?? new InvalidOperationException(UiText.T("Reset не выполнен.", "Reset failed."));
        }

        private AppliedSampleEventArgs CreateSample(
            AppSettings settings,
            SessionPhase phase,
            double requested,
            double actual)
        {
            var sample = new AppliedSampleEventArgs
            {
                TimestampUtc = DateTime.UtcNow,
                Elapsed = _phaseClock.Elapsed,
                Phase = phase,
                RequestedMultiplier = requested,
                ActualMultiplier = actual,
                EffectiveSensitivity = SensitivityMath.EffectiveSensitivity(settings.BaseSensitivity, actual),
                EffectiveEdpi = SensitivityMath.EffectiveEdpi(settings.Dpi, settings.BaseSensitivity, actual)
            };

            lock (_sync)
            {
                _currentMultiplier = actual;
                if (phase == SessionPhase.Randomized) _changeCount++;
            }
            return sample;
        }

        private void RecordSample(
            AppliedSampleEventArgs sample,
            SessionLogger logger,
            SessionAccumulator fallbackAccumulator)
        {
            if (logger != null)
                logger.Write(sample);

            if (fallbackAccumulator != null && sample.Phase == SessionPhase.Randomized)
                fallbackAccumulator.Add(sample.ActualMultiplier, sample.EffectiveSensitivity, sample.EffectiveEdpi, sample.Elapsed);

            var handler = SampleApplied;
            if (handler != null) handler(this, sample);
        }

        private static SessionSummary BuildFallbackSummary(
            SessionAccumulator accumulator,
            string completionReason,
            int seed,
            TimeSpan randomizedElapsed,
            TimeSpan recalibrationElapsed,
            string csvPath,
            string summaryPath)
        {
            return accumulator.BuildSummary(
                DateTime.UtcNow - randomizedElapsed - recalibrationElapsed,
                DateTime.UtcNow,
                completionReason,
                seed,
                randomizedElapsed,
                recalibrationElapsed,
                csvPath,
                summaryPath);
        }

        private async Task WaitWhilePausedAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                bool paused;
                lock (_sync) paused = _pauseRequested;
                if (!paused) return;
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private async Task WaitActiveSecondsAsync(double seconds, CancellationToken token)
        {
            var remaining = Math.Max(0, seconds);
            var stopwatch = Stopwatch.StartNew();
            while (remaining > 0.0001)
            {
                token.ThrowIfCancellationRequested();
                bool paused;
                lock (_sync) paused = _pauseRequested;

                if (paused)
                {
                    stopwatch.Restart();
                    await Task.Delay(50, token).ConfigureAwait(false);
                    continue;
                }

                var delayMilliseconds = (int)Math.Max(1, Math.Min(50, remaining * 1000.0));
                await Task.Delay(delayMilliseconds, token).ConfigureAwait(false);
                remaining -= stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();
            }
        }

        private void SetCurrentMultiplier(double multiplier)
        {
            lock (_sync) _currentMultiplier = multiplier;
        }

        private void SetStatus(EngineState state, string detail)
        {
            lock (_sync)
            {
                _state = state;
                if (state == EngineState.Paused) _pauseRequested = true;
            }
            var handler = StatusChanged;
            if (handler != null) handler(this, new EngineStatusEventArgs { State = state, Detail = detail });
        }

        private void RaiseStatus(string detail)
        {
            EngineState state;
            lock (_sync) state = _state;
            var handler = StatusChanged;
            if (handler != null) handler(this, new EngineStatusEventArgs { State = state, Detail = detail });
        }

        private void RaiseSessionCompleted(SessionSummary summary)
        {
            var handler = SessionCompleted;
            if (handler != null) handler(this, new SessionCompletedEventArgs { Summary = summary });
        }

        private sealed class ActiveClock
        {
            private readonly object _clockSync = new object();
            private readonly Stopwatch _stopwatch = new Stopwatch();

            public bool IsStarted
            {
                get { lock (_clockSync) return _stopwatch.IsRunning || _stopwatch.Elapsed > TimeSpan.Zero; }
            }

            public TimeSpan Elapsed
            {
                get { lock (_clockSync) return _stopwatch.Elapsed; }
            }

            public void Start()
            {
                lock (_clockSync)
                {
                    _stopwatch.Reset();
                    _stopwatch.Start();
                }
            }

            public void Pause()
            {
                lock (_clockSync) _stopwatch.Stop();
            }

            public void Resume()
            {
                lock (_clockSync)
                {
                    if (_stopwatch.Elapsed > TimeSpan.Zero && !_stopwatch.IsRunning)
                        _stopwatch.Start();
                }
            }
        }
    }
}
