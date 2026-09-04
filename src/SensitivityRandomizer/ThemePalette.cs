using System.Windows.Media;

namespace SensitivityRandomizer
{
    internal sealed class ThemePalette
    {
        public Color BackgroundColor { get; private set; }
        public Color BackgroundAltColor { get; private set; }
        public Color SurfaceColor { get; private set; }
        public Color SurfaceRaisedColor { get; private set; }
        public Color InputColor { get; private set; }
        public Color OutlineColor { get; private set; }
        public Color BorderSoftColor { get; private set; }
        public Color TextPrimaryColor { get; private set; }
        public Color TextSecondaryColor { get; private set; }
        public Color AccentColor { get; private set; }
        public Color AccentStrongColor { get; private set; }
        public Color PurpleColor { get; private set; }
        public Color BlueColor { get; private set; }
        public Color DangerColor { get; private set; }
        public Color WarningColor { get; private set; }
        public Color DisabledTextColor { get; private set; }
        public Color ToggleOffColor { get; private set; }
        public Color ToggleDisabledColor { get; private set; }
        public Color ScrollRailColor { get; private set; }
        public Color ScrollThumbColor { get; private set; }
        public Color ScrollThumbHoverColor { get; private set; }
        public Color ChartBackgroundColor { get; private set; }
        public Color ChartGridColor { get; private set; }
        public Color ChartBaseColor { get; private set; }
        public Color ChartEmptyColor { get; private set; }
        public Color LiveStartColor { get; private set; }
        public Color LiveEndColor { get; private set; }
        public Color PrimaryStartColor { get; private set; }
        public Color PrimaryEndColor { get; private set; }
        public Color PrimaryHoverStartColor { get; private set; }
        public Color PrimaryHoverEndColor { get; private set; }
        public Color TabStartColor { get; private set; }
        public Color TabEndColor { get; private set; }
        public Color DangerSurfaceColor { get; private set; }
        public Color DangerHoverColor { get; private set; }
        public Color GhostHoverColor { get; private set; }
        public Color RequiredBackgroundColor { get; private set; }
        public Color OptionalBackgroundColor { get; private set; }
        public Color ModeNoteBackgroundColor { get; private set; }

        public SolidColorBrush Background { get; private set; }
        public SolidColorBrush Surface { get; private set; }
        public SolidColorBrush SurfaceRaised { get; private set; }
        public SolidColorBrush Input { get; private set; }
        public SolidColorBrush Outline { get; private set; }
        public SolidColorBrush BorderSoft { get; private set; }
        public SolidColorBrush TextPrimary { get; private set; }
        public SolidColorBrush TextSecondary { get; private set; }
        public SolidColorBrush Accent { get; private set; }
        public SolidColorBrush Purple { get; private set; }
        public SolidColorBrush Blue { get; private set; }
        public SolidColorBrush Danger { get; private set; }
        public SolidColorBrush Warning { get; private set; }
        public SolidColorBrush DisabledText { get; private set; }

        private ThemePalette(
            Color background, Color backgroundAlt, Color surface, Color surfaceRaised, Color input,
            Color outline, Color borderSoft, Color textPrimary, Color textSecondary,
            Color accent, Color accentStrong, Color purple, Color blue, Color danger, Color warning,
            Color disabledText, Color toggleOff, Color toggleDisabled,
            Color scrollRail, Color scrollThumb, Color scrollThumbHover,
            Color chartBackground, Color chartGrid, Color chartBase, Color chartEmpty,
            Color liveStart, Color liveEnd,
            Color primaryStart, Color primaryEnd, Color primaryHoverStart, Color primaryHoverEnd,
            Color tabStart, Color tabEnd, Color dangerSurface, Color dangerHover, Color ghostHover,
            Color requiredBackground, Color optionalBackground, Color modeNoteBackground)
        {
            BackgroundColor = background;
            BackgroundAltColor = backgroundAlt;
            SurfaceColor = surface;
            SurfaceRaisedColor = surfaceRaised;
            InputColor = input;
            OutlineColor = outline;
            BorderSoftColor = borderSoft;
            TextPrimaryColor = textPrimary;
            TextSecondaryColor = textSecondary;
            AccentColor = accent;
            AccentStrongColor = accentStrong;
            PurpleColor = purple;
            BlueColor = blue;
            DangerColor = danger;
            WarningColor = warning;
            DisabledTextColor = disabledText;
            ToggleOffColor = toggleOff;
            ToggleDisabledColor = toggleDisabled;
            ScrollRailColor = scrollRail;
            ScrollThumbColor = scrollThumb;
            ScrollThumbHoverColor = scrollThumbHover;
            ChartBackgroundColor = chartBackground;
            ChartGridColor = chartGrid;
            ChartBaseColor = chartBase;
            ChartEmptyColor = chartEmpty;
            LiveStartColor = liveStart;
            LiveEndColor = liveEnd;
            PrimaryStartColor = primaryStart;
            PrimaryEndColor = primaryEnd;
            PrimaryHoverStartColor = primaryHoverStart;
            PrimaryHoverEndColor = primaryHoverEnd;
            TabStartColor = tabStart;
            TabEndColor = tabEnd;
            DangerSurfaceColor = dangerSurface;
            DangerHoverColor = dangerHover;
            GhostHoverColor = ghostHover;
            RequiredBackgroundColor = requiredBackground;
            OptionalBackgroundColor = optionalBackground;
            ModeNoteBackgroundColor = modeNoteBackground;

            Background = Brush(background);
            Surface = Brush(surface);
            SurfaceRaised = Brush(surfaceRaised);
            Input = Brush(input);
            Outline = Brush(outline);
            BorderSoft = Brush(borderSoft);
            TextPrimary = Brush(textPrimary);
            TextSecondary = Brush(textSecondary);
            Accent = Brush(accent);
            Purple = Brush(purple);
            Blue = Brush(blue);
            Danger = Brush(danger);
            Warning = Brush(warning);
            DisabledText = Brush(disabledText);
        }

        public static ThemePalette Create(AppTheme theme)
        {
            if (theme == AppTheme.Light)
            {
                return new ThemePalette(
                    C(244, 247, 251), C(237, 242, 248), C(255, 255, 255), C(238, 243, 248), C(249, 251, 253),
                    C(198, 209, 223), C(226, 233, 241), C(27, 37, 55), C(91, 106, 128),
                    C(13, 174, 159), C(8, 142, 132), C(118, 86, 216), C(46, 125, 203), C(205, 65, 84), C(161, 103, 18),
                    C(143, 154, 170), C(199, 209, 220), C(222, 228, 236),
                    C(221, 228, 237), C(154, 170, 190), C(116, 138, 166),
                    C(248, 250, 253), C(213, 221, 232), C(123, 139, 161), C(151, 163, 180),
                    C(255, 255, 255), C(244, 246, 255),
                    C(46, 201, 187), C(119, 97, 225), C(65, 215, 202), C(141, 119, 235),
                    C(216, 241, 238), C(236, 228, 250), C(255, 240, 242), C(255, 228, 233), C(229, 236, 245),
                    C(234, 249, 247), C(237, 246, 255), C(238, 244, 252));
            }

            if (theme == AppTheme.Pastel)
            {
                return new ThemePalette(
                    C(244, 239, 248), C(237, 230, 244), C(255, 250, 254), C(241, 234, 247), C(251, 247, 252),
                    C(210, 197, 225), C(234, 224, 241), C(49, 40, 62), C(116, 101, 130),
                    C(73, 183, 169), C(48, 152, 142), C(145, 113, 196), C(101, 145, 194), C(205, 105, 119), C(174, 126, 68),
                    C(156, 144, 166), C(211, 201, 220), C(228, 220, 234),
                    C(225, 215, 233), C(174, 157, 190), C(145, 124, 166),
                    C(252, 248, 253), C(226, 216, 235), C(149, 132, 166), C(176, 161, 189),
                    C(255, 250, 254), C(246, 240, 252),
                    C(95, 204, 190), C(151, 125, 210), C(117, 217, 204), C(170, 147, 222),
                    C(222, 241, 240), C(239, 229, 248), C(255, 238, 242), C(252, 222, 229), C(233, 224, 241),
                    C(234, 248, 245), C(237, 244, 253), C(241, 236, 249));
            }

            return new ThemePalette(
                C(8, 13, 23), C(10, 16, 29), C(15, 23, 37), C(21, 31, 49), C(10, 17, 29),
                C(44, 59, 84), C(34, 47, 69), C(242, 246, 255), C(145, 160, 187),
                C(68, 224, 208), C(48, 190, 180), C(167, 139, 250), C(103, 184, 255), C(248, 113, 113), C(251, 191, 36),
                C(102, 114, 139), C(42, 53, 73), C(31, 39, 54),
                C(27, 38, 57), C(67, 83, 111), C(91, 112, 148),
                C(12, 19, 32), C(42, 55, 78), C(110, 127, 158), C(91, 105, 132),
                C(17, 26, 42), C(22, 28, 49),
                C(68, 224, 208), C(130, 105, 229), C(88, 239, 224), C(156, 129, 245),
                C(39, 85, 91), C(69, 53, 105), C(43, 24, 35), C(65, 31, 44), C(31, 43, 64),
                C(13, 47, 46), C(16, 34, 57), C(17, 31, 54));
        }

        private static Color C(byte red, byte green, byte blue)
        {
            return Color.FromRgb(red, green, blue);
        }

        private static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    internal static class UiPalette
    {
        private static ThemePalette _current = ThemePalette.Create(AppTheme.Dark);

        public static ThemePalette Current
        {
            get { return _current; }
        }

        public static void Select(AppTheme theme)
        {
            _current = ThemePalette.Create(theme);
        }
    }
}
