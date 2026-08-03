using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the altitude-versus-downrange ascent graph.
    /// </summary>
    public sealed class AscentGraphRenderer
    {
        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            AscentGraphRenderModel model)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (model == null)
            {
                return;
            }

            Graphics graphics =
                context.Graphics;

            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        70,
                        context.DimPhosphorColor),
                    1.0f))
            using (Pen targetPen =
                new Pen(
                    context.DimPhosphorColor,
                    2.0f))
            using (Pen actualPen =
                new Pen(
                    Color.FromArgb(
                        230,
                        255,
                        90,
                        80),
                    2.2f))
            {
                targetPen.DashStyle =
                    DashStyle.Dash;

                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                Rectangle plot =
                    Rectangle.Inflate(
                        bounds,
                        -64,
                        -54);

                plot.Y += 18;
                plot.Height -= 18;

                DrawGrid(
                    graphics,
                    plot,
                    gridPen);

                DrawAxisLabels(
                    context,
                    plot);

                DrawCurve(
                    graphics,
                    plot,
                    targetPen,
                    model.TargetPoints,
                    model.MaximumDownrangeMeters,
                    model.MaximumAltitudeMeters);

                DrawCurve(
                    graphics,
                    plot,
                    actualPen,
                    model.ActualPoints,
                    model.MaximumDownrangeMeters,
                    model.MaximumAltitudeMeters);

                DrawCurrentMarker(
                    context,
                    plot,
                    model);

                using (Brush labelBrush =
                    new SolidBrush(
                        context.PhosphorColor))
                {
                    graphics.DrawString(
                        "ALTITUDE VS DOWNRANGE",
                        context.SmallFont,
                        labelBrush,
                        bounds.Left + 10,
                        bounds.Top + 8);
                }

                DrawLegend(
                    context,
                    bounds);
            }
        }

        private static void DrawGrid(
            Graphics graphics,
            Rectangle plot,
            Pen gridPen)
        {
            const int verticalDivisions =
                8;

            const int horizontalDivisions =
                6;

            for (int index = 0;
                 index <= verticalDivisions;
                 index++)
            {
                int x =
                    plot.Left +
                    plot.Width *
                    index /
                    verticalDivisions;

                graphics.DrawLine(
                    gridPen,
                    x,
                    plot.Top,
                    x,
                    plot.Bottom);
            }

            for (int index = 0;
                 index <= horizontalDivisions;
                 index++)
            {
                int y =
                    plot.Top +
                    plot.Height *
                    index /
                    horizontalDivisions;

                graphics.DrawLine(
                    gridPen,
                    plot.Left,
                    y,
                    plot.Right,
                    y);
            }
        }

        private static void DrawAxisLabels(
            MissionRenderContext context,
            Rectangle plot)
        {
            Graphics graphics =
                context.Graphics;

            using (Brush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                graphics.DrawString(
                    "ALT",
                    context.SmallFont,
                    brush,
                    plot.Left - 34,
                    plot.Top - 4);

                const string rangeLabel =
                    "DOWNRANGE";

                SizeF size =
                    graphics.MeasureString(
                        rangeLabel,
                        context.SmallFont);

                graphics.DrawString(
                    rangeLabel,
                    context.SmallFont,
                    brush,
                    plot.Right -
                    size.Width,
                    plot.Bottom + 7);
            }
        }

        private static void DrawCurve(
            Graphics graphics,
            Rectangle plot,
            Pen pen,
            AscentGraphPoint[] source,
            double maxDownrange,
            double maxAltitude)
        {
            if (source == null ||
                source.Length < 2)
            {
                return;
            }

            PointF[] points =
                new PointF[
                    source.Length];

            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                AscentGraphPoint point =
                    source[index];

                points[index] =
                    MapPoint(
                        plot,
                        point != null
                            ? point.DownrangeMeters
                            : 0.0,
                        point != null
                            ? point.AltitudeMeters
                            : 0.0,
                        maxDownrange,
                        maxAltitude);
            }

            graphics.DrawLines(
                pen,
                points);
        }

        private static void DrawCurrentMarker(
            MissionRenderContext context,
            Rectangle plot,
            AscentGraphRenderModel model)
        {
            AscentGraphPoint[] actual =
                model.ActualPoints;

            if (actual == null ||
                actual.Length == 0)
            {
                return;
            }

            AscentGraphPoint current =
                actual[
                    actual.Length - 1];

            if (current == null)
            {
                return;
            }

            PointF point =
                MapPoint(
                    plot,
                    current.DownrangeMeters,
                    current.AltitudeMeters,
                    model.MaximumDownrangeMeters,
                    model.MaximumAltitudeMeters);

            RectangleF marker =
                new RectangleF(
                    point.X - 4.0f,
                    point.Y - 4.0f,
                    8.0f,
                    8.0f);

            using (Brush brush =
                new SolidBrush(
                    Color.FromArgb(
                        240,
                        255,
                        110,
                        90)))
            {
                context.Graphics.FillEllipse(
                    brush,
                    marker);
            }
        }

        private static void DrawLegend(
            MissionRenderContext context,
            Rectangle bounds)
        {
            Graphics graphics =
                context.Graphics;

            int y =
                bounds.Bottom - 22;

            using (Pen targetPen =
                new Pen(
                    context.DimPhosphorColor,
                    2.0f))
            using (Pen actualPen =
                new Pen(
                    Color.FromArgb(
                        230,
                        255,
                        90,
                        80),
                    2.0f))
            using (Brush textBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                targetPen.DashStyle =
                    DashStyle.Dash;

                graphics.DrawLine(
                    targetPen,
                    bounds.Left + 12,
                    y,
                    bounds.Left + 42,
                    y);

                graphics.DrawString(
                    "TARGET",
                    context.SmallFont,
                    textBrush,
                    bounds.Left + 48,
                    y - 8);

                graphics.DrawLine(
                    actualPen,
                    bounds.Left + 128,
                    y,
                    bounds.Left + 158,
                    y);

                graphics.DrawString(
                    "ACTUAL",
                    context.SmallFont,
                    textBrush,
                    bounds.Left + 164,
                    y - 8);
            }
        }

        private static PointF MapPoint(
            Rectangle plot,
            double downrange,
            double altitude,
            double maxDownrange,
            double maxAltitude)
        {
            double xFraction =
                maxDownrange > 0.0
                    ? downrange /
                      maxDownrange
                    : 0.0;

            double yFraction =
                maxAltitude > 0.0
                    ? altitude /
                      maxAltitude
                    : 0.0;

            xFraction =
                Clamp(
                    xFraction,
                    0.0,
                    1.0);

            yFraction =
                Clamp(
                    yFraction,
                    0.0,
                    1.0);

            return new PointF(
                plot.Left +
                (float)(
                    plot.Width *
                    xFraction),
                plot.Bottom -
                (float)(
                    plot.Height *
                    yFraction));
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }
    }
}
