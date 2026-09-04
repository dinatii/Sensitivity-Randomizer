using System;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SensitivityRandomizer
{
    internal static class Program
    {
        private const string InstanceMutexName = "Local\\SensitivityRandomizer.8B7F346A";
        private const string ExpectedWrapperSha256 = "e17d540fb0bb5e47ba7c8e01aff39f1570bfb7ef673a1ddc0bccd2a26f9efb78";
        private const string ExpectedNewtonsoftSha256 = "b624949df8b0e3a6153fdfb730a7c6f4990b6592ee0d922e1788433d276610f3";
        private static readonly object StartupLogSync = new object();
        private static string _startupLogPath;
        private static bool _fatalUiErrorReported;

        [STAThread]
        private static void Main()
        {
            UiText.UseSystemLanguage();
            InitializeStartupLog();
            LogStartup("Process entry. Base directory: " + AppDomain.CurrentDomain.BaseDirectory);

            try
            {
                RunApplication();
            }
            catch (Exception ex)
            {
                if (_fatalUiErrorReported)
                    LogStartup("Runtime UI exception reached the application boundary: " + ex);
                else
                    ReportFatalStartup(ex);
                Environment.ExitCode = 1;
            }
        }

        // Keep the minimal entry point independent from Raw Accel types. If the
        // mixed-mode wrapper or one of its native dependencies cannot load, the
        // failure is now caught by Main instead of terminating a WinExe silently.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication()
        {
            ValidateRuntimeAndReleaseFiles();

            bool createdNew;
            using (var mutex = new Mutex(true, InstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    LogStartup("Another instance owns the application mutex.");
                    System.Windows.MessageBox.Show(
                        UiText.T("Sensitivity Randomizer уже запущен.", "Sensitivity Randomizer is already running."),
                        UiText.T("Один экземпляр", "Single instance"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var appDataRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SensitivityRandomizer");
                Directory.CreateDirectory(appDataRoot);
                LogStartup("Local application data: " + appDataRoot);
                var settingsPath = Path.Combine(appDataRoot, "config.json");
                ImportLegacySettingsIfNeeded(settingsPath);
                var guardArmPath = Path.Combine(appDataRoot, "reset-guard-" + Process.GetCurrentProcess().Id + ".armed");

                string loadWarning;
                var settings = AppSettings.Load(settingsPath, out loadWarning);
                UiText.SetLanguage(settings.Language);
                LogStartup("Settings loaded.");
                var controller = new RawAccelController();
                var engine = new RandomizerEngine(controller);

                var guardAvailable = StartResetGuard(guardArmPath);
                LogStartup("ResetGuard start result: " + guardAvailable.ToString(CultureInfo.InvariantCulture));

                var application = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose
                };

                application.DispatcherUnhandledException += (sender, args) =>
                {
                    _fatalUiErrorReported = true;
                    WriteCrashLog(appDataRoot, args.Exception);
                    LogStartup("WPF dispatcher exception: " + args.Exception);
                    System.Windows.MessageBox.Show(
                        UiText.T("Программа аварийно завершится. ", "The app is about to terminate unexpectedly. ") +
                        (guardAvailable
                            ? UiText.T(
                                "Если randomizer был активен, ResetGuard отдельно вернёт Raw Accel multiplier к 1.000.",
                                "If the randomizer was active, ResetGuard will independently restore Raw Accel multiplier to 1.000.")
                            : UiText.T(
                                "Приложение попытается выполнить штатный reset, но отдельный ResetGuard недоступен.",
                                "The app will attempt a normal reset, but the independent ResetGuard is unavailable.")) +
                        "\n\n" + args.Exception.Message,
                        UiText.T("Необработанная ошибка", "Unhandled error"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    args.Handled = false;
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var exception = args.ExceptionObject as Exception ?? new Exception("Unknown fatal error");
                    WriteCrashLog(appDataRoot, exception);
                    LogStartup("Unhandled AppDomain exception: " + exception);
                };

                var mainWindow = new MainWindow(controller, engine, settings, settingsPath, loadWarning, guardArmPath, guardAvailable);
                application.MainWindow = mainWindow;
                LogStartup("MainWindow constructed. Entering WPF dispatcher loop.");
                application.Run(mainWindow);
                LogStartup("WPF dispatcher loop returned normally.");
                GC.KeepAlive(mutex);
            }
        }

        internal static void LogStartup(string message)
        {
            if (string.IsNullOrEmpty(_startupLogPath)) return;
            try
            {
                lock (StartupLogSync)
                {
                    File.AppendAllText(
                        _startupLogPath,
                        DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never become another startup failure.
            }
        }

        private static void InitializeStartupLog()
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SensitivityRandomizer");
                Directory.CreateDirectory(root);
                _startupLogPath = Path.Combine(root, "startup.log");
            }
            catch
            {
                try
                {
                    _startupLogPath = Path.Combine(Path.GetTempPath(), "SensitivityRandomizer-startup.log");
                }
                catch
                {
                    _startupLogPath = null;
                }
            }

            LogStartup(string.Empty);
            LogStartup("===== Sensitivity Randomizer 1.2.0-rc2 startup =====");
        }

        private static void ValidateRuntimeAndReleaseFiles()
        {
            if (!Environment.Is64BitOperatingSystem || !Environment.Is64BitProcess)
                throw new PlatformNotSupportedException(UiText.T(
                    "Нужны 64-битные Windows и x64-процесс.",
                    "64-bit Windows and an x64 process are required."));

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var wrapperPath = Path.Combine(baseDirectory, "wrapper.dll");
            var newtonsoftPath = Path.Combine(baseDirectory, "Newtonsoft.Json.dll");
            var guardPath = Path.Combine(baseDirectory, "SensitivityResetGuard.exe");

            var missing = new StringBuilder();
            AppendMissingFile(missing, wrapperPath);
            AppendMissingFile(missing, newtonsoftPath);
            AppendMissingFile(missing, guardPath);
            if (missing.Length > 0)
            {
                throw new FileNotFoundException(
                    UiText.T("Не найдены обязательные файлы:", "Required files are missing:") + Environment.NewLine + missing +
                    Environment.NewLine + UiText.T(
                        "Сначала полностью извлеки папку win-x64 из ZIP. Не запускай EXE внутри архива.",
                        "Fully extract the win-x64 folder from the ZIP first. Do not run the EXE from inside the archive."));
            }

            VerifySha256(wrapperPath, ExpectedWrapperSha256);
            VerifySha256(newtonsoftPath, ExpectedNewtonsoftSha256);
            VerifyNativeRuntime("VCRUNTIME140.dll");
            VerifyNativeRuntime("MSVCP140.dll");

            LogStartup(
                "Dependency preflight passed. OS=" + Environment.OSVersion +
                ", CLR=" + Environment.Version +
                ", process64=" + Environment.Is64BitProcess.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendMissingFile(StringBuilder builder, string path)
        {
            if (!File.Exists(path))
                builder.Append("  - ").Append(Path.GetFileName(path)).AppendLine();
        }

        private static void VerifySha256(string path, string expected)
        {
            string actual;
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                actual = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    UiText.T("Контрольная сумма ", "The checksum of ") + Path.GetFileName(path) +
                    UiText.T(
                        " не совпадает. Файл повреждён или был заменён. Извлеки чистый архив заново.",
                        " does not match. The file is damaged or has been replaced. Extract a clean archive again."));
            }
        }

        private static void VerifyNativeRuntime(string libraryName)
        {
            var module = LoadLibrary(libraryName);
            if (module == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException(
                    UiText.T("Не найдена системная библиотека ", "System library not found: ") + libraryName + " (Win32 error " + error + "). " +
                    UiText.T(
                        "Установи Microsoft Visual C++ Redistributable 2015-2022 x64 и запусти приложение снова.",
                        "Install Microsoft Visual C++ Redistributable 2015-2022 x64 and start the app again."));
            }
            FreeLibrary(module);
        }

        private static void ReportFatalStartup(Exception exception)
        {
            LogStartup("FATAL STARTUP ERROR: " + exception);
            var root = exception;
            while (root.InnerException != null) root = root.InnerException;

            var message =
                UiText.T(
                    "Sensitivity Randomizer не смог открыть главное окно.",
                    "Sensitivity Randomizer could not open its main window.") + Environment.NewLine + Environment.NewLine +
                root.GetType().Name + ": " + root.Message + Environment.NewLine + Environment.NewLine +
                UiText.T("Проверь:", "Check:") + Environment.NewLine +
                UiText.T("1. Вся папка win-x64 полностью извлечена из ZIP.", "1. The entire win-x64 folder was extracted from the ZIP.") + Environment.NewLine +
                UiText.T("2. Установлен Microsoft Visual C++ Redistributable 2015-2022 x64.", "2. Microsoft Visual C++ Redistributable 2015-2022 x64 is installed.") + Environment.NewLine +
                UiText.T("3. wrapper.dll и Newtonsoft.Json.dll лежат рядом с EXE.", "3. wrapper.dll and Newtonsoft.Json.dll are next to the EXE.") + Environment.NewLine + Environment.NewLine +
                UiText.T("Диагностический лог:", "Diagnostic log:") + Environment.NewLine +
                (_startupLogPath ?? UiText.T("создать лог не удалось", "the log could not be created"));

            try
            {
                System.Windows.MessageBox.Show(
                    message,
                    UiText.T("Ошибка запуска", "Startup error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                MessageBoxW(IntPtr.Zero, message, UiText.T("Ошибка запуска", "Startup error"), 0x00000010);
            }
        }

        private static bool StartResetGuard(string armPath)
        {
            try
            {
                // A previous hard kill can leave a marker behind. The marker name
                // contains a PID, but Windows can eventually reuse that PID, so a
                // new process must always begin explicitly disarmed.
                if (File.Exists(armPath))
                    File.Delete(armPath);

                var guardPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SensitivityResetGuard.exe");
                if (!File.Exists(guardPath)) return false;

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = guardPath,
                    Arguments = Process.GetCurrentProcess().Id + " \"" + armPath.Replace("\"", string.Empty) + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
                if (process == null) return false;
                process.Dispose();
                return true;
            }
            catch
            {
                // The main form still performs its own reset on Stop/Close/exception.
                return false;
            }
        }

        private static void WriteCrashLog(string root, Exception exception)
        {
            try
            {
                Directory.CreateDirectory(root);
                File.AppendAllText(
                    Path.Combine(root, "crash.log"),
                    DateTime.UtcNow.ToString("O") + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }

        private static void ImportLegacySettingsIfNeeded(string settingsPath)
        {
            if (File.Exists(settingsPath)) return;
            try
            {
                var legacyFolderName = string.Concat(
                    "C",
                    char.ConvertFromUtf32(83),
                    char.ConvertFromUtf32(50),
                    "SensitivityRandomizer");
                var legacyRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    legacyFolderName);
                var legacyPath = Path.Combine(legacyRoot, "config.json");
                if (!File.Exists(legacyPath)) return;
                File.Copy(legacyPath, settingsPath, false);
                LogStartup("Imported settings from the pre-1.2 application data folder.");
            }
            catch (Exception ex)
            {
                LogStartup("Legacy settings import skipped: " + ex.Message);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libraryName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
    }
}
