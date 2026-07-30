using System.Drawing;

namespace KMC.MissionControl.Rendering
{
    public sealed class MissionRenderContext
    {
        public Graphics Graphics { get; }

        public Rectangle ContentBounds { get; }

        public Font LargeFont { get; }

        public Font SmallFont { get; }

        public Color PhosphorColor { get; }

        public Color DimPhosphorColor { get; }

        public MissionRenderContext(
            Graphics graphics,
            Rectangle contentBounds,
            Font largeFont,
            Font smallFont,
            Color phosphorColor,
            Color dimPhosphorColor)
        {
            Graphics = graphics;
            ContentBounds = contentBounds;
            LargeFont = largeFont;
            SmallFont = smallFont;
            PhosphorColor = phosphorColor;
            DimPhosphorColor = dimPhosphorColor;
        }
    }
}