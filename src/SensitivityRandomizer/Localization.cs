using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SensitivityRandomizer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AppLanguage
    {
        Russian,
        English,
        German,
        Spanish,
        Polish,
        Ukrainian
    }

    internal static class UiText
    {
        private static readonly AppLanguage[] Languages =
        {
            AppLanguage.Russian,
            AppLanguage.English,
            AppLanguage.German,
            AppLanguage.Spanish,
            AppLanguage.Polish,
            AppLanguage.Ukrainian
        };

        private static readonly Dictionary<AppLanguage, Dictionary<string, string>> Translations =
            new Dictionary<AppLanguage, Dictionary<string, string>>
            {
                { AppLanguage.German, new Dictionary<string, string>(StringComparer.Ordinal) },
                { AppLanguage.Spanish, new Dictionary<string, string>(StringComparer.Ordinal) },
                { AppLanguage.Polish, new Dictionary<string, string>(StringComparer.Ordinal) },
                { AppLanguage.Ukrainian, new Dictionary<string, string>(StringComparer.Ordinal) }
            };

        private static AppLanguage _current = DetectSystemLanguage();

        static UiText()
        {
            Add("Settings", "Einstellungen", "Ajustes", "Ustawienia", "Налаштування");
            Add("Import", "Import", "Importar", "Importuj", "Імпорт");
            Add("Export", "Export", "Exportar", "Eksportuj", "Експорт");
            Add("Import a validated JSON configuration", "Geprüfte JSON-Konfiguration importieren", "Importar una configuración JSON validada", "Importuj sprawdzoną konfigurację JSON", "Імпортувати перевірену JSON-конфігурацію");
            Add("Export current settings to JSON", "Aktuelle Einstellungen als JSON exportieren", "Exportar los ajustes actuales a JSON", "Eksportuj bieżące ustawienia do JSON", "Експортувати поточні налаштування в JSON");
            Add("Interface language", "Sprache der Oberfläche", "Idioma de la interfaz", "Język interfejsu", "Мова інтерфейсу");
            Add("Interface theme", "Oberflächendesign", "Tema de la interfaz", "Motyw interfejsu", "Тема інтерфейсу");
            Add("constant gain via Raw Accel", "konstante Verstärkung über Raw Accel", "ganancia constante mediante Raw Accel", "stałe wzmocnienie przez Raw Accel", "постійний gain через Raw Accel");
            Add("Author", "Autor", "Autor", "Autor", "Автор");
            Add("Open the author's website", "Website des Autors öffnen", "Abrir el sitio web del autor", "Otwórz stronę autora", "Відкрити сайт автора");
            Add("Could not open the author's website in a browser.", "Die Website des Autors konnte nicht im Browser geöffnet werden.", "No se pudo abrir el sitio web del autor en el navegador.", "Nie udało się otworzyć strony autora w przeglądarce.", "Не вдалося відкрити сайт автора у браузері.");
            Add("Could not open link", "Link konnte nicht geöffnet werden", "No se pudo abrir el enlace", "Nie udało się otworzyć linku", "Не вдалося відкрити посилання");
            Add("Dark", "Dunkel", "Oscuro", "Ciemny", "Темна");
            Add("Light", "Hell", "Claro", "Jasny", "Світла");
            Add("Pastel", "Pastell", "Pastel", "Pastelowy", "Пастельна");
            Add("STATUS", "STATUS", "ESTADO", "STAN", "СТАН");
            Add("OFF", "AUS", "APAGADO", "WYŁ.", "ВИМК");
            Add("STARTING", "STARTET", "INICIANDO", "URUCHAMIANIE", "ЗАПУСК");
            Add("RUNNING", "AKTIV", "ACTIVO", "AKTYWNY", "ПРАЦЮЄ");
            Add("PAUSED", "PAUSIERT", "PAUSADO", "PAUZA", "ПАУЗА");
            Add("RESETTING", "RESET", "RESTABLECIENDO", "RESET", "СКИДАННЯ");
            Add("DONE", "FERTIG", "LISTO", "GOTOWE", "ГОТОВО");
            Add("ERROR", "FEHLER", "ERROR", "BŁĄD", "ПОМИЛКА");
            Add("CURRENT MULTIPLIER", "AKTUELLER MULTIPLIKATOR", "MULTIPLICADOR ACTUAL", "BIEŻĄCY MNOŻNIK", "ПОТОЧНИЙ МНОЖНИК");
            Add("EFFECTIVE SENSITIVITY", "EFFEKTIVE EMPFINDLICHKEIT", "SENSIBILIDAD EFECTIVA", "EFEKTYWNA CZUŁOŚĆ", "ЕФЕКТИВНА ЧУТЛИВІСТЬ");
            Add("Essential", "Wichtig", "Esencial", "Podstawowe", "Основне");
            Add("Optional", "Optional", "Opcional", "Opcjonalne", "Додатково");
            Add("Guide", "Hilfe", "Guía", "Pomoc", "Довідка");
            Add("REQUIRED", "ERFORDERLICH", "OBLIGATORIO", "WYMAGANE", "ОБОВ'ЯЗКОВО");
            Add("OPTIONAL", "OPTIONAL", "OPCIONAL", "OPCJONALNE", "НЕОБОВ'ЯЗКОВО");
            Add("Only settings that affect multiplier values and change timing are shown here.", "Hier stehen nur Einstellungen für Multiplikatorwerte und Wechselzeiten.", "Aquí solo aparecen los ajustes que afectan a los valores y al momento del cambio.", "Tutaj są tylko ustawienia wpływające na mnożnik i czas zmian.", "Тут показано лише параметри, що впливають на множник і час зміни.");
            Add("Display calculations, duration, repeatability and local logs.", "Anzeigeberechnungen, Dauer, Wiederholbarkeit und lokale Logs.", "Cálculos de pantalla, duración, repetibilidad y registros locales.", "Obliczenia, czas trwania, powtarzalność i lokalne logi.", "Розрахунки, тривалість, повторюваність і локальні журнали.");
            Add("Preset", "Profil", "Preajuste", "Profil", "Профіль");
            Add("Custom", "Benutzerdefiniert", "Personalizado", "Własny", "Власний");
            Add("A starting set. Every setting remains editable.", "Ein Startprofil. Jede Einstellung bleibt änderbar.", "Un punto de partida. Todos los ajustes siguen siendo editables.", "Zestaw startowy. Każde ustawienie można zmienić.", "Стартовий набір. Кожен параметр можна змінити.");
            Add("Mode", "Modus", "Modo", "Tryb", "Режим");
            Add("Mouse movement speed is never used by any mode.", "Die Mausgeschwindigkeit wird in keinem Modus verwendet.", "Ningún modo utiliza la velocidad del ratón.", "Żaden tryb nie używa prędkości ruchu myszy.", "Жоден режим не використовує швидкість руху миші.");
            Add("Multiplier range", "Multiplikatorbereich", "Rango del multiplicador", "Zakres mnożnika", "Діапазон множника");
            Add("Constant values from 0.10000 to 10.00000.", "Konstante Werte von 0,10000 bis 10,00000.", "Valores constantes de 0,10000 a 10,00000.", "Stałe wartości od 0,10000 do 10,00000.", "Постійні значення від 0,10000 до 10,00000.");
            Add("Symmetry around 1.000", "Symmetrie um 1,000", "Simetría en torno a 1,000", "Symetria wokół 1,000", "Симетрія відносно 1,000");
            Add("Link min/max", "Min/Max koppeln", "Vincular mín./máx.", "Połącz min./maks.", "Зв'язати min/max");
            Add("Maintains min × max = 1 and updates the opposite bound automatically.", "Hält min × max = 1 und aktualisiert die andere Grenze automatisch.", "Mantiene mín. × máx. = 1 y actualiza el otro límite automáticamente.", "Utrzymuje min × maks. = 1 i automatycznie zmienia drugą granicę.", "Підтримує min × max = 1 і автоматично змінює іншу межу.");
            Add("Maximum step", "Maximaler Schritt", "Paso máximo", "Maksymalny krok", "Максимальний крок");
            Add("Largest change in one transition.", "Größte Änderung bei einem Übergang.", "Mayor cambio en una transición.", "Największa zmiana w jednym przejściu.", "Найбільша зміна за один перехід.");
            Add("Return toward 1.000", "Rückkehr zu 1,000", "Retorno hacia 1,000", "Powrót do 1,000", "Повернення до 1,000");
            Add("Pull of Random Walk toward base sensitivity.", "Zug des Random Walk zur Basisempfindlichkeit.", "Atracción del paseo aleatorio hacia la sensibilidad base.", "Siła powrotu Random Walk do czułości bazowej.", "Сила повернення Random Walk до базової чутливості.");
            Add("Mean", "Mittelwert", "Media", "Średnia", "Середнє");
            Add("Center of the Gaussian distribution.", "Mitte der Gauß-Verteilung.", "Centro de la distribución gaussiana.", "Środek rozkładu Gaussa.", "Центр гаусового розподілу.");
            Add("Spread of values around the center.", "Streuung der Werte um die Mitte.", "Dispersión de valores en torno al centro.", "Rozrzut wartości wokół środka.", "Розкид значень навколо центру.");
            Add("Randomization", "Randomisierung", "Aleatorización", "Losowanie", "Рандомізація");
            Add("Constant gain between scheduled changes", "Konstante Verstärkung zwischen geplanten Wechseln", "Ganancia constante entre cambios programados", "Stałe wzmocnienie między zmianami", "Постійний gain між запланованими змінами");
            Add("Random range", "Zufallsbereich", "Rango aleatorio", "Losowy zakres", "Випадковий діапазон");
            Add("Fixed", "Fest", "Fijo", "Stały", "Фіксований");
            Add("Minimum interval", "Mindestintervall", "Intervalo mínimo", "Minimalny interwał", "Мінімальний інтервал");
            Add("Fixed interval", "Festes Intervall", "Intervalo fijo", "Stały interwał", "Фіксований інтервал");
            Add("Seconds. Raw Accel's technical minimum is 1.25.", "Sekunden. Das technische Minimum von Raw Accel ist 1,25.", "Segundos. El mínimo técnico de Raw Accel es 1,25.", "Sekundy. Techniczne minimum Raw Accel to 1,25.", "Секунди. Технічний мінімум Raw Accel дорівнює 1,25.");
            Add("Maximum interval", "Maximalintervall", "Intervalo máximo", "Maksymalny interwał", "Максимальний інтервал");
            Add("Upper bound of the randomized delay.", "Obergrenze der zufälligen Pause.", "Límite superior de la pausa aleatoria.", "Górna granica losowej przerwy.", "Верхня межа випадкової паузи.");
            Add("Change timing", "Wechselzeit", "Frecuencia de cambio", "Czas zmian", "Частота зміни");
            Add("When the next multiplier is applied", "Wann der nächste Multiplikator angewendet wird", "Cuándo se aplica el siguiente multiplicador", "Kiedy stosowany jest kolejny mnożnik", "Коли застосовується наступний множник");
            Add("Interval mode", "Intervallmodus", "Modo de intervalo", "Tryb interwału", "Режим інтервалу");
            Add("Fixed or randomized delay.", "Feste oder zufällige Pause.", "Pausa fija o aleatoria.", "Stała lub losowa przerwa.", "Фіксована або випадкова пауза.");
            Add("eDPI display", "eDPI-Anzeige", "Visualización de eDPI", "Wyświetlanie eDPI", "Відображення eDPI");
            Add("Does not change mouse DPI or base sensitivity", "Ändert weder Maus-DPI noch Basisempfindlichkeit", "No cambia los DPI del ratón ni la sensibilidad base", "Nie zmienia DPI myszy ani czułości bazowej", "Не змінює DPI миші або базову чутливість");
            Add("Used only for UI calculations.", "Wird nur für Berechnungen der Oberfläche verwendet.", "Solo se utiliza para cálculos de la interfaz.", "Używane tylko do obliczeń w interfejsie.", "Використовується лише для розрахунків в інтерфейсі.");
            Add("Base sensitivity", "Basisempfindlichkeit", "Sensibilidad base", "Czułość bazowa", "Базова чутливість");
            Add("Base sensitivity for effective sensitivity and eDPI.", "Basisempfindlichkeit für effektive Empfindlichkeit und eDPI.", "Sensibilidad base para la sensibilidad efectiva y eDPI.", "Czułość bazowa do obliczania efektywnej czułości i eDPI.", "Базова чутливість для ефективної чутливості та eDPI.");
            Add("Session and data", "Sitzung und Daten", "Sesión y datos", "Sesja i dane", "Сесія та дані");
            Add("Auto-stop, repeatability and local history", "Auto-Stopp, Wiederholbarkeit und lokaler Verlauf", "Parada automática, repetibilidad e historial local", "Auto-stop, powtarzalność i lokalna historia", "Автозупинка, повторюваність і локальна історія");
            Add("Randomized phase, min", "Zufallsphase, Min.", "Fase aleatoria, min", "Faza losowa, min", "Випадкова фаза, хв");
            Add("0 means no time limit.", "0 bedeutet ohne Zeitlimit.", "0 significa sin límite de tiempo.", "0 oznacza brak limitu czasu.", "0 означає роботу без обмеження часу.");
            Add("Repeatable sequence", "Wiederholbare Sequenz", "Secuencia repetible", "Powtarzalna sekwencja", "Повторювана послідовність");
            Add("Fixed seed for comparable sessions.", "Fester Seed für vergleichbare Sitzungen.", "Semilla fija para sesiones comparables.", "Stałe ziarno dla porównywalnych sesji.", "Фіксований seed для порівнюваних сесій.");
            Add("Enable", "Aktivieren", "Activar", "Włącz", "Увімкнути");
            Add("Logging", "Protokollierung", "Registro", "Logowanie", "Журналювання");
            Add("Stored locally in LocalAppData only.", "Wird nur lokal in LocalAppData gespeichert.", "Solo se guarda localmente en LocalAppData.", "Zapisywane tylko lokalnie w LocalAppData.", "Зберігається лише локально в LocalAppData.");
            Add("Quick start", "Schnellstart", "Inicio rápido", "Szybki start", "Швидкий старт");
            Add("Complete shutdown", "Vollständiges Beenden", "Cierre completo", "Pełne wyłączenie", "Повне вимкнення");
            Add("●  LIVE MULTIPLIER", "●  LIVE-MULTIPLIKATOR", "●  MULTIPLICADOR EN VIVO", "●  MNOŻNIK NA ŻYWO", "●  МНОЖНИК НАЖИВО");
            Add("not checked", "nicht geprüft", "sin comprobar", "nie sprawdzono", "не перевірено");
            Add("Timer", "Timer", "Temporizador", "Licznik", "Таймер");
            Add("Next change", "Nächster Wechsel", "Próximo cambio", "Następna zmiana", "Наступна зміна");
            Add("Changes", "Wechsel", "Cambios", "Zmiany", "Зміни");
            Add("Verify", "Prüfen", "Comprobar", "Sprawdź", "Перевірити");
            Add("Open logs", "Logs öffnen", "Abrir registros", "Otwórz logi", "Відкрити журнали");
            Add("Start  F8", "Start  F8", "Iniciar  F8", "Start  F8", "Старт  F8");
            Add("Pause  F10", "Pause  F10", "Pausa  F10", "Pauza  F10", "Пауза  F10");
            Add("Resume  F10", "Fortsetzen  F10", "Reanudar  F10", "Wznów  F10", "Продовжити  F10");
            Add("Reset  F9", "Reset  F9", "Restablecer  F9", "Reset  F9", "Скинути  F9");
            Add("Raw Accel has not been checked yet.", "Raw Accel wurde noch nicht geprüft.", "Raw Accel aún no se ha comprobado.", "Raw Accel nie został jeszcze sprawdzony.", "Raw Accel ще не перевірено.");
            Add("Configuration imported and validated.", "Konfiguration importiert und geprüft.", "Configuración importada y validada.", "Konfiguracja została zaimportowana i sprawdzona.", "Конфігурацію імпортовано й перевірено.");
            Add("Configuration exported. It contains no logs or system data.", "Konfiguration exportiert. Sie enthält keine Logs oder Systemdaten.", "Configuración exportada. No contiene registros ni datos del sistema.", "Konfiguracja wyeksportowana. Nie zawiera logów ani danych systemowych.", "Конфігурацію експортовано. Вона не містить журналів або системних даних.");
            Add("Import configuration", "Konfiguration importieren", "Importar configuración", "Importuj konfigurację", "Імпорт конфігурації");
            Add("Export configuration", "Konfiguration exportieren", "Exportar configuración", "Eksportuj konfigurację", "Експорт конфігурації");
            Add("Import complete", "Import abgeschlossen", "Importación completada", "Import zakończony", "Імпорт завершено");
            Add("Export complete", "Export abgeschlossen", "Exportación completada", "Eksport zakończony", "Експорт завершено");
            Add("Import failed", "Import fehlgeschlagen", "Error de importación", "Błąd importu", "Помилка імпорту");
            Add("Export failed", "Export fehlgeschlagen", "Error de exportación", "Błąd eksportu", "Помилка експорту");
            Add("Invalid settings", "Ungültige Einstellungen", "Ajustes no válidos", "Nieprawidłowe ustawienia", "Некоректні налаштування");
            Add("Fix the settings error first.", "Behebe zuerst den Fehler in den Einstellungen.", "Corrige primero el error de configuración.", "Najpierw popraw błąd ustawień.", "Спочатку виправ помилку в налаштуваннях.");
            Add("Language change failed", "Sprachwechsel fehlgeschlagen", "Error al cambiar el idioma", "Nie udało się zmienić języka", "Не вдалося змінити мову");
            Add("Theme change failed", "Designwechsel fehlgeschlagen", "Error al cambiar el tema", "Nie udało się zmienić motywu", "Не вдалося змінити тему");
            Add("The chart appears after the first change", "Das Diagramm erscheint nach dem ersten Wechsel", "El gráfico aparece después del primer cambio", "Wykres pojawi się po pierwszej zmianie", "Графік з'явиться після першої зміни");
            Add("Centered Gaussian", "Zentrierte Gauß-Verteilung", "Gaussiana centrada", "Wyśrodkowany Gauss", "Центрований Gaussian");
            Add("Linear Uniform", "Linear gleichverteilt", "Uniforme lineal", "Liniowy Uniform", "Лінійний Uniform");
            Add("Keeps multiplier 1.000 after the randomized phase.", "Hält nach der Zufallsphase den Multiplikator 1,000.", "Mantiene el multiplicador 1,000 después de la fase aleatoria.", "Utrzymuje mnożnik 1,000 po fazie losowej.", "Утримує множник 1,000 після випадкової фази.");
            Add("The same seed repeats the sequence.", "Derselbe Seed wiederholt die Sequenz.", "La misma semilla repite la secuencia.", "To samo ziarno powtarza sekwencję.", "Однаковий seed повторює послідовність.");
            Add("Accel Type must be Off/noaccel in every Raw Accel profile.", "Accel Type muss in jedem Raw-Accel-Profil Off/noaccel sein.", "Accel Type debe ser Off/noaccel en todos los perfiles de Raw Accel.", "Accel Type musi być Off/noaccel w każdym profilu Raw Accel.", "Accel Type має бути Off/noaccel у кожному профілі Raw Accel.");
            Add("1. Select Off/noaccel in Raw Accel and click Apply.\n2. Click Verify. Constant gain and multiplier 1.000 are required.\n3. Configure the range and timing.\n4. Click Start. Stop or Reset restores 1.000.", "1. Wähle Off/noaccel in Raw Accel und klicke Apply.\n2. Klicke Prüfen. Konstante Verstärkung und 1,000 sind erforderlich.\n3. Stelle Bereich und Zeiten ein.\n4. Klicke Start. Stop oder Reset stellt 1,000 wieder her.", "1. Selecciona Off/noaccel en Raw Accel y pulsa Apply.\n2. Pulsa Comprobar. Se requieren ganancia constante y 1,000.\n3. Configura el rango y los tiempos.\n4. Pulsa Iniciar. Stop o Reset restaura 1,000.", "1. Wybierz Off/noaccel w Raw Accel i kliknij Apply.\n2. Kliknij Sprawdź. Wymagane są stałe wzmocnienie i 1,000.\n3. Ustaw zakres i czas.\n4. Kliknij Start. Stop lub Reset przywraca 1,000.", "1. Вибери Off/noaccel у Raw Accel і натисни Apply.\n2. Натисни Перевірити. Потрібні constant gain і 1,000.\n3. Налаштуй діапазон і час.\n4. Натисни Старт. Stop або Reset повертає 1,000.");
            Add("Every cycle visits eight logarithmic zones: four below and four above 1.000. With linked bounds, a full cycle's geometric mean is 1.000.", "Jeder Zyklus besucht acht logarithmische Zonen: vier unter und vier über 1,000. Bei gekoppelten Grenzen beträgt das geometrische Mittel 1,000.", "Cada ciclo visita ocho zonas logarítmicas: cuatro por debajo y cuatro por encima de 1,000. Con límites vinculados, la media geométrica es 1,000.", "Każdy cykl odwiedza osiem stref logarytmicznych: cztery poniżej i cztery powyżej 1,000. Przy połączonych granicach średnia geometryczna wynosi 1,000.", "Кожен цикл відвідує вісім логарифмічних зон: чотири нижче й чотири вище 1,000. За зв'язаних меж геометричне середнє дорівнює 1,000.");
            Add("Stop, Reset, timer completion and normal close restore multiplier 1.000 with readback. The app does not remain in the tray or start with Windows.", "Stop, Reset, Timerende und normales Schließen stellen 1,000 mit Readback wieder her. Die App bleibt nicht im Tray und startet nicht mit Windows.", "Stop, Reset, el fin del temporizador y el cierre normal restauran 1,000 con lectura de comprobación. La app no queda en la bandeja ni se inicia con Windows.", "Stop, Reset, koniec licznika i zwykłe zamknięcie przywracają 1,000 z odczytem kontrolnym. Aplikacja nie zostaje w zasobniku ani nie uruchamia się z Windows.", "Stop, Reset, завершення таймера й штатне закриття повертають 1,000 з readback. Програма не лишається в tray і не запускається з Windows.");
            Add("●  ResetGuard ready", "●  ResetGuard bereit", "●  ResetGuard listo", "●  ResetGuard gotowy", "●  ResetGuard готовий");
            Add("●  ResetGuard unavailable", "●  ResetGuard nicht verfügbar", "●  ResetGuard no disponible", "●  ResetGuard niedostępny", "●  ResetGuard недоступний");
            Add("8 logarithmic zones per cycle: exactly 4 below and 4 above 1.000.", "8 logarithmische Zonen pro Zyklus: genau 4 unter und 4 über 1,000.", "8 zonas logarítmicas por ciclo: exactamente 4 por debajo y 4 por encima de 1,000.", "8 stref logarytmicznych na cykl: dokładnie 4 poniżej i 4 powyżej 1,000.", "8 логарифмічних зон за цикл: рівно 4 нижче і 4 вище 1,000.");
            Add("Values concentrate near the center; the edges are deliberately rare.", "Werte konzentrieren sich in der Mitte; Randwerte sind absichtlich selten.", "Los valores se concentran cerca del centro; los extremos son deliberadamente raros.", "Wartości skupiają się blisko środka; skrajne są celowo rzadkie.", "Значення концентруються біля центру; крайні навмисно рідкісні.");
            Add("Every point of the linear range is equally likely.", "Jeder Punkt des linearen Bereichs ist gleich wahrscheinlich.", "Cada punto del rango lineal es igual de probable.", "Każdy punkt zakresu liniowego jest jednakowo prawdopodobny.", "Кожна точка лінійного діапазону однаково ймовірна.");
            Add("✓ Settings are valid. Start is available after Raw Accel verification.", "✓ Einstellungen sind gültig. Start ist nach der Raw-Accel-Prüfung verfügbar.", "✓ Los ajustes son válidos. Iniciar estará disponible tras comprobar Raw Accel.", "✓ Ustawienia są prawidłowe. Start będzie dostępny po sprawdzeniu Raw Accel.", "✓ Налаштування коректні. Старт буде доступний після перевірки Raw Accel.");
        }

        public static AppLanguage Current { get { return _current; } }
        public static AppLanguage SystemLanguage { get { return DetectSystemLanguage(); } }
        public static AppLanguage[] SupportedLanguages { get { return (AppLanguage[])Languages.Clone(); } }

        public static void UseSystemLanguage() { _current = DetectSystemLanguage(); }

        public static void SetLanguage(AppLanguage language)
        {
            _current = Array.IndexOf(Languages, language) >= 0 ? language : AppLanguage.English;
        }

        public static int IndexOfLanguage(AppLanguage language)
        {
            var index = Array.IndexOf(Languages, language);
            return index < 0 ? 1 : index;
        }

        public static AppLanguage LanguageFromIndex(int index)
        {
            return index >= 0 && index < Languages.Length ? Languages[index] : AppLanguage.English;
        }

        public static string[] LanguageDisplayNames()
        {
            return new[] { "Русский", "English", "Deutsch", "Español", "Polski", "Українська" };
        }

        public static string[] ThemeDisplayNames()
        {
            return new[] { T("Тёмная", "Dark"), T("Светлая", "Light"), T("Пастельная", "Pastel") };
        }

        public static int IndexOfTheme(AppTheme theme)
        {
            return theme == AppTheme.Light ? 1 : theme == AppTheme.Pastel ? 2 : 0;
        }

        public static AppTheme ThemeFromIndex(int index)
        {
            return index == 1 ? AppTheme.Light : index == 2 ? AppTheme.Pastel : AppTheme.Dark;
        }

        public static string T(string russian, string english)
        {
            if (_current == AppLanguage.Russian) return russian;
            if (_current == AppLanguage.English) return english;
            Dictionary<string, string> language;
            string translated;
            return Translations.TryGetValue(_current, out language) && language.TryGetValue(english, out translated)
                ? translated
                : english;
        }

        public static string YesNo(bool value)
        {
            switch (_current)
            {
                case AppLanguage.Russian: return value ? "ДА" : "НЕТ";
                case AppLanguage.German: return value ? "JA" : "NEIN";
                case AppLanguage.Spanish: return value ? "SÍ" : "NO";
                case AppLanguage.Polish: return value ? "TAK" : "NIE";
                case AppLanguage.Ukrainian: return value ? "ТАК" : "НІ";
                default: return value ? "YES" : "NO";
            }
        }

        private static void Add(string english, string german, string spanish, string polish, string ukrainian)
        {
            Translations[AppLanguage.German][english] = german;
            Translations[AppLanguage.Spanish][english] = spanish;
            Translations[AppLanguage.Polish][english] = polish;
            Translations[AppLanguage.Ukrainian][english] = ukrainian;
        }

        private static AppLanguage DetectSystemLanguage()
        {
            var name = CultureInfo.CurrentUICulture == null
                ? string.Empty
                : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case "ru": return AppLanguage.Russian;
                case "de": return AppLanguage.German;
                case "es": return AppLanguage.Spanish;
                case "pl": return AppLanguage.Polish;
                case "uk": return AppLanguage.Ukrainian;
                default: return AppLanguage.English;
            }
        }
    }
}
