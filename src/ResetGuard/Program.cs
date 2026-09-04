using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SensitivityResetGuard
{
    internal static class Program
    {
        private const double NormalizedOutputDpi = 1000.0;

        private static void Main(string[] args)
        {
            if (args.Length != 2) return;
            int parentProcessId;
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out parentProcessId)) return;
            var armPath = args[1];

            try
            {
                var parent = Process.GetProcessById(parentProcessId);
                parent.WaitForExit();
                parent.Dispose();
            }
            catch
            {
                // If the parent disappeared before we opened it, reset immediately.
            }

            if (!File.Exists(armPath)) return;

            Thread.Sleep(100);
            Exception lastError = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    VersionHelper.ValidOrThrow();
                    var config = DriverConfig.GetActive();
                    if (config.profiles == null || config.profiles.Count == 0)
                        throw new InvalidOperationException("Raw Accel returned no profiles.");

                    for (var i = 0; i < config.profiles.Count; i++)
                    {
                        var profile = config.profiles[i];
                        profile.outputDPI = NormalizedOutputDpi;
                        config.SetProfileAt(i, profile);
                    }

                    var validationErrors = config.Errors();
                    if (!string.IsNullOrWhiteSpace(validationErrors))
                        throw new InvalidOperationException("Raw Accel rejected reset: " + validationErrors);

                    config.Activate();

                    var readback = DriverConfig.GetActive();
                    var valid = readback.profiles != null && readback.profiles.Count > 0;
                    for (var i = 0; valid && i < readback.profiles.Count; i++)
                    {
                        if (Math.Abs(readback.profiles[i].outputDPI / NormalizedOutputDpi - 1.0) > 0.0005)
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid)
                    {
                        try { File.Delete(armPath); }
                        catch { }
                        return;
                    }
                    throw new InvalidOperationException("Readback did not confirm multiplier 1.000.");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(250);
                }
            }

            WriteGuardLog(lastError);
        }

        private static void WriteGuardLog(Exception exception)
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SensitivityRandomizer");
                Directory.CreateDirectory(root);
                File.AppendAllText(
                    Path.Combine(root, "reset-guard.log"),
                    DateTime.UtcNow.ToString("O") + " reset failed: " + exception + Environment.NewLine);
            }
            catch { }
        }
    }
}
