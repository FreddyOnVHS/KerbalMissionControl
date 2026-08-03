using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the Orbit Trend panel.
    /// </summary>
    public sealed class OrbitTrendRenderer
    {
        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            OrbitTrendRenderModel model)
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

            float panelFontSize =
                Math.Max(
                    7.0f,
                    context.SmallFont.Size *
                    0.82f);

            using (Font panelFont =
                new Font(
                    context.SmallFont.FontFamily,
                    panelFontSize,
                    FontStyle.Regular,
                    GraphicsUnit.Point))
            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        62,
                        context.DimPhosphorColor),
                    1.0f))
            using (Pen orbitPen =
                new Pen(
                    context.PhosphorColor,
                    1.4f))
            using (Pen dividerPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Brush textBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Brush dimBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Brush bodyBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                const int padding = 10;
                const int titleHeight = 25;

                graphics.DrawString(
                    "ORBIT TREND",
                    panelFont,
                    textBrush,
                    bounds.Left + padding,
                    bounds.Top + 7);

                Rectangle content =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 4,
                        bounds.Width - padding * 2,
                        bounds.Height -
                        titleHeight -
                        padding - 5);

                int dataWidth =
                    Math.Max(
                        135,
                        content.Width * 34 / 100);

                Rectangle orbitArea =
                    new Rectangle(
                        content.Left,
                        content.Top,
                        content.Width -
                        dataWidth -
                        10,
                        content.Height);

                Rectangle dataArea =
                    new Rectangle(
                        orbitArea.Right + 10,
                        content.Top,
                        dataWidth,
                        content.Height);

                graphics.DrawLine(
                    dividerPen,
                    dataArea.Left,
                    dataArea.Top,
                    dataArea.Left,
                    dataArea.Bottom);

                Rectangle orbitPlot =
                    Rectangle.Inflate(
                        orbitArea,
                        -10,
                        -10);

                DrawGrid(
                    graphics,
                    gridPen,
                    orbitPlot);

                double eccentricity =
                    IsFinite(
                        model.Eccentricity)
                        ? Clamp(
                            model.Eccentricity,
                            0.0,
                            0.94)
                        : 0.0;

                float centerX =
                    orbitPlot.Left +
                    orbitPlot.Width * 0.52f;

                float centerY =
                    orbitPlot.Top +
                    orbitPlot.Height * 0.50f;

                float semiMajor =
                    orbitPlot.Width * 0.43f;

                float semiMinor =
                    Math.Min(
                        orbitPlot.Height * 0.37f,
                        semiMajor *
                        (float)Math.Sqrt(
                            Math.Max(
                                0.12,
                                1.0 -
                                eccentricity *
                                eccentricity)));

                graphics.DrawEllipse(
                    orbitPen,
                    centerX - semiMajor,
                    centerY - semiMinor,
                    semiMajor * 2.0f,
                    semiMinor * 2.0f);

                graphics.FillEllipse(
                    bodyBrush,
                    centerX - 4.0f,
                    centerY - 4.0f,
                    8.0f,
                    8.0f);

                double anomaly =
                    IsFinite(
                        model.TrueAnomalyDegrees)
                        ? model.TrueAnomalyDegrees *
                          Math.PI /
                          180.0
                        : 0.0;

                float vesselX =
                    centerX +
                    semiMajor *
                    (float)Math.Cos(
                        anomaly);

                float vesselY =
                    centerY -
                    semiMinor *
                    (float)Math.Sin(
                        anomaly);

                graphics.FillEllipse(
                    dimBrush,
                    vesselX - 3.0f,
                    vesselY - 3.0f,
                    6.0f,
                    6.0f);

                int rowHeight =
                    Math.Max(
                        26,
                        dataArea.Height / 3);

                DrawOrbitDataRow(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    dataArea,
                    0,
                    rowHeight,
                    "APOAPSIS",
                    FormatDistance(
                        model.ApoapsisMeters));

                DrawOrbitDataRow(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    dataArea,
                    1,
                    rowHeight,
                    "PERIAPSIS",
                    FormatDistance(
                        model.PeriapsisMeters));

                DrawOrbitDataRow(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    dataArea,
                    2,
                    rowHeight,
                    "INCLINATION",
                    FormatAngle(
                        model.InclinationDegrees));
            }
        }

        private static void DrawGrid(
            Graphics graphics,
            Pen gridPen,
            Rectangle orbitPlot)
        {
            for (int index = 1;
                 index < 4;
                 index++)
            {
                int x =
                    orbitPlot.Left +
                    orbitPlot.Width *
                    index /
                    4;

                graphics.DrawLine(
                    gridPen,
                    x,
                    orbitPlot.Top,
                    x,
                    orbitPlot.Bottom);
            }

            graphics.DrawLine(
                gridPen,
                orbitPlot.Left,
                orbitPlot.Top +
                orbitPlot.Height / 2,
                orbitPlot.Right,
                orbitPlot.Top +
                orbitPlot.Height / 2);
        }

        private static void DrawOrbitDataRow(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            int index,
            int rowHeight,
            string label,
            string value)
        {
            int top =
                bounds.Top +
                index *
                rowHeight;

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left + 10,
                    top,
                    bounds.Width - 20,
                    rowHeight / 2);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + 10,
                    top + rowHeight / 2,
                    bounds.Width - 20,
                    rowHeight -
                    rowHeight / 2);

            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center,

                    Trimming =
                        StringTrimming.EllipsisCharacter,

                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.DrawString(
                    label ?? string.Empty,
                    font,
                    labelBrush,
                    labelBounds,
                    format);

                graphics.DrawString(
                    value ?? string.Empty,
                    font,
                    valueBrush,
                    valueBounds,
                    format);
            }
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            double absolute =
                Math.Abs(meters);

            if (absolute >= 1000000.0)
            {
                return
                    (meters / 1000000.0)
                    .ToString("0.00") +
                    " MM";
            }

            if (absolute >= 1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString("0.0") +
                    " KM";
            }

            return
                meters.ToString("0") +
                " M";
        }

        private static string FormatAngle(
            double degrees)
        {
            if (!IsFinite(degrees))
            {
                return "---";
            }

            return
                degrees.ToString("0.0") +
                "°";
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
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
