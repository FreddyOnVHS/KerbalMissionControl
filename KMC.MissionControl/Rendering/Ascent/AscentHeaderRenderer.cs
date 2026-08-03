using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the Ascent Guidance title bar.
    /// </summary>
    public sealed class AscentHeaderRenderer
    {
        public void Draw(
            MissionRenderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            Graphics graphics =
                context.Graphics;

            Rectangle titleBounds =
                context.GetRelativeRectangle(
                    0.015f,
                    0.018f,
                    0.970f,
                    0.055f);

            using (Pen linePen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Brush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                graphics.DrawLine(
                    linePen,
                    titleBounds.Left,
                    titleBounds.Bottom,
                    titleBounds.Right,
                    titleBounds.Bottom);

                graphics.DrawString(
                    "ASCENT GUIDANCE",
                    context.LargeFont,
                    titleBrush,
                    titleBounds.Left,
                    titleBounds.Top);

                const string channel =
                    "CH 02";

                SizeF channelSize =
                    graphics.MeasureString(
                        channel,
                        context.LargeFont);

                graphics.DrawString(
                    channel,
                    context.LargeFont,
                    titleBrush,
                    titleBounds.Right -
                    channelSize.Width,
                    titleBounds.Top);
            }
        }
    }
}
