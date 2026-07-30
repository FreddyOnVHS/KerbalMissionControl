using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering
{
    /// <summary>
    /// Provides the drawing resources and virtual-canvas information
    /// needed by a mission page.
    /// </summary>
    public sealed class MissionRenderContext
    {
        public Graphics Graphics { get; }

        public Rectangle ContentBounds { get; }

        public Font LargeFont { get; }

        public Font SmallFont { get; }

        public Color PhosphorColor { get; }

        public Color DimPhosphorColor { get; }

        public Size VirtualCanvasSize { get; }

        public int VirtualWidth
        {
            get
            {
                return VirtualCanvasSize.Width;
            }
        }

        public int VirtualHeight
        {
            get
            {
                return VirtualCanvasSize.Height;
            }
        }

        public MissionRenderContext(
            Graphics graphics,
            Rectangle contentBounds,
            Font largeFont,
            Font smallFont,
            Color phosphorColor,
            Color dimPhosphorColor)
            : this(
                graphics,
                contentBounds,
                largeFont,
                smallFont,
                phosphorColor,
                dimPhosphorColor,
                contentBounds.Size)
        {
        }

        public MissionRenderContext(
            Graphics graphics,
            Rectangle contentBounds,
            Font largeFont,
            Font smallFont,
            Color phosphorColor,
            Color dimPhosphorColor,
            Size virtualCanvasSize)
        {
            if (graphics == null)
            {
                throw new ArgumentNullException(
                    nameof(graphics));
            }

            if (largeFont == null)
            {
                throw new ArgumentNullException(
                    nameof(largeFont));
            }

            if (smallFont == null)
            {
                throw new ArgumentNullException(
                    nameof(smallFont));
            }

            if (contentBounds.Width <= 0 ||
                contentBounds.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contentBounds),
                    "Content bounds must have a positive size.");
            }

            if (virtualCanvasSize.Width <= 0 ||
                virtualCanvasSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(virtualCanvasSize),
                    "Virtual canvas size must be positive.");
            }

            Graphics = graphics;
            ContentBounds = contentBounds;
            LargeFont = largeFont;
            SmallFont = smallFont;
            PhosphorColor = phosphorColor;
            DimPhosphorColor = dimPhosphorColor;
            VirtualCanvasSize = virtualCanvasSize;
        }

        public Rectangle GetRelativeRectangle(
            float x,
            float y,
            float width,
            float height)
        {
            int left =
                ContentBounds.Left +
                (int)Math.Round(
                    ContentBounds.Width * x);

            int top =
                ContentBounds.Top +
                (int)Math.Round(
                    ContentBounds.Height * y);

            int rectangleWidth =
                (int)Math.Round(
                    ContentBounds.Width * width);

            int rectangleHeight =
                (int)Math.Round(
                    ContentBounds.Height * height);

            return new Rectangle(
                left,
                top,
                rectangleWidth,
                rectangleHeight);
        }

        public Point GetRelativePoint(
            float x,
            float y)
        {
            return new Point(
                ContentBounds.Left +
                (int)Math.Round(
                    ContentBounds.Width * x),
                ContentBounds.Top +
                (int)Math.Round(
                    ContentBounds.Height * y));
        }
    }
}