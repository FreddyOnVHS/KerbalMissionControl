using System.Drawing;

namespace KMC.MissionControl.Rendering
{
    public static class DisplayDrawing
    {
        public static void DrawHorizontalDivider(
            MissionRenderContext context,
            int y,
            int left,
            int right)
        {
            using (Pen pen =
                new Pen(context.DimPhosphorColor, 1))
            {
                context.Graphics.DrawLine(
                    pen,
                    left,
                    y,
                    right,
                    y);
            }
        }

        public static void DrawVerticalDivider(
            MissionRenderContext context,
            int x,
            int top,
            int bottom)
        {
            using (Pen pen =
                new Pen(context.DimPhosphorColor, 1))
            {
                context.Graphics.DrawLine(
                    pen,
                    x,
                    top,
                    x,
                    bottom);
            }
        }

        public static void DrawLabel(
            MissionRenderContext context,
            string text,
            float x,
            float y)
        {
            string safeText =
                string.IsNullOrWhiteSpace(text)
                    ? "---"
                    : text.Trim().ToUpperInvariant();

            using (SolidBrush brush =
                new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    safeText,
                    context.SmallFont,
                    brush,
                    x,
                    y);
            }
        }

        public static void DrawValue(
            MissionRenderContext context,
            string text,
            float x,
            float y)
        {
            string safeText =
                string.IsNullOrWhiteSpace(text)
                    ? "---"
                    : text.Trim().ToUpperInvariant();

            using (SolidBrush brush =
                new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    safeText,
                    context.LargeFont,
                    brush,
                    x,
                    y);
            }
        }

        public static void DrawSectionTitle(
            MissionRenderContext context,
            string text,
            Rectangle bounds)
        {
            string safeText =
                string.IsNullOrWhiteSpace(text)
                    ? "---"
                    : text.Trim().ToUpperInvariant();

            using (SolidBrush brush =
                new SolidBrush(context.PhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    safeText,
                    context.SmallFont,
                    brush,
                    bounds,
                    format);
            }
        }

        public static void DrawPageHeader(
            MissionRenderContext context,
            string title,
            string channel,
            Rectangle bounds)
        {
            string safeTitle =
                string.IsNullOrWhiteSpace(title)
                    ? "---"
                    : title.Trim().ToUpperInvariant();

            string safeChannel =
                string.IsNullOrWhiteSpace(channel)
                    ? "CH --"
                    : channel.Trim().ToUpperInvariant();

            int left =
                bounds.Left +
                DisplayLayout.ScreenMarginX;

            int right =
                bounds.Right -
                DisplayLayout.ScreenMarginX;

            int top =
                bounds.Top +
                DisplayLayout.ScreenMarginY;

            using (SolidBrush brush =
                new SolidBrush(context.PhosphorColor))
            using (StringFormat rightFormat =
                new StringFormat())
            {
                rightFormat.Alignment =
                    StringAlignment.Far;

                rightFormat.LineAlignment =
                    StringAlignment.Near;

                context.Graphics.DrawString(
                    safeTitle,
                    context.LargeFont,
                    brush,
                    left,
                    top);

                context.Graphics.DrawString(
                    safeChannel,
                    context.LargeFont,
                    brush,
                    right,
                    top,
                    rightFormat);
            }

            int dividerY =
                top +
                DisplayLayout.HeaderDividerOffsetY;

            DrawHorizontalDivider(
                context,
                dividerY,
                left,
                right);
        }

        public static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int columnX,
            int y)
        {
            string safeLabel =
                string.IsNullOrWhiteSpace(label)
                    ? "---"
                    : label.Trim().ToUpperInvariant();

            string safeValue =
                string.IsNullOrWhiteSpace(value)
                    ? "---"
                    : value.Trim().ToUpperInvariant();

            using (SolidBrush brush =
                new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    safeLabel,
                    context.LargeFont,
                    brush,
                    columnX,
                    y);

                context.Graphics.DrawString(
                    safeValue,
                    context.LargeFont,
                    brush,
                    columnX + DisplayLayout.ValueOffsetX,
                    y);
            }
        }

        public static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int columnX,
            int row,
            int startY)
        {
            int y =
                startY +
                (row * DisplayLayout.RowHeight);

            DrawField(
                context,
                label,
                value,
                columnX,
                y);
        }
    }
}