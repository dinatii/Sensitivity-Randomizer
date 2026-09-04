using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SensitivityRandomizer
{
    public sealed class PortableConfiguration
    {
        public string Format { get; set; }
        public int FormatVersion { get; set; }
        public DateTime ExportedUtc { get; set; }
        public AppSettings Settings { get; set; }
    }

    public static class ConfigurationExchange
    {
        public const string FormatName = "SensitivityRandomizerConfig";
        public const int CurrentFormatVersion = 1;
        private const long MaximumImportBytes = 1024 * 1024;

        public static void Export(AppSettings settings, string path)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty.", nameof(path));

            var copy = settings.Clone();
            copy.PrepareForSave();
            var document = new PortableConfiguration
            {
                Format = FormatName,
                FormatVersion = CurrentFormatVersion,
                ExportedUtc = DateTime.UtcNow,
                Settings = copy
            };
            WriteAtomic(path, JsonConvert.SerializeObject(document, Formatting.Indented));
        }

        public static AppSettings Import(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is empty.", nameof(path));
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException(UiText.T("Файл конфигурации не найден.", "Configuration file was not found."), path);
            if (info.Length <= 0 || info.Length > MaximumImportBytes)
            {
                throw new InvalidDataException(UiText.T(
                    "Файл конфигурации пуст или превышает допустимый размер 1 МБ.",
                    "The configuration file is empty or exceeds the 1 MB limit."));
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(UiText.T(
                    "Файл не является корректным JSON.",
                    "The file is not valid JSON."), ex);
            }

            var settingsToken = root.GetValue("Settings", StringComparison.OrdinalIgnoreCase);
            if (settingsToken == null)
            {
                // Direct config.json files from earlier versions remain importable.
                return AppSettings.FromJson(json);
            }

            var format = (string)root.GetValue("Format", StringComparison.OrdinalIgnoreCase);
            var versionToken = root.GetValue("FormatVersion", StringComparison.OrdinalIgnoreCase);
            var version = versionToken == null ? 0 : versionToken.Value<int>();
            var compatibleFormat = string.Equals(format, FormatName, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(format) &&
                 format.EndsWith(FormatName, StringComparison.Ordinal));
            if (!compatibleFormat ||
                version < 1 || version > CurrentFormatVersion)
            {
                throw new InvalidDataException(UiText.T(
                    "Неподдерживаемый формат или версия конфигурации.",
                    "Unsupported configuration format or version."));
            }

            var settings = settingsToken.ToObject<AppSettings>();
            if (settings == null)
                throw new InvalidDataException(UiText.T("В конфигурации нет настроек.", "The configuration contains no settings."));
            settings.UpgradeAndValidate();
            return settings;
        }

        private static void WriteAtomic(string path, string contents)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temporaryPath = fullPath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
                File.Copy(temporaryPath, fullPath, true);
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
    }
}
