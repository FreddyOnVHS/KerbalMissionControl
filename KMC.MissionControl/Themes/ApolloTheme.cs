using System.Drawing;

namespace KMC.MissionControl.Themes
{
    /// <summary>
    /// Shared colors, fonts, and dimensions for the
    /// Apollo-era Mission Control visual style.
    /// </summary>
    public static class ApolloTheme
    {
        // Main console body
        public static readonly Color WindowBackground =
            Color.FromArgb(52, 58, 55);

        public static readonly Color ConsoleFace =
            Color.FromArgb(164, 169, 153);

        public static readonly Color ConsoleFaceLight =
            Color.FromArgb(194, 198, 181);

        public static readonly Color ConsoleFaceDark =
            Color.FromArgb(104, 109, 99);

        public static readonly Color ConsoleEdge =
            Color.FromArgb(52, 56, 52);

        // Recessed instrument areas
        public static readonly Color InstrumentWell =
            Color.FromArgb(12, 16, 17);

        public static readonly Color InstrumentWellLight =
            Color.FromArgb(31, 38, 38);

        public static readonly Color InstrumentBorder =
            Color.FromArgb(73, 79, 74);

        // CRT colors
        public static readonly Color CrtBackground =
            Color.FromArgb(7, 18, 25);

        public static readonly Color CrtBlue =
            Color.FromArgb(185, 225, 255);

        public static readonly Color CrtWhite =
            Color.FromArgb(220, 238, 242);

        public static readonly Color CrtGreen =
            Color.FromArgb(135, 230, 166);

        public static readonly Color CrtDim =
            Color.FromArgb(120, 175, 195);

        // Printed and engraved text
        public static readonly Color PrimaryText =
            Color.FromArgb(224, 231, 220);

        public static readonly Color SecondaryText =
            Color.FromArgb(145, 158, 147);

        public static readonly Color EngravedLabel =
            Color.FromArgb(31, 34, 30);

        // Indicator lamps
        public static readonly Color LampGreen =
            Color.FromArgb(45, 190, 72);

        public static readonly Color LampAmber =
            Color.FromArgb(239, 170, 39);

        public static readonly Color LampRed =
            Color.FromArgb(204, 55, 45);

        public static readonly Color LampBlue =
            Color.FromArgb(60, 139, 214);

        public static readonly Color LampOff =
            Color.FromArgb(54, 57, 51);

        // Hardware details
        public static readonly Color ScrewLight =
            Color.FromArgb(215, 216, 203);

        public static readonly Color ScrewDark =
            Color.FromArgb(64, 67, 62);

        public static readonly Color ButtonFace =
            Color.FromArgb(42, 44, 40);

        public static readonly Color ButtonBorder =
            Color.FromArgb(105, 108, 99);

        // Compatibility names used by existing controls
        public static readonly Color PanelBackground = ConsoleFace;
        public static readonly Color PanelHighlight = ConsoleFaceLight;
        public static readonly Color PanelBorder = ConsoleEdge;
        public static readonly Color PanelInset = InstrumentWell;
        public static readonly Color MetalFace = ConsoleFace;
        public static readonly Color MetalDark = ConsoleFaceDark;

        // Layout
        public const int PanelBorderWidth = 2;
        public const int PanelPadding = 12;
        public const int ControlSpacing = 8;
        public const int SectionSpacing = 16;

        public static Font CreateConsoleFont(
            float size,
            FontStyle style = FontStyle.Regular)
        {
            return new Font(
                "Consolas",
                size,
                style,
                GraphicsUnit.Point);
        }

        public static Font CreateLabelFont(
            float size,
            FontStyle style = FontStyle.Bold)
        {
            return new Font(
                "Arial",
                size,
                style,
                GraphicsUnit.Point);
        }

        public static Font CreateDisplayFont(
            float size,
            FontStyle style = FontStyle.Bold)
        {
            return new Font(
                "Consolas",
                size,
                style,
                GraphicsUnit.Point);
        }
    }
}