using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.Engine.Analysis;
using KMC.Engine.Orbit;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Ascent;
using KMC.MissionControl.Widgets;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Integrated Engine-owned ORBIT guidance display.
    ///
    /// Build 10.5 consumes the Engine-owned OrbitModel and FlightDirector.
    /// The page is display/advisory only and never commands the vehicle.
    /// </summary>
    public sealed class OrbitPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const int HeaderOffset = 68;
        private const int OuterGap = 14;
        private const int PanelPadding = 14;
        private const int PanelHeaderHeight = 34;
        private const int FieldRowHeight = 24;

        private readonly OrbitPlotWidget
            _orbitPlotWidget =
                new OrbitPlotWidget();

        private readonly NavballRenderer
            _navballRenderer =
                new NavballRenderer();

        public string Name
        {
            get
            {
                return "ORBIT GUIDANCE";
            }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                /*
                 * Match POWER and ASCENT: use the live CRT viewport rather
                 * than forcing ORBIT into a fixed logical aspect ratio.
                 */
                return Size.Empty;
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile
                        .DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout pageLayout =
                new MissionPageLayout(
                    context);

            pageLayout.DrawHeader(
                Name,
                "FDO / GNC");

            OrbitModel orbit =
                GetLatestOrbit();

            Rectangle content =
                context.ContentBounds;

            int top =
                content.Top +
                HeaderOffset;

            int availableHeight =
                Math.Max(
                    1,
                    content.Bottom -
                    top);

            int topHeight =
                Math.Max(
                    300,
                    Math.Min(
                        470,
                        (int)(
                            availableHeight *
                            0.55)));

            if (topHeight >
                availableHeight -
                180)
            {
                topHeight =
                    Math.Max(
                        220,
                        availableHeight -
                        180);
            }

            int bottomTop =
                top +
                topHeight +
                OuterGap;

            int bottomHeight =
                Math.Max(
                    1,
                    content.Bottom -
                    bottomTop -
                    4);

            int leftWidth =
                Math.Max(
                    300,
                    (int)(
                        content.Width *
                        0.27));

            int centerWidth =
                Math.Max(
                    360,
                    (int)(
                        content.Width *
                        0.33));

            int rightWidth =
                content.Width -
                leftWidth -
                centerWidth -
                OuterGap * 2;

            if (rightWidth <
                340)
            {
                int deficit =
                    340 -
                    rightWidth;

                centerWidth =
                    Math.Max(
                        320,
                        centerWidth -
                        deficit);

                rightWidth =
                    content.Width -
                    leftWidth -
                    centerWidth -
                    OuterGap * 2;
            }

            Rectangle stateBounds =
                new Rectangle(
                    content.Left,
                    top,
                    leftWidth,
                    topHeight);

            Rectangle fdaiBounds =
                new Rectangle(
                    stateBounds.Right +
                    OuterGap,
                    top,
                    centerWidth,
                    topHeight);

            Rectangle directorBounds =
                new Rectangle(
                    fdaiBounds.Right +
                    OuterGap,
                    top,
                    Math.Max(
                        1,
                        content.Right -
                        fdaiBounds.Right -
                        OuterGap),
                    topHeight);

            int plotWidth =
                Math.Max(
                    420,
                    (int)(
                        content.Width *
                        0.62));

            Rectangle plotBounds =
                new Rectangle(
                    content.Left,
                    bottomTop,
                    plotWidth,
                    bottomHeight);

            Rectangle burnBounds =
                new Rectangle(
                    plotBounds.Right +
                    OuterGap,
                    bottomTop,
                    Math.Max(
                        1,
                        content.Right -
                        plotBounds.Right -
                        OuterGap),
                    bottomHeight);

            DrawOrbitalStatePanel(
                context,
                stateBounds,
                telemetry,
                orbit);

            DrawFdaiPanel(
                context,
                fdaiBounds,
                telemetry,
                orbit);

            DrawFlightDirectorPanel(
                context,
                directorBounds,
                orbit);

            _orbitPlotWidget.Draw(
                context,
                plotBounds,
                telemetry);

            DrawBurnSafetyPanel(
                context,
                burnBounds,
                orbit);
        }

        private static OrbitModel GetLatestOrbit()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore
                    .TryGetLatest(
                        out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Orbit == null ||
                !result.Snapshot.Orbit.Available)
            {
                return null;
            }

            return
                result.Snapshot.Orbit;
        }

        private static void DrawOrbitalStatePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry,
            OrbitModel orbit)
        {
            DrawPanelFrame(
                context,
                bounds,
                "ORBITAL STATE");

            int y =
                bounds.Top +
                PanelHeaderHeight +
                10;

            int labelX =
                bounds.Left +
                PanelPadding;

            int valueRight =
                bounds.Right -
                PanelPadding;

            DrawField(
                context,
                "ACTUAL AP",
                FormatDistance(
                    telemetry.Apoapsis),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "ACTUAL PE",
                FormatDistance(
                    telemetry.Periapsis),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "TARGET ORBIT",
                orbit != null
                    ? FormatDistance(
                        orbit.TargetOrbitMeters)
                    : "---",
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawDivider(
                context,
                bounds,
                y + 2);

            y += 12;

            CircularizationPredictionModel prediction =
                orbit != null
                    ? orbit.CircularizationPrediction
                    : null;

            DrawField(
                context,
                "PREDICTED AP",
                prediction != null &&
                prediction.Available
                    ? FormatDistance(
                        prediction
                            .PredictedApoapsisMeters)
                    : "---",
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "PREDICTED PE",
                prediction != null &&
                prediction.Available
                    ? FormatDistance(
                        prediction
                            .PredictedPeriapsisMeters)
                    : "---",
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "TIME TO AP",
                FormatDuration(
                    telemetry.TimeToApoapsis),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "TIME TO PE",
                FormatDuration(
                    telemetry.TimeToPeriapsis),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawDivider(
                context,
                bounds,
                y + 2);

            y += 12;

            DrawField(
                context,
                "ORBIT VELOCITY",
                FormatSpeed(
                    telemetry.OrbitalSpeed),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "ECCENTRICITY",
                FormatRatio(
                    telemetry.Eccentricity),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "INCLINATION",
                FormatAngle(
                    telemetry.InclinationDegrees),
                labelX,
                valueRight,
                y);

            y += FieldRowHeight;

            DrawField(
                context,
                "PERIOD",
                FormatDuration(
                    telemetry.OrbitalPeriod),
                labelX,
                valueRight,
                y);
        }

        private void DrawFdaiPanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry,
            OrbitModel orbit)
        {
            DrawPanelFrame(
                context,
                bounds,
                "FDAI / ORBITAL PROGRADE");

            Rectangle inner =
                Rectangle.FromLTRB(
                    bounds.Left +
                    PanelPadding,
                    bounds.Top +
                    PanelHeaderHeight +
                    8,
                    bounds.Right -
                    PanelPadding,
                    bounds.Bottom -
                    PanelPadding);

            int diameter =
                Math.Max(
                    100,
                    Math.Min(
                        inner.Width,
                        inner.Height));

            Rectangle navballBounds =
                new Rectangle(
                    inner.Left +
                    (inner.Width -
                     diameter) /
                    2,
                    inner.Top +
                    (inner.Height -
                     diameter) /
                    2,
                    diameter,
                    diameter);

            NavballRenderModel model =
                new NavballRenderModel
                {
                    PitchDegrees =
                        telemetry.Pitch,

                    HeadingDegrees =
                        telemetry.Heading,

                    RollDegrees =
                        telemetry.Roll,

                    FlightPathAvailable =
                        false,

                    GuidanceAvailable =
                        false,

                    ProgradeReference =
                        "ORBIT"
                };

            OrbitFlightDirectorModel fd =
                orbit != null
                    ? orbit.FlightDirector
                    : null;

            if (fd != null &&
                fd.ProgradeAvailable &&
                IsFinite(
                    fd.OrbitalProgradeMagnitudeMetersPerSecond) &&
                fd.OrbitalProgradeMagnitudeMetersPerSecond >=
                    1.0)
            {
                model.ProgradeAvailable =
                    true;

                model.ProgradeRightMetersPerSecond =
                    fd.OrbitalProgradeRightMetersPerSecond;

                model.ProgradeNoseMetersPerSecond =
                    fd.OrbitalProgradeNoseMetersPerSecond;

                model.ProgradeReferenceForwardMetersPerSecond =
                    fd.OrbitalProgradeReferenceForwardMetersPerSecond;

                model.ProgradeMagnitudeMetersPerSecond =
                    fd.OrbitalProgradeMagnitudeMetersPerSecond;
            }
            else if (orbit != null &&
                     orbit.VelocityVector != null &&
                     orbit.VelocityVector.Available)
            {
                VelocityVectorTelemetryModel vector =
                    orbit.VelocityVector;

                if (IsFinite(
                        vector.OrbitalMagnitudeMetersPerSecond) &&
                    vector.OrbitalMagnitudeMetersPerSecond >=
                        1.0)
                {
                    model.ProgradeAvailable =
                        true;

                    model.ProgradeRightMetersPerSecond =
                        vector.OrbitalRightMetersPerSecond;

                    model.ProgradeNoseMetersPerSecond =
                        vector.OrbitalNoseMetersPerSecond;

                    model.ProgradeReferenceForwardMetersPerSecond =
                        vector.OrbitalReferenceForwardMetersPerSecond;

                    model.ProgradeMagnitudeMetersPerSecond =
                        vector.OrbitalMagnitudeMetersPerSecond;
                }
            }

            _navballRenderer.Draw(
                context,
                navballBounds,
                model);
        }

        private static void DrawFlightDirectorPanel(
            MissionRenderContext context,
            Rectangle bounds,
            OrbitModel orbit)
        {
            GraphicsState graphicsState =
                context.Graphics.Save();

            context.Graphics.SetClip(
                bounds);

            try
            {
                DrawPanelFrame(
                    context,
                    bounds,
                    "ORBIT FLIGHT DIRECTOR");

                OrbitFlightDirectorModel fd =
                orbit != null
                    ? orbit.FlightDirector
                    : null;

            int left =
                bounds.Left +
                PanelPadding;

            int right =
                bounds.Right -
                PanelPadding;

            int y =
                bounds.Top +
                PanelHeaderHeight +
                10;

            string phase =
                fd != null
                    ? fd.FlightPhase
                    : "ORBIT WAITING";

            DrawStatusBand(
                context,
                Rectangle.FromLTRB(
                    left,
                    y,
                    right,
                    y + 34),
                phase,
                GetPhaseColor(
                    fd));

            y += 44;

            DrawLabeledTextBlock(
                context,
                "COMMAND",
                fd != null
                    ? fd.Command
                    : "AWAIT ORBIT HANDOFF",
                left,
                right,
                ref y);

            DrawLabeledTextBlock(
                context,
                "ATTITUDE",
                fd != null
                    ? fd.AttitudeCommand
                    : "HOLD ATTITUDE",
                left,
                right,
                ref y);

            DrawLabeledTextBlock(
                context,
                "THROTTLE",
                fd != null
                    ? fd.ThrottleCommand
                    : "THROTTLE 0%",
                left,
                right,
                ref y);

            DrawLabeledTextBlock(
                context,
                "STATUS",
                fd != null
                    ? fd.Status
                    : "GUIDANCE WAITING",
                left,
                right,
                ref y);

            DrawLabeledTextBlock(
                context,
                "NEXT EVENT",
                fd != null
                    ? fd.NextEvent
                    : "---",
                left,
                right,
                ref y);

            DrawLabeledTextBlock(
                context,
                "SOURCE",
                fd != null
                    ? fd.DecisionSource
                    : "ORBIT FOUNDATION",
                left,
                right,
                ref y);

                if (fd != null &&
                    fd.CutoffRequired)
                {
                    int alertBottom =
                        bounds.Bottom -
                        8;

                    int alertTop =
                        Math.Max(
                            bounds.Top +
                            PanelHeaderHeight +
                            8,
                            alertBottom -
                            34);

                    DrawStatusBand(
                        context,
                        Rectangle.FromLTRB(
                            left,
                            alertTop,
                            right,
                            alertBottom),
                        "CUTOFF REQUIRED",
                        Color.Orange);
                }
            }
            finally
            {
                context.Graphics.Restore(
                    graphicsState);
            }
        }

        private static void DrawBurnSafetyPanel(
            MissionRenderContext context,
            Rectangle bounds,
            OrbitModel orbit)
        {
            GraphicsState graphicsState =
                context.Graphics.Save();

            context.Graphics.SetClip(
                bounds);

            try
            {
                DrawPanelFrame(
                    context,
                    bounds,
                    "BURN / SAFETY");

                OrbitFlightDirectorModel fd =
                    orbit != null
                        ? orbit.FlightDirector
                        : null;

                OrbitSafetyModel safety =
                    orbit != null
                        ? orbit.Safety
                        : null;

                PeriapsisRecoveryModel recovery =
                    orbit != null
                        ? orbit.PeriapsisRecovery
                        : null;

                int innerLeft =
                    bounds.Left +
                    PanelPadding;

                int innerRight =
                    bounds.Right -
                    PanelPadding;

                int innerTop =
                    bounds.Top +
                    PanelHeaderHeight +
                    8;

                int innerBottom =
                    bounds.Bottom -
                    PanelPadding;

                int columnGap =
                    18;

                int usableWidth =
                    Math.Max(
                        2,
                        innerRight -
                        innerLeft -
                        columnGap);

                int leftWidth =
                    usableWidth /
                    2;

                Rectangle leftColumn =
                    Rectangle.FromLTRB(
                        innerLeft,
                        innerTop,
                        innerLeft +
                        leftWidth,
                        innerBottom);

                Rectangle rightColumn =
                    Rectangle.FromLTRB(
                        leftColumn.Right +
                        columnGap,
                        innerTop,
                        innerRight,
                        innerBottom);

                DrawSubsectionTitle(
                    context,
                    leftColumn,
                    "BURN SOLUTION");

                DrawSubsectionTitle(
                    context,
                    rightColumn,
                    "SAFETY STATE");

                int leftY =
                    leftColumn.Top +
                    29;

                int rightY =
                    rightColumn.Top +
                    29;

                DrawCompactField(
                    context,
                    "IGNITION",
                    fd != null
                        ? FormatSignedDuration(
                            fd.IgnitionInSeconds)
                        : "---",
                    leftColumn.Left,
                    leftColumn.Right,
                    leftY);

                leftY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "BURN TIME",
                    fd != null
                        ? FormatDuration(
                            fd.BurnTimeSeconds)
                        : "---",
                    leftColumn.Left,
                    leftColumn.Right,
                    leftY);

                leftY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "REMAINING DV",
                    fd != null
                        ? FormatSpeed(
                            fd.RemainingDeltaVMetersPerSecond)
                        : "---",
                    leftColumn.Left,
                    leftColumn.Right,
                    leftY);

                leftY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "COMPLETE",
                    fd != null &&
                    IsFinite(
                        fd.BurnCompletionPercent)
                        ? fd.BurnCompletionPercent
                            .ToString("0.0") +
                          "%"
                        : "---",
                    leftColumn.Left,
                    leftColumn.Right,
                    leftY);

                leftY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "THROTTLE CMD",
                    fd != null &&
                    IsFinite(
                        fd.ThrottleCommandPercent)
                        ? fd.ThrottleCommandPercent
                            .ToString("0") +
                          "%"
                        : "---",
                    leftColumn.Left,
                    leftColumn.Right,
                    leftY);

                bool livePeriapsisSafe =
                    safety != null &&
                    safety.Available
                        ? safety.ActualPeriapsisSafe
                        : orbit != null &&
                          orbit.Current != null &&
                          orbit.Current.Available &&
                          IsFinite(
                              orbit.Current
                                  .PeriapsisMeters) &&
                          orbit.Current
                              .PeriapsisMeters >=
                              70000.0;

                bool predictedPeriapsisSafe =
                    safety != null &&
                    safety.Available
                        ? safety.PredictedPeriapsisSafe
                        : orbit != null &&
                          orbit.CircularizationPrediction != null &&
                          orbit.CircularizationPrediction.Available &&
                          IsFinite(
                              orbit.CircularizationPrediction
                                  .PredictedPeriapsisMeters) &&
                          orbit.CircularizationPrediction
                              .PredictedPeriapsisMeters >=
                              70000.0;

                DrawCompactField(
                    context,
                    "LIVE PE SAFE",
                    FormatBool(
                        livePeriapsisSafe),
                    rightColumn.Left,
                    rightColumn.Right,
                    rightY);

                rightY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "PRED PE SAFE",
                    FormatBool(
                        predictedPeriapsisSafe),
                    rightColumn.Left,
                    rightColumn.Right,
                    rightY);

                rightY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "RECOVERY",
                    recovery != null &&
                    recovery.Active
                        ? "ACTIVE"
                        : "INACTIVE",
                    rightColumn.Left,
                    rightColumn.Right,
                    rightY);

                rightY +=
                    FieldRowHeight;

                DrawCompactField(
                    context,
                    "ORBIT CUTOFF",
                    FormatBool(
                        safety != null &&
                        safety.CutoffLatched),
                    rightColumn.Left,
                    rightColumn.Right,
                    rightY);

                bool reacquiredOrbit =
                    fd != null &&
                    string.Equals(
                        fd.DecisionSource,
                        "ORBIT REACQUISITION",
                        StringComparison.Ordinal);

                string reason =
                    reacquiredOrbit &&
                    !string.IsNullOrWhiteSpace(
                        fd.Status)
                        ? fd.Status
                        : safety != null &&
                          !string.IsNullOrWhiteSpace(
                              safety.Reason)
                            ? safety.Reason
                            : "SAFETY WAITING";

                int decisionHeight =
                    58;

                int decisionTop =
                    Math.Max(
                        Math.Max(
                            leftY,
                            rightY) +
                        FieldRowHeight +
                        8,
                        innerBottom -
                        decisionHeight);

                if (decisionTop <
                    innerBottom)
                {
                    DrawSafetyDecisionBand(
                        context,
                        Rectangle.FromLTRB(
                            innerLeft,
                            decisionTop,
                            innerRight,
                            innerBottom),
                        reason,
                        safety,
                        fd);
                }
            }
            finally
            {
                context.Graphics.Restore(
                    graphicsState);
            }
        }

        private static void DrawSubsectionTitle(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    brush,
                    bounds.Left,
                    bounds.Top);
            }
        }

        private static void DrawCompactField(
            MissionRenderContext context,
            string label,
            string value,
            int left,
            int right,
            int y)
        {
            int width =
                Math.Max(
                    1,
                    right -
                    left);

            int valueWidth =
                Math.Max(
                    72,
                    (int)(
                        width *
                        0.38));

            RectangleF labelBounds =
                new RectangleF(
                    left,
                    y,
                    Math.Max(
                        1,
                        width -
                        valueWidth -
                        8),
                    context.SmallFont.Height +
                    3);

            RectangleF valueBounds =
                new RectangleF(
                    right -
                    valueWidth,
                    y,
                    valueWidth,
                    context.SmallFont.Height +
                    3);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat valueFormat =
                new StringFormat())
            {
                valueFormat.Alignment =
                    StringAlignment.Far;

                valueFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    valueFormat);
            }
        }

        private static void DrawSafetyDecisionBand(
            MissionRenderContext context,
            Rectangle bounds,
            string reason,
            OrbitSafetyModel safety,
            OrbitFlightDirectorModel fd)
        {
            bool cutoff =
                safety != null &&
                safety.CutoffLatched;

            bool pause =
                safety != null &&
                safety.PauseBurn;

            bool reacquired =
                fd != null &&
                string.Equals(
                    fd.DecisionSource,
                    "ORBIT REACQUISITION",
                    StringComparison.Ordinal);

            Color stateColor =
                cutoff
                    ? Color.Orange
                    : pause
                        ? Color.Gold
                        : reacquired &&
                          fd.OrbitAchieved
                            ? Color.LightGreen
                            : reacquired
                                ? Color.Gold
                                : Color.LightSkyBlue;

            using (SolidBrush fillBrush =
                new SolidBrush(
                    Color.FromArgb(
                        34,
                        stateColor)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        155,
                        stateColor),
                    1.0f))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    stateColor))
            {
                context.Graphics.FillRectangle(
                    fillBrush,
                    bounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    bounds);

                context.Graphics.DrawString(
                    "SAFETY DECISION",
                    context.SmallFont,
                    labelBrush,
                    bounds.Left +
                    8,
                    bounds.Top +
                    6);

                int reasonY =
                    bounds.Top +
                    context.SmallFont.Height +
                    13;

                context.Graphics.DrawString(
                    string.IsNullOrWhiteSpace(
                        reason)
                        ? "---"
                        : reason,
                    context.SmallFont,
                    valueBrush,
                    bounds.Left +
                    8,
                    reasonY);
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        72,
                        3,
                        17,
                        23)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        155,
                        context.DimPhosphorColor),
                    2.0f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.FillRectangle(
                    backgroundBrush,
                    bounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    bounds);

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left +
                    PanelPadding,
                    bounds.Top +
                    7);

                int dividerY =
                    bounds.Top +
                    PanelHeaderHeight;

                context.Graphics.DrawLine(
                    borderPen,
                    bounds.Left +
                    PanelPadding,
                    dividerY,
                    bounds.Right -
                    PanelPadding,
                    dividerY);
            }
        }

        private static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int left,
            int right,
            int y)
        {
            int width =
                Math.Max(
                    1,
                    right -
                    left);

            int valueWidth =
                Math.Max(
                    110,
                    (int)(
                        width *
                        0.47));

            RectangleF labelBounds =
                new RectangleF(
                    left,
                    y,
                    Math.Max(
                        1,
                        width -
                        valueWidth -
                        8),
                    context.SmallFont.Height +
                    4);

            RectangleF valueBounds =
                new RectangleF(
                    right -
                    valueWidth,
                    y,
                    valueWidth,
                    context.SmallFont.Height +
                    4);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat valueFormat =
                new StringFormat())
            {
                valueFormat.Alignment =
                    StringAlignment.Far;

                valueFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    valueFormat);
            }
        }

        private static void DrawLabeledTextBlock(
            MissionRenderContext context,
            string label,
            string value,
            int left,
            int right,
            ref int y)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat valueFormat =
                new StringFormat())
            {
                valueFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                valueFormat.Trimming =
                    StringTrimming.EllipsisCharacter;

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    left,
                    y);

                y +=
                    context.SmallFont.Height +
                    1;

                RectangleF valueBounds =
                    new RectangleF(
                        left,
                        y,
                        Math.Max(
                            1,
                            right -
                            left),
                        context.SmallFont.Height +
                        3);

                context.Graphics.DrawString(
                    string.IsNullOrWhiteSpace(
                        value)
                        ? "---"
                        : value,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    valueFormat);

                y +=
                    context.SmallFont.Height +
                    9;
            }
        }

        private static void DrawStatusBand(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            using (SolidBrush fillBrush =
                new SolidBrush(
                    Color.FromArgb(
                        42,
                        color)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        190,
                        color),
                    1.0f))
            using (SolidBrush textBrush =
                new SolidBrush(
                    color))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                format.FormatFlags =
                    StringFormatFlags.NoWrap;

                context.Graphics.FillRectangle(
                    fillBrush,
                    bounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    bounds);

                context.Graphics.DrawString(
                    string.IsNullOrWhiteSpace(
                        text)
                        ? "---"
                        : text,
                    context.SmallFont,
                    textBrush,
                    bounds,
                    format);
            }
        }

        private static void DrawDivider(
            MissionRenderContext context,
            Rectangle bounds,
            int y)
        {
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawLine(
                    pen,
                    bounds.Left +
                    PanelPadding,
                    y,
                    bounds.Right -
                    PanelPadding,
                    y);
            }
        }

        private static Color GetPhaseColor(
            OrbitFlightDirectorModel fd)
        {
            if (fd == null)
            {
                return Color.Gray;
            }

            if (fd.CutoffRequired ||
                fd.OrbitAchieved)
            {
                return Color.Orange;
            }

            if (fd.PeriapsisRecoveryActive ||
                string.Equals(
                    fd.FlightPhase,
                    "SAFE ORBIT",
                    StringComparison.Ordinal))
            {
                return Color.Gold;
            }

            if (fd.IgnitionDue ||
                fd.CircularizationStarted)
            {
                return Color.LightGreen;
            }

            return Color.LightSkyBlue;
        }

        private static string FormatDistance(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double absolute =
                Math.Abs(value);

            if (absolute >=
                1000000.0)
            {
                return
                    (value /
                     1000000.0)
                    .ToString("0.00") +
                    " MM";
            }

            if (absolute >=
                1000.0)
            {
                return
                    (value /
                     1000.0)
                    .ToString("0.0") +
                    " KM";
            }

            return
                value.ToString("N0") +
                " M";
        }

        private static string FormatSpeed(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString("N1") +
                " M/S";
        }

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString("0.00000");
        }

        private static string FormatAngle(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString("0.00") +
                "°";
        }

        private static string FormatDuration(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds <
                    0.0)
            {
                return "---";
            }

            int hours =
                (int)(
                    totalSeconds /
                    3600.0);

            int minutes =
                (int)(
                    totalSeconds %
                    3600.0) /
                60;

            int seconds =
                (int)(
                    totalSeconds %
                    60.0);

            if (hours >
                0)
            {
                return
                    string.Format(
                        "{0:00}:{1:00}:{2:00}",
                        hours,
                        minutes,
                        seconds);
            }

            return
                string.Format(
                    "{0:00}:{1:00}",
                    minutes,
                    seconds);
        }

        private static string FormatSignedDuration(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            if (Math.Abs(seconds) >=
                60.0)
            {
                string sign =
                    seconds <
                    0.0
                        ? "-"
                        : "";

                return
                    sign +
                    FormatDuration(
                        Math.Abs(
                            seconds));
            }

            return
                seconds.ToString(
                    "+0.0;-0.0;0.0") +
                " S";
        }

        private static string FormatBool(
            bool value)
        {
            return
                value
                    ? "YES"
                    : "NO";
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
