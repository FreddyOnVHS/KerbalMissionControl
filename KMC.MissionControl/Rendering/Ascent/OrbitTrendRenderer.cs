using System;
using System.Drawing;
using System.Drawing.Drawing2D;

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
                    0.72f);

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
                    1.3f))
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

                const int padding = 8;
                const int titleHeight = 24;

                graphics.DrawString(
                    "ORBIT TREND",
                    panelFont,
                    textBrush,
                    bounds.Left + padding,
                    bounds.Top + 6);

                Rectangle content =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 2,
                        bounds.Width - padding * 2,
                        bounds.Height -
                        titleHeight -
                        padding - 4);

                int dataHeight =
                    Math.Max(
                        64,
                        content.Height * 34 / 100);

                Rectangle orbitArea =
                    new Rectangle(
                        content.Left,
                        content.Top,
                        content.Width,
                        Math.Max(
                            30,
                            content.Height - dataHeight - 4));

                Rectangle dataArea =
                    new Rectangle(
                        content.Left,
                        orbitArea.Bottom + 4,
                        content.Width,
                        dataHeight);

                Rectangle orbitPlot =
                    Rectangle.Inflate(
                        orbitArea,
                        -10,
                        -7);

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
                    orbitPlot.Width * 0.50f;

                float centerY =
                    orbitPlot.Top +
                    orbitPlot.Height * 0.50f;

                float semiMajor =
                    orbitPlot.Width * 0.40f;

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
                    centerX - 3.0f,
                    centerY - 3.0f,
                    6.0f,
                    6.0f);

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

                int columnWidth =
                    Math.Max(
                        1,
                        dataArea.Width / 3);

                DrawCompactData(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    new Rectangle(
                        dataArea.Left,
                        dataArea.Top,
                        columnWidth,
                        dataArea.Height),
                    "AP",
                    FormatDistance(
                        model.ApoapsisMeters));

                DrawCompactData(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    new Rectangle(
                        dataArea.Left + columnWidth,
                        dataArea.Top,
                        columnWidth,
                        dataArea.Height),
                    "PE",
                    FormatDistance(
                        model.PeriapsisMeters));

                DrawCompactData(
                    graphics,
                    panelFont,
                    dimBrush,
                    textBrush,
                    new Rectangle(
                        dataArea.Left + columnWidth * 2,
                        dataArea.Top,
                        dataArea.Width - columnWidth * 2,
                        dataArea.Height),
                    "INC",
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

        private static void DrawCompactData(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            string label,
            string value)
        {
            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    Math.Max(
                        14,
                        bounds.Height / 2));

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left,
                    labelBounds.Bottom,
                    bounds.Width,
                    Math.Max(
                        14,
                        bounds.Bottom - labelBounds.Bottom));

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

    /// <summary>
    /// Render-only attitude state for the Build 9.5 FDAI/navball foundation.
    ///
    /// Full 3-D velocity-vector markers are intentionally deferred because
    /// current telemetry does not yet provide explicit North/East/Up velocity
    /// components. FlightPathAngle therefore supplies the initial vertical
    /// prograde/flight-path cue.
    /// </summary>
    public sealed class NavballRenderModel
    {
        public double PitchDegrees { get; set; }

        public double HeadingDegrees { get; set; }

        public double RollDegrees { get; set; }

        public bool FlightPathAvailable { get; set; }

        public double FlightPathAngleDegrees { get; set; }

        public bool GuidanceAvailable { get; set; }

        public double CommandedPitchDegrees { get; set; }

        public double PitchErrorDegrees { get; set; }

        public string FlightPhase { get; set; }

        public bool CutoffRequired { get; set; }

        public bool CoastLockoutActive { get; set; }

        public bool OrbitHandoffRequired { get; set; }

        public bool FlashAlert { get; set; }
    }

    /// <summary>
    /// KSP-behavior / FDAI-visual-language attitude sphere.
    ///
    /// The vehicle reference is fixed at screen center. The local planetary
    /// attitude sphere rotates underneath it from heading, pitch, and roll.
    ///
    /// Sphere grid:
    /// - latitude / pitch lines every 10 degrees
    /// - longitude / heading lines every 15 degrees
    /// - major grid emphasis every 30 degrees
    /// - pitch labels every 20 degrees where visible
    /// - heading labels every 30 degrees around the horizon
    ///
    /// No vehicle symbol motion is permitted; attitude is expressed entirely
    /// by movement of the sphere beneath the fixed reference.
    /// </summary>
    public sealed class NavballRenderer
    {
        private const double DegreesToRadians =
            Math.PI /
            180.0;

        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            NavballRenderModel model)
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

            SmoothingMode oldSmoothing =
                graphics.SmoothingMode;

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            float fontSize =
                Math.Max(
                    8.0f,
                    context.SmallFont.Size *
                    0.70f);

            using (Font font =
                new Font(
                    context.SmallFont.FontFamily,
                    fontSize,
                    FontStyle.Regular,
                    GraphicsUnit.Point))
            using (Font microFont =
                new Font(
                    context.SmallFont.FontFamily,
                    Math.Max(
                        8.0f,
                        fontSize * 0.90f),
                    FontStyle.Regular,
                    GraphicsUnit.Point))
            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen majorPen =
                new Pen(
                    Color.FromArgb(244, 220, 235, 245),
                    2.10f))
            using (Pen minorPen =
                new Pen(
                    Color.FromArgb(214, 188, 212, 228),
                    1.65f))
            using (Pen faintPen =
                new Pen(
                    Color.FromArgb(176, 170, 194, 212),
                    1.25f))
            using (Pen horizonPen =
                new Pen(
                    Color.FromArgb(
                        255,
                        236,
                        246,
                        252),
                    2.90f))
            using (Pen referenceOutlinePen =
                new Pen(
                    Color.FromArgb(232, 28, 18, 8),
                    5.8f))
            using (Pen referencePen =
                new Pen(
                    Color.FromArgb(255, 255, 176, 64),
                    3.15f))
            using (Brush brightBrush =
                new SolidBrush(
                    Color.FromArgb(244, 220, 235, 245)))
            using (Brush dimBrush =
                new SolidBrush(
                    Color.FromArgb(214, 188, 212, 228)))
            using (Brush sphereBrush =
                new SolidBrush(
                    Color.FromArgb(
                        52,
                        context.PhosphorColor)))
            using (Pen guidanceOutlinePen =
                new Pen(
                    Color.FromArgb(
                        225,
                        2,
                        10,
                        14),
                    5.2f))
            using (Pen guidancePen =
                new Pen(
                    Color.FromArgb(
                        255,
                        116,
                        255,
                        170),
                    2.8f))
            using (Brush guidanceBrush =
                new SolidBrush(
                    Color.FromArgb(
                        255,
                        116,
                        255,
                        170)))
            using (Brush guidanceBackdropBrush =
                new SolidBrush(
                    Color.FromArgb(
                        210,
                        2,
                        10,
                        14)))
            using (Brush cautionBrush =
                new SolidBrush(
                    Color.FromArgb(
                        255,
                        255,
                        176,
                        64)))
            {
                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                graphics.DrawString(
                    "ATTITUDE / FDAI",
                    font,
                    brightBrush,
                    bounds.Left + 10,
                    bounds.Top + 7);

                /*
                 * Explicitly reserve title and numeric-strip bands. The ball
                 * owns only the center instrument region.
                 */
                Rectangle instrumentBounds =
                    new Rectangle(
                        bounds.Left + 12,
                        bounds.Top + 38,
                        bounds.Width - 24,
                        Math.Max(
                            40,
                            bounds.Height - 92));

                int diameter =
                    Math.Max(
                        44,
                        Math.Min(
                            instrumentBounds.Width - 54,
                            instrumentBounds.Height - 10));

                PointF center =
                    new PointF(
                        instrumentBounds.Left +
                        instrumentBounds.Width / 2.0f,
                        instrumentBounds.Top +
                        instrumentBounds.Height / 2.0f);

                float radius =
                    diameter /
                    2.0f;

                RectangleF sphere =
                    new RectangleF(
                        center.X - radius,
                        center.Y - radius,
                        radius * 2.0f,
                        radius * 2.0f);

                graphics.FillEllipse(
                    sphereBrush,
                    sphere);

                DrawTrueSphereGrid(
                    graphics,
                    sphere,
                    center,
                    radius,
                    NormalizePitch(
                        model.PitchDegrees),
                    NormalizeHeading(
                        model.HeadingDegrees),
                    NormalizeSigned180(
                        model.RollDegrees),
                    horizonPen,
                    majorPen,
                    minorPen,
                    faintPen,
                    microFont,
                    brightBrush,
                    dimBrush);

                DrawLeftHeadingRepeater(
                    graphics,
                    center,
                    radius,
                    NormalizeHeading(
                        model.HeadingDegrees),
                    microFont,
                    brightBrush);

                graphics.DrawEllipse(
                    majorPen,
                    sphere);

                DrawFixedRollScale(
                    graphics,
                    center,
                    radius,
                    majorPen,
                    minorPen);

                DrawRollPointer(
                    graphics,
                    center,
                    radius,
                    NormalizeSigned180(
                        model.RollDegrees),
                    majorPen,
                    brightBrush);

                /*
                 * Fixed spacecraft/nose reference: this NEVER moves.
                 */
                DrawVehicleReference(
                    graphics,
                    center,
                    radius,
                    referenceOutlinePen);

                DrawVehicleReference(
                    graphics,
                    center,
                    radius,
                    referencePen);

                if (model.FlightPathAvailable)
                {
                    DrawFlightPathMarker(
                        graphics,
                        center,
                        radius,
                        NormalizePitch(
                            model.PitchDegrees),
                        model.FlightPathAngleDegrees,
                        NormalizeSigned180(
                            model.RollDegrees),
                        referenceOutlinePen);

                    DrawFlightPathMarker(
                        graphics,
                        center,
                        radius,
                        NormalizePitch(
                            model.PitchDegrees),
                        model.FlightPathAngleDegrees,
                        NormalizeSigned180(
                            model.RollDegrees),
                        referencePen);
                }

                if (model.GuidanceAvailable)
                {
                    DrawCommandedPitchCue(
                        graphics,
                        center,
                        radius,
                        model,
                        guidanceOutlinePen);

                    DrawCommandedPitchCue(
                        graphics,
                        center,
                        radius,
                        model,
                        guidancePen);

                    DrawPitchDeviationScale(
                        graphics,
                        bounds,
                        center,
                        radius,
                        microFont,
                        guidancePen,
                        guidanceBrush,
                        guidanceBackdropBrush,
                        model);

                    DrawGuidanceAnnunciator(
                        graphics,
                        bounds,
                        microFont,
                        guidanceBrush,
                        guidanceBackdropBrush,
                        cautionBrush,
                        model);
                }

                DrawNumericStrip(
                    graphics,
                    bounds,
                    font,
                    brightBrush,
                    dimBrush,
                    model);
            }

            graphics.SmoothingMode =
                oldSmoothing;
        }

        private static void DrawTrueSphereGrid(
            Graphics graphics,
            RectangleF sphere,
            PointF center,
            float radius,
            double pitchDegrees,
            double headingDegrees,
            double rollDegrees,
            Pen horizonPen,
            Pen majorPen,
            Pen minorPen,
            Pen faintPen,
            Font font,
            Brush brightBrush,
            Brush dimBrush)
        {
            GraphicsState clipState =
                graphics.Save();

            using (GraphicsPath clip =
                new GraphicsPath())
            {
                clip.AddEllipse(
                    sphere);

                graphics.SetClip(
                    clip,
                    CombineMode.Intersect);

                AttitudeBasis basis =
                    CreateBasis(
                        pitchDegrees,
                        headingDegrees,
                        rollDegrees);

                /*
                 * Latitude / pitch grid. World latitude is elevation above the
                 * local horizon. The vehicle rotates; the sphere does not.
                 */
                for (int latitude = -80;
                     latitude <= 80;
                     latitude += 10)
                {
                    bool horizon =
                        latitude == 0;

                    bool major =
                        latitude % 30 == 0;

                    Pen pen =
                        horizon
                            ? horizonPen
                            : major
                                ? majorPen
                                : minorPen;

                    DrawLatitude(
                        graphics,
                        center,
                        radius,
                        latitude,
                        basis,
                        pen);

                    if (latitude != 0 &&
                        latitude % 20 == 0)
                    {
                        DrawLatitudeLabel(
                            graphics,
                            center,
                            radius,
                            latitude,
                            basis,
                            font,
                            latitude > 0
                                ? brightBrush
                                : dimBrush);
                    }
                }

                /*
                 * Longitude / heading grid. 15-degree lines provide the dense
                 * KSP-like visual reference; 30-degree lines are emphasized.
                 */
                for (int longitude = 0;
                     longitude < 360;
                     longitude += 15)
                {
                    bool major =
                        longitude % 30 == 0;

                    DrawLongitude(
                        graphics,
                        center,
                        radius,
                        longitude,
                        basis,
                        major
                            ? majorPen
                            : faintPen);

                    if (longitude % 30 == 0)
                    {
                        DrawHeadingLabel(
                            graphics,
                            center,
                            radius,
                            longitude,
                            basis,
                            font,
                            brightBrush);
                    }
                }
            }

            graphics.Restore(
                clipState);
        }

        private static void DrawLatitude(
            Graphics graphics,
            PointF center,
            float radius,
            double latitudeDegrees,
            AttitudeBasis basis,
            Pen pen)
        {
            const int StepDegrees = 3;

            bool havePrevious =
                false;

            PointF previous =
                PointF.Empty;

            for (int heading = 0;
                 heading <= 360;
                 heading += StepDegrees)
            {
                Vector3 world =
                    DirectionFromHeadingPitch(
                        heading,
                        latitudeDegrees);

                ProjectedPoint projected =
                    Project(
                        world,
                        basis,
                        center,
                        radius);

                if (havePrevious &&
                    projected.Visible)
                {
                    graphics.DrawLine(
                        pen,
                        previous,
                        projected.Point);
                }

                havePrevious =
                    projected.Visible;

                if (projected.Visible)
                {
                    previous =
                        projected.Point;
                }
            }
        }

        private static void DrawLongitude(
            Graphics graphics,
            PointF center,
            float radius,
            double headingDegrees,
            AttitudeBasis basis,
            Pen pen)
        {
            const int StepDegrees = 2;

            bool havePrevious =
                false;

            PointF previous =
                PointF.Empty;

            for (int latitude = -90;
                 latitude <= 90;
                 latitude += StepDegrees)
            {
                Vector3 world =
                    DirectionFromHeadingPitch(
                        headingDegrees,
                        latitude);

                ProjectedPoint projected =
                    Project(
                        world,
                        basis,
                        center,
                        radius);

                if (havePrevious &&
                    projected.Visible)
                {
                    graphics.DrawLine(
                        pen,
                        previous,
                        projected.Point);
                }

                havePrevious =
                    projected.Visible;

                if (projected.Visible)
                {
                    previous =
                        projected.Point;
                }
            }
        }

        private static void DrawLatitudeLabel(
            Graphics graphics,
            PointF center,
            float radius,
            int latitude,
            AttitudeBasis basis,
            Font font,
            Brush brush)
        {
            /*
             * Try labels 25 degrees either side of the current vessel heading.
             * This keeps pitch values readable without stacking them at center.
             */
            double[] offsets =
            {
                -25.0,
                25.0
            };

            for (int index = 0;
                 index < offsets.Length;
                 index++)
            {
                Vector3 world =
                    DirectionFromHeadingPitch(
                        basis.HeadingDegrees +
                        offsets[index],
                        latitude);

                ProjectedPoint projected =
                    Project(
                        world,
                        basis,
                        center,
                        radius);

                if (!projected.Visible ||
                    projected.RadiusFraction >
                        0.75f)
                {
                    continue;
                }

                string text =
                    Math.Abs(
                        latitude)
                    .ToString("0");

                SizeF size =
                    graphics.MeasureString(
                        text,
                        font);

                graphics.DrawString(
                    text,
                    font,
                    brush,
                    projected.Point.X -
                    size.Width / 2.0f,
                    projected.Point.Y -
                    size.Height / 2.0f);
            }
        }

        private static void DrawHeadingLabel(
            Graphics graphics,
            PointF center,
            float radius,
            int heading,
            AttitudeBasis basis,
            Font font,
            Brush brush)
        {
            Vector3 world =
                DirectionFromHeadingPitch(
                    heading,
                    0.0);

            ProjectedPoint projected =
                Project(
                    world,
                    basis,
                    center,
                    radius);

            if (!projected.Visible ||
                projected.RadiusFraction >
                    0.78f)
            {
                return;
            }

            string text =
                FormatSphereHeading(
                    heading);

            SizeF size =
                graphics.MeasureString(
                    text,
                    font);

            RectangleF labelBounds =
                new RectangleF(
                    projected.Point.X -
                    size.Width / 2.0f - 2.0f,
                    projected.Point.Y -
                    size.Height / 2.0f - 1.0f,
                    size.Width + 4.0f,
                    size.Height + 2.0f);

            using (Brush labelBackdrop =
                new SolidBrush(
                    Color.FromArgb(
                        185,
                        2,
                        10,
                        14)))
            {
                graphics.FillRectangle(
                    labelBackdrop,
                    labelBounds);
            }

            graphics.DrawString(
                text,
                font,
                brush,
                projected.Point.X -
                size.Width / 2.0f,
                projected.Point.Y -
                size.Height / 2.0f);
        }

        private static void DrawLeftHeadingRepeater(
            Graphics graphics,
            PointF center,
            float radius,
            double headingDegrees,
            Font font,
            Brush brush)
        {
            string headingText =
                NormalizeHeading(
                    headingDegrees)
                .ToString("000");

            string cardinal =
                FormatHeadingCardinal(
                    headingDegrees);

            string display =
                string.IsNullOrEmpty(
                    cardinal)
                    ? headingText
                    : headingText +
                      " " +
                      cardinal;

            SizeF textSize =
                graphics.MeasureString(
                    display,
                    font);

            float x =
                center.X -
                radius * 0.67f;

            float y =
                center.Y -
                textSize.Height * 0.50f;

            RectangleF backdrop =
                new RectangleF(
                    x - 6.0f,
                    y - 3.0f,
                    textSize.Width + 12.0f,
                    textSize.Height + 6.0f);

            using (Brush backingBrush =
                new SolidBrush(
                    Color.FromArgb(
                        214,
                        2,
                        10,
                        14)))
            {
                graphics.FillRectangle(
                    backingBrush,
                    backdrop);
            }

            graphics.DrawString(
                display,
                font,
                brush,
                x,
                y);
        }

        private static string FormatHeadingCardinal(
            double headingDegrees)
        {
            double heading =
                NormalizeHeading(
                    headingDegrees);

            if (AngularDistance(
                    heading,
                    0.0) <= 11.25)
            {
                return "N";
            }

            if (AngularDistance(
                    heading,
                    90.0) <= 11.25)
            {
                return "E";
            }

            if (AngularDistance(
                    heading,
                    180.0) <= 11.25)
            {
                return "S";
            }

            if (AngularDistance(
                    heading,
                    270.0) <= 11.25)
            {
                return "W";
            }

            return string.Empty;
        }

        private static double AngularDistance(
            double a,
            double b)
        {
            double difference =
                Math.Abs(
                    NormalizeHeading(a) -
                    NormalizeHeading(b));

            return Math.Min(
                difference,
                360.0 - difference);
        }

        private static void DrawFixedRollScale(
            Graphics graphics,
            PointF center,
            float radius,
            Pen majorPen,
            Pen minorPen)
        {
            float outer =
                radius +
                15.0f;

            for (int degrees = -90;
                 degrees <= 90;
                 degrees += 15)
            {
                double angle =
                    (-90.0 +
                     degrees) *
                    DegreesToRadians;

                bool major =
                    degrees % 30 == 0;

                float inner =
                    outer -
                    (major
                        ? 10.0f
                        : 5.0f);

                PointF p1 =
                    new PointF(
                        center.X +
                        inner *
                        (float)Math.Cos(angle),
                        center.Y +
                        inner *
                        (float)Math.Sin(angle));

                PointF p2 =
                    new PointF(
                        center.X +
                        outer *
                        (float)Math.Cos(angle),
                        center.Y +
                        outer *
                        (float)Math.Sin(angle));

                graphics.DrawLine(
                    major
                        ? majorPen
                        : minorPen,
                    p1,
                    p2);
            }
        }

        private static void DrawRollPointer(
            Graphics graphics,
            PointF center,
            float radius,
            double rollDegrees,
            Pen pen,
            Brush brush)
        {
            /*
             * The roll scale is fixed to the instrument bezel. The pointer
             * reports vehicle roll while the ball itself also rotates beneath
             * the fixed vehicle symbol.
             */
            double angle =
                (-90.0 +
                 rollDegrees) *
                DegreesToRadians;

            float pointerRadius =
                radius +
                13.0f;

            PointF tip =
                new PointF(
                    center.X +
                    pointerRadius *
                    (float)Math.Cos(angle),
                    center.Y +
                    pointerRadius *
                    (float)Math.Sin(angle));

            float baseRadius =
                pointerRadius +
                7.0f;

            double spread =
                4.0 *
                DegreesToRadians;

            PointF left =
                new PointF(
                    center.X +
                    baseRadius *
                    (float)Math.Cos(
                        angle - spread),
                    center.Y +
                    baseRadius *
                    (float)Math.Sin(
                        angle - spread));

            PointF right =
                new PointF(
                    center.X +
                    baseRadius *
                    (float)Math.Cos(
                        angle + spread),
                    center.Y +
                    baseRadius *
                    (float)Math.Sin(
                        angle + spread));

            PointF[] triangle =
            {
                tip,
                left,
                right
            };

            graphics.DrawPolygon(
                pen,
                triangle);

            graphics.FillPolygon(
                brush,
                triangle);
        }

        private static void DrawVehicleReference(
            Graphics graphics,
            PointF center,
            float radius,
            Pen pen)
        {
            float wing =
                Math.Max(
                    15.0f,
                    radius * 0.17f);

            float inner =
                Math.Max(
                    8.0f,
                    radius * 0.075f);

            float drop =
                Math.Max(
                    8.0f,
                    radius * 0.070f);

            /*
             * KSP/FDAI-style fixed boresight:
             *
             *        |
             *   ---- + ----
             *       | |
             *
             * All sphere motion occurs underneath this symbol.
             */
            graphics.DrawLine(
                pen,
                center.X - wing,
                center.Y,
                center.X - inner,
                center.Y);

            graphics.DrawLine(
                pen,
                center.X + inner,
                center.Y,
                center.X + wing,
                center.Y);

            graphics.DrawLine(
                pen,
                center.X,
                center.Y - inner,
                center.X,
                center.Y + inner);

            graphics.DrawLine(
                pen,
                center.X - inner,
                center.Y,
                center.X - inner,
                center.Y + drop);

            graphics.DrawLine(
                pen,
                center.X + inner,
                center.Y,
                center.X + inner,
                center.Y + drop);
        }

        private static void DrawFlightPathMarker(
            Graphics graphics,
            PointF center,
            float radius,
            double pitchDegrees,
            double flightPathAngleDegrees,
            double rollDegrees,
            Pen pen)
        {
            /*
             * Current telemetry only supports a vertical-plane FPA cue.
             * Retain the verified 9.5 relationship while the attitude sphere
             * itself becomes fully 3-D.
             */
            double pitchError =
                flightPathAngleDegrees -
                pitchDegrees;

            pitchError =
                Math.Max(
                    -80.0,
                    Math.Min(
                        80.0,
                        pitchError));

            float pixelsPerDegree =
                radius /
                90.0f;

            float localY =
                (float)(-pitchError *
                pixelsPerDegree);

            PointF marker =
                RotateLocalPoint(
                    center,
                    0.0f,
                    localY,
                    rollDegrees);

            float distance =
                Distance(
                    center,
                    marker);

            if (distance >
                radius * 0.76f)
            {
                float scale =
                    radius * 0.76f /
                    Math.Max(
                        0.001f,
                        distance);

                marker =
                    new PointF(
                        center.X +
                        (marker.X - center.X) *
                        scale,
                        center.Y +
                        (marker.Y - center.Y) *
                        scale);
            }

            float markerRadius =
                Math.Max(
                    6.0f,
                    radius * 0.045f);

            graphics.DrawEllipse(
                pen,
                marker.X - markerRadius,
                marker.Y - markerRadius,
                markerRadius * 2.0f,
                markerRadius * 2.0f);

            graphics.DrawLine(
                pen,
                marker.X - markerRadius * 1.8f,
                marker.Y,
                marker.X - markerRadius,
                marker.Y);

            graphics.DrawLine(
                pen,
                marker.X + markerRadius,
                marker.Y,
                marker.X + markerRadius * 1.8f,
                marker.Y);

            graphics.DrawLine(
                pen,
                marker.X,
                marker.Y - markerRadius,
                marker.X,
                marker.Y - markerRadius * 1.8f);
        }


        private static void DrawCommandedPitchCue(
            Graphics graphics,
            PointF center,
            float radius,
            NavballRenderModel model,
            Pen pen)
        {
            double commandedPitch =
                NormalizePitch(
                    model.CommandedPitchDegrees);

            AttitudeBasis basis =
                CreateBasis(
                    NormalizePitch(
                        model.PitchDegrees),
                    NormalizeHeading(
                        model.HeadingDegrees),
                    NormalizeSigned180(
                        model.RollDegrees));

            Vector3 commandedDirection =
                DirectionFromHeadingPitch(
                    NormalizeHeading(
                        model.HeadingDegrees),
                    commandedPitch);

            ProjectedPoint projected =
                Project(
                    commandedDirection,
                    basis,
                    center,
                    radius);

            PointF cue =
                projected.Point;

            float distance =
                Distance(
                    center,
                    cue);

            if (!projected.Visible ||
                distance >
                radius * 0.76f)
            {
                double pitchError =
                    Math.Max(
                        -30.0,
                        Math.Min(
                            30.0,
                            model.PitchErrorDegrees));

                float pixelsPerDegree =
                    radius /
                    90.0f;

                PointF fallback =
                    RotateLocalPoint(
                        center,
                        0.0f,
                        (float)(
                            -pitchError *
                            pixelsPerDegree),
                        NormalizeSigned180(
                            model.RollDegrees));

                float fallbackDistance =
                    Distance(
                        center,
                        fallback);

                if (fallbackDistance >
                    radius * 0.76f)
                {
                    float scale =
                        radius * 0.76f /
                        Math.Max(
                            0.001f,
                            fallbackDistance);

                    fallback =
                        new PointF(
                            center.X +
                            (fallback.X -
                             center.X) *
                            scale,
                            center.Y +
                            (fallback.Y -
                             center.Y) *
                            scale);
                }

                cue =
                    fallback;
            }

            float half =
                Math.Max(
                    7.0f,
                    radius * 0.055f);

            PointF[] diamond =
            {
                new PointF(
                    cue.X,
                    cue.Y -
                    half),
                new PointF(
                    cue.X +
                    half,
                    cue.Y),
                new PointF(
                    cue.X,
                    cue.Y +
                    half),
                new PointF(
                    cue.X -
                    half,
                    cue.Y)
            };

            graphics.DrawPolygon(
                pen,
                diamond);

            graphics.DrawLine(
                pen,
                cue.X -
                half * 1.65f,
                cue.Y,
                cue.X -
                half,
                cue.Y);

            graphics.DrawLine(
                pen,
                cue.X +
                half,
                cue.Y,
                cue.X +
                half * 1.65f,
                cue.Y);
        }

        private static void DrawPitchDeviationScale(
            Graphics graphics,
            Rectangle bounds,
            PointF center,
            float radius,
            Font font,
            Pen pen,
            Brush brush,
            Brush backdropBrush,
            NavballRenderModel model)
        {
            float sphereRight =
                center.X +
                radius;

            float availableRight =
                bounds.Right -
                sphereRight -
                10.0f;

            if (availableRight <
                44.0f)
            {
                return;
            }

            float x =
                sphereRight +
                Math.Min(
                    38.0f,
                    availableRight *
                    0.45f);

            float halfHeight =
                radius *
                0.56f;

            float top =
                center.Y -
                halfHeight;

            float bottom =
                center.Y +
                halfHeight;

            graphics.DrawLine(
                pen,
                x,
                top,
                x,
                bottom);

            const double ScaleLimitDegrees =
                15.0;

            for (int value = -15;
                 value <= 15;
                 value += 5)
            {
                float y =
                    center.Y -
                    (float)(
                        value /
                        ScaleLimitDegrees) *
                    halfHeight;

                float tick =
                    value == 0
                        ? 12.0f
                        : 7.0f;

                graphics.DrawLine(
                    pen,
                    x - tick,
                    y,
                    x + 2.0f,
                    y);
            }

            double error =
                Math.Max(
                    -ScaleLimitDegrees,
                    Math.Min(
                        ScaleLimitDegrees,
                        model.PitchErrorDegrees));

            float pointerY =
                center.Y -
                (float)(
                    error /
                    ScaleLimitDegrees) *
                halfHeight;

            PointF[] pointer =
            {
                new PointF(
                    x + 5.0f,
                    pointerY),
                new PointF(
                    x + 16.0f,
                    pointerY - 6.0f),
                new PointF(
                    x + 16.0f,
                    pointerY + 6.0f)
            };

            graphics.FillPolygon(
                brush,
                pointer);

            string errorText =
                "P ERR " +
                model.PitchErrorDegrees
                    .ToString(
                        "+0.0;-0.0;0.0") +
                "°";

            string commandText =
                "CMD " +
                NormalizePitch(
                    model.CommandedPitchDegrees)
                    .ToString("0.0") +
                "°";

            SizeF eSize =
                graphics.MeasureString(
                    errorText,
                    font);

            SizeF cSize =
                graphics.MeasureString(
                    commandText,
                    font);

            float textX =
                Math.Min(
                    bounds.Right -
                    Math.Max(
                        eSize.Width,
                        cSize.Width) -
                    7.0f,
                    x +
                    21.0f);

            RectangleF backing =
                new RectangleF(
                    textX - 4.0f,
                    bottom - cSize.Height - eSize.Height - 6.0f,
                    Math.Max(
                        eSize.Width,
                        cSize.Width) +
                    8.0f,
                    cSize.Height +
                    eSize.Height +
                    6.0f);

            graphics.FillRectangle(
                backdropBrush,
                backing);

            graphics.DrawString(
                commandText,
                font,
                brush,
                textX,
                backing.Top + 1.0f);

            graphics.DrawString(
                errorText,
                font,
                brush,
                textX,
                backing.Top +
                cSize.Height);
        }

        private static void DrawGuidanceAnnunciator(
            Graphics graphics,
            Rectangle bounds,
            Font font,
            Brush guidanceBrush,
            Brush backdropBrush,
            Brush cautionBrush,
            NavballRenderModel model)
        {
            string text;

            Brush foreground =
                guidanceBrush;

            if (model.CutoffRequired)
            {
                text =
                    "MECO / CUTOFF";

                foreground =
                    cautionBrush;
            }
            else if (model.OrbitHandoffRequired)
            {
                text =
                    "ORBIT HANDOFF";
            }
            else if (model.CoastLockoutActive)
            {
                text =
                    "COAST LOCKOUT";
            }
            else
            {
                text =
                    string.IsNullOrWhiteSpace(
                        model.FlightPhase)
                        ? "GUIDANCE"
                        : model.FlightPhase;
            }

            SizeF size =
                graphics.MeasureString(
                    text,
                    font);

            float x =
                bounds.Right -
                size.Width -
                12.0f;

            float y =
                bounds.Top +
                7.0f;

            RectangleF backing =
                new RectangleF(
                    x - 5.0f,
                    y - 2.0f,
                    size.Width + 10.0f,
                    size.Height + 4.0f);

            graphics.FillRectangle(
                backdropBrush,
                backing);

            graphics.DrawString(
                text,
                font,
                foreground,
                x,
                y);
        }

        private static void DrawNumericStrip(
            Graphics graphics,
            Rectangle bounds,
            Font font,
            Brush valueBrush,
            Brush dimBrush,
            NavballRenderModel model)
        {
            int top =
                bounds.Bottom - 45;

            Rectangle strip =
                new Rectangle(
                    bounds.Left + 10,
                    top,
                    bounds.Width - 20,
                    36);

            string fpa =
                model.FlightPathAvailable
                    ? model.FlightPathAngleDegrees
                        .ToString("+0.0;-0.0;0.0") +
                      "°"
                    : "---";

            string[] labels =
            {
                "PITCH",
                "HDG",
                "ROLL",
                "FPA"
            };

            string[] values =
            {
                NormalizePitch(
                    model.PitchDegrees)
                    .ToString("+0.0;-0.0;0.0") +
                "°",

                NormalizeHeading(
                    model.HeadingDegrees)
                    .ToString("000.0") +
                "°",

                NormalizeSigned180(
                    model.RollDegrees)
                    .ToString("+0.0;-0.0;0.0") +
                "°",

                fpa
            };

            int columnWidth =
                Math.Max(
                    1,
                    strip.Width / 4);

            for (int index = 0;
                 index < 4;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        strip.Left +
                        columnWidth *
                        index,
                        strip.Top,
                        index == 3
                            ? strip.Right -
                              (strip.Left +
                               columnWidth * index)
                            : columnWidth,
                        strip.Height);

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
                        labels[index],
                        font,
                        dimBrush,
                        new Rectangle(
                            cell.Left,
                            cell.Top,
                            cell.Width,
                            cell.Height / 2),
                        format);

                    graphics.DrawString(
                        values[index],
                        font,
                        valueBrush,
                        new Rectangle(
                            cell.Left,
                            cell.Top +
                            cell.Height / 2,
                            cell.Width,
                            cell.Height -
                            cell.Height / 2),
                        format);
                }
            }
        }

        private static AttitudeBasis CreateBasis(
            double pitchDegrees,
            double headingDegrees,
            double rollDegrees)
        {
            double pitch =
                pitchDegrees *
                DegreesToRadians;

            double heading =
                headingDegrees *
                DegreesToRadians;

            /*
             * The attitude ball represents the world moving beneath a fixed
             * vehicle reference. Therefore the sphere transform uses the
             * inverse of vehicle roll.
             */
            double roll =
                -rollDegrees *
                DegreesToRadians;

            /*
             * Local-world coordinates:
             * X = East
             * Y = Up
             * Z = North
             *
             * Heading 0 = North, 90 = East.
             */
            Vector3 forward =
                new Vector3(
                    Math.Cos(pitch) *
                    Math.Sin(heading),
                    Math.Sin(pitch),
                    Math.Cos(pitch) *
                    Math.Cos(heading));

            Vector3 rightZeroRoll =
                new Vector3(
                    Math.Cos(heading),
                    0.0,
                    -Math.Sin(heading));

            Vector3 upZeroRoll =
                Cross(
                    forward,
                    rightZeroRoll);

            rightZeroRoll =
                Normalize(
                    rightZeroRoll);

            upZeroRoll =
                Normalize(
                    upZeroRoll);

            Vector3 right =
                Add(
                    Scale(
                        rightZeroRoll,
                        Math.Cos(roll)),
                    Scale(
                        upZeroRoll,
                        Math.Sin(roll)));

            Vector3 up =
                Add(
                    Scale(
                        rightZeroRoll,
                        -Math.Sin(roll)),
                    Scale(
                        upZeroRoll,
                        Math.Cos(roll)));

            return new AttitudeBasis
            {
                Forward =
                    Normalize(
                        forward),

                Right =
                    Normalize(
                        right),

                Up =
                    Normalize(
                        up),

                HeadingDegrees =
                    headingDegrees
            };
        }

        private static Vector3 DirectionFromHeadingPitch(
            double headingDegrees,
            double pitchDegrees)
        {
            double heading =
                NormalizeHeading(
                    headingDegrees) *
                DegreesToRadians;

            double pitch =
                pitchDegrees *
                DegreesToRadians;

            return new Vector3(
                Math.Cos(pitch) *
                Math.Sin(heading),
                Math.Sin(pitch),
                Math.Cos(pitch) *
                Math.Cos(heading));
        }

        private static ProjectedPoint Project(
            Vector3 world,
            AttitudeBasis basis,
            PointF center,
            float radius)
        {
            double x =
                Dot(
                    world,
                    basis.Right);

            double y =
                Dot(
                    world,
                    basis.Up);

            double forward =
                Dot(
                    world,
                    basis.Forward);

            float screenX =
                center.X +
                (float)x *
                radius;

            float screenY =
                center.Y -
                (float)y *
                radius;

            float dx =
                screenX -
                center.X;

            float dy =
                screenY -
                center.Y;

            return new ProjectedPoint
            {
                Visible =
                    forward >= -0.001,

                Point =
                    new PointF(
                        screenX,
                        screenY),

                RadiusFraction =
                    (float)(
                        Math.Sqrt(
                            dx * dx +
                            dy * dy) /
                        Math.Max(
                            1.0f,
                            radius))
            };
        }

        private static string FormatSphereHeading(
            int heading)
        {
            int normalized =
                (int)Math.Round(
                    NormalizeHeading(
                        heading));

            if (normalized >= 360)
            {
                normalized =
                    0;
            }

            switch (normalized)
            {
                case 0:
                    return "000 N";

                case 90:
                    return "090 E";

                case 180:
                    return "180 S";

                case 270:
                    return "270 W";

                default:
                    return
                        normalized
                            .ToString("000");
            }
        }

        private static PointF RotateLocalPoint(
            PointF center,
            float localX,
            float localY,
            double degrees)
        {
            double radians =
                degrees *
                DegreesToRadians;

            double cosine =
                Math.Cos(
                    radians);

            double sine =
                Math.Sin(
                    radians);

            return new PointF(
                center.X +
                (float)(
                    localX *
                    cosine -
                    localY *
                    sine),
                center.Y +
                (float)(
                    localX *
                    sine +
                    localY *
                    cosine));
        }

        private static float Distance(
            PointF a,
            PointF b)
        {
            float dx =
                b.X -
                a.X;

            float dy =
                b.Y -
                a.Y;

            return
                (float)Math.Sqrt(
                    dx * dx +
                    dy * dy);
        }

        private static Vector3 Cross(
            Vector3 a,
            Vector3 b)
        {
            return new Vector3(
                a.Y * b.Z -
                a.Z * b.Y,
                a.Z * b.X -
                a.X * b.Z,
                a.X * b.Y -
                a.Y * b.X);
        }

        private static double Dot(
            Vector3 a,
            Vector3 b)
        {
            return
                a.X * b.X +
                a.Y * b.Y +
                a.Z * b.Z;
        }

        private static Vector3 Add(
            Vector3 a,
            Vector3 b)
        {
            return new Vector3(
                a.X + b.X,
                a.Y + b.Y,
                a.Z + b.Z);
        }

        private static Vector3 Scale(
            Vector3 value,
            double scale)
        {
            return new Vector3(
                value.X * scale,
                value.Y * scale,
                value.Z * scale);
        }

        private static Vector3 Normalize(
            Vector3 value)
        {
            double length =
                Math.Sqrt(
                    value.X * value.X +
                    value.Y * value.Y +
                    value.Z * value.Z);

            if (length <= 0.000001)
            {
                return value;
            }

            return new Vector3(
                value.X / length,
                value.Y / length,
                value.Z / length);
        }

        private static double NormalizeHeading(
            double value)
        {
            if (!IsFinite(
                    value))
            {
                return 0.0;
            }

            value %=
                360.0;

            if (value < 0.0)
            {
                value +=
                    360.0;
            }

            return value;
        }

        private static double NormalizeSigned180(
            double value)
        {
            if (!IsFinite(
                    value))
            {
                return 0.0;
            }

            value %=
                360.0;

            if (value > 180.0)
            {
                value -=
                    360.0;
            }

            if (value <= -180.0)
            {
                value +=
                    360.0;
            }

            return value;
        }

        private static double NormalizePitch(
            double value)
        {
            if (!IsFinite(
                    value))
            {
                return 0.0;
            }

            return Math.Max(
                -90.0,
                Math.Min(
                    90.0,
                    value));
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private sealed class AttitudeBasis
        {
            public Vector3 Forward;
            public Vector3 Right;
            public Vector3 Up;
            public double HeadingDegrees;
        }

        private struct Vector3
        {
            public Vector3(
                double x,
                double y,
                double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X;
            public double Y;
            public double Z;
        }

        private struct ProjectedPoint
        {
            public bool Visible;
            public PointF Point;
            public float RadiusFraction;
        }
    }
}
