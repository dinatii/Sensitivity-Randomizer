using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensitivityRandomizer
{
    /// <summary>
    /// Uses Raw Accel's own C++/CLI wrapper. Each write starts by reading the active
    /// driver configuration, changes Profile.outputDPI only, writes it back, and then
    /// reads the driver again for verification. Raw Accel defines 1000 output DPI as
    /// sensitivity multiplier 1.0.
    /// </summary>
    public sealed class RawAccelController : IMultiplierController
    {
        private const double NormalizedOutputDpi = 1000.0;
        private const double ReadbackTolerance = 0.0005;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);

        public TimeSpan WriteDelay
        {
            get { return TimeSpan.FromMilliseconds(DriverConfig.WriteDelayMs); }
        }

        public async Task<MultiplierReadback> InspectAsync(CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => InspectBlocking(), CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public Task<MultiplierReadback> ApplyAsync(double multiplier, CancellationToken cancellationToken)
        {
            return ApplyWithPolicyAsync(multiplier, false, cancellationToken);
        }

        public Task<MultiplierReadback> ResetAsync(CancellationToken cancellationToken)
        {
            // A safety reset must remain possible even if acceleration was enabled
            // externally. It changes outputDPI back to 1000 but never edits the curve.
            return ApplyWithPolicyAsync(1.0, true, cancellationToken);
        }

        private async Task<MultiplierReadback> ApplyWithPolicyAsync(
            double multiplier,
            bool allowAccelerationForSafetyReset,
            CancellationToken cancellationToken)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) ||
                multiplier < AppSettings.MinimumAllowedMultiplier || multiplier > AppSettings.MaximumAllowedMultiplier)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), UiText.T(
                    "Небезопасное значение multiplier.",
                    "Unsafe multiplier value."));
            }

            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Once DeviceIoControl has started, cancelling it would be unsafe. Raw Accel
                // deliberately blocks this call for roughly one second before applying.
                return await Task.Run(
                    () => ApplyBlocking(multiplier, allowAccelerationForSafetyReset),
                    CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private static MultiplierReadback InspectBlocking()
        {
            try
            {
                var driverVersion = VersionHelper.ValidOrThrow();
                var config = DriverConfig.GetActive();
                return Describe(config, driverVersion.ToString(), double.NaN);
            }
            catch (Exception ex)
            {
                throw WrapException(UiText.T(
                    "Не удалось прочитать активную конфигурацию Raw Accel",
                    "Could not read the active Raw Accel configuration"), ex);
            }
        }

        private static MultiplierReadback ApplyBlocking(double multiplier, bool allowAccelerationForSafetyReset)
        {
            try
            {
                var driverVersion = VersionHelper.ValidOrThrow();
                var config = DriverConfig.GetActive();

                if (config.profiles == null || config.profiles.Count == 0)
                    throw new InvalidOperationException(UiText.T(
                        "Raw Accel не вернул ни одного профиля.",
                        "Raw Accel returned no profiles."));

                var beforeWrite = Describe(config, driverVersion.ToString(), multiplier);
                if (!allowAccelerationForSafetyReset)
                    EnsureConstantGain(beforeWrite);

                for (var i = 0; i < config.profiles.Count; i++)
                {
                    var profile = config.profiles[i];
                    profile.outputDPI = NormalizedOutputDpi * multiplier;
                    config.SetProfileAt(i, profile);
                }

                var validationErrors = config.Errors();
                if (!string.IsNullOrWhiteSpace(validationErrors))
                    throw new InvalidOperationException(UiText.T(
                        "Raw Accel отклонил конфигурацию: ",
                        "Raw Accel rejected the configuration: ") + validationErrors);

                config.Activate();

                var actualConfig = DriverConfig.GetActive();
                var readback = Describe(actualConfig, driverVersion.ToString(), multiplier);
                if (!allowAccelerationForSafetyReset)
                    EnsureConstantGain(readback);
                if (!readback.AllProfilesMatch(multiplier, ReadbackTolerance))
                {
                    throw new RawAccelOperationException(
                        UiText.T(
                            "Raw Accel не подтвердил применённое значение. Запрошено ",
                            "Raw Accel did not confirm the applied value. Requested ") +
                        multiplier.ToString("0.000") + UiText.T(", прочитано: ", ", read back: ") + readback.DescribeProfiles());
                }

                return readback;
            }
            catch (RawAccelOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw WrapException(UiText.T(
                    "Не удалось применить multiplier через Raw Accel",
                    "Could not apply multiplier through Raw Accel"), ex);
            }
        }

        private static void EnsureConstantGain(MultiplierReadback readback)
        {
            if (!readback.AccelerationDetected)
                return;

            throw new RawAccelOperationException(
                UiText.T("Speed-based acceleration обнаружена: ", "Speed-based acceleration detected: ") +
                string.Join("; ", readback.AccelerationModes ?? new string[0]) +
                UiText.T(
                    ". Randomization заблокирована. Выставь Accel Type = Off/noaccel во всех профилях Raw Accel и повтори проверку.",
                    ". Randomization is blocked. Set Accel Type = Off/noaccel in every Raw Accel profile and verify again."));
        }

        private static MultiplierReadback Describe(DriverConfig config, string version, double requested)
        {
            var names = new List<string>();
            var multipliers = new List<double>();
            var accelerationModes = new List<string>();
            var accelerationDetected = false;
            var directionalRatioDetected = false;
            if (config.profiles != null)
            {
                for (var i = 0; i < config.profiles.Count; i++)
                {
                    var profile = config.profiles[i];
                    var profileName = string.IsNullOrWhiteSpace(profile.name) ? "profile " + (i + 1) : profile.name;
                    names.Add(profileName);
                    multipliers.Add(profile.outputDPI / NormalizedOutputDpi);

                    var xMode = profile.argsX.mode;
                    var yMode = profile.argsY.mode;
                    var usesYCurve = !profile.inputSpeedArgs.combineMagnitudes;
                    if (xMode != AccelMode.noaccel || (usesYCurve && yMode != AccelMode.noaccel))
                    {
                        accelerationDetected = true;
                        accelerationModes.Add(profileName + ": X=" + xMode + (usesYCurve ? ", Y=" + yMode : string.Empty));
                    }

                    if (Math.Abs(profile.yxOutputDPIRatio - 1.0) > 0.000001 ||
                        Math.Abs(profile.lrOutputDPIRatio - 1.0) > 0.000001 ||
                        Math.Abs(profile.udOutputDPIRatio - 1.0) > 0.000001)
                    {
                        directionalRatioDetected = true;
                    }
                }
            }

            var dpiNormalizationDetected = config.defaultDeviceConfig.dpi > 0;
            var disabledDeviceDetected = config.defaultDeviceConfig.disable;
            if (config.devices != null)
            {
                for (var i = 0; i < config.devices.Count; i++)
                {
                    dpiNormalizationDetected |= config.devices[i].config.dpi > 0;
                    disabledDeviceDetected |= config.devices[i].config.disable;
                }
            }

            var representative = multipliers.Count == 0 ? double.NaN : multipliers[0];
            return new MultiplierReadback
            {
                DriverVersion = version,
                RequestedMultiplier = requested,
                RepresentativeMultiplier = representative,
                ProfileNames = names,
                ProfileMultipliers = multipliers,
                AccelerationModes = accelerationModes,
                AccelerationDetected = accelerationDetected,
                DpiNormalizationDetected = dpiNormalizationDetected,
                DirectionalRatioDetected = directionalRatioDetected,
                DisabledDeviceDetected = disabledDeviceDetected
            };
        }

        private static RawAccelOperationException WrapException(string prefix, Exception exception)
        {
            var root = exception;
            while (root.InnerException != null)
                root = root.InnerException;

            return new RawAccelOperationException(prefix + ": " + root.Message, exception);
        }
    }

    public sealed class RawAccelOperationException : Exception
    {
        public RawAccelOperationException(string message) : base(message) { }
        public RawAccelOperationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
