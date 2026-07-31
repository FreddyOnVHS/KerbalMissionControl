using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Widgets;
using System;
using System.Drawing;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Hybrid propulsion display: precise engine and thrust values remain
    /// text, while resource quantities use reusable horizontal bar widgets.
    /// </summary>
    public sealed class PropulsionPage : IMissionPage
    {
        private const int SectionGap = 14;
        private const int RowHeight = 34;
        private const int PanelPadding = 22;

        private readonly IMissionWidget[] _resourceWidgets;

        public PropulsionPage()
        {
            _resourceWidgets =
                new IMissionWidget[]
                {
                    new HorizontalResourceBarWidget(
                        "STAGE LIQUID FUEL",
                        telemetry => telemetry.StageLiquidFuelAmount,
                        telemetry => telemetry.StageLiquidFuelCapacity),

                    new HorizontalResourceBarWidget(
                        "STAGE OXIDIZER",
                        telemetry => telemetry.StageOxidizerAmount,
                        telemetry => telemetry.StageOxidizerCapacity),

                    new HorizontalResourceBarWidget(
                        "STAGE MONOPROP",
                        telemetry => telemetry.StageMonopropellantAmount,
                        telemetry => telemetry.StageMonopropellantCapacity),

                    new HorizontalResourceBarWidget(
                        "TOTAL LIQUID FUEL",
                        telemetry => telemetry.TotalLiquidFuelAmount,
                        telemetry => telemetry.TotalLiquidFuelCapacity),

                    new HorizontalResourceBarWidget(
                        "TOTAL OXIDIZER",
                        telemetry => telemetry.TotalOxidizerAmount,
                        telemetry => telemetry.TotalOxidizerCapacity),

                    new HorizontalResourceBarWidget(
                        "TOTAL MONOPROP",
                        telemetry => telemetry.TotalMonopropellantAmount,
                        telemetry => telemetry.TotalMonopropellantCapacity)
                };
        }

        public string Name
        {
            get { return "PROPULSION DATA"; }
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
                "CH 04");

            Rectangle workingBounds =
                new Rectangle(
                    context.ContentBounds.Left + 28,
                    context.ContentBounds.Top + 92,
                    context.ContentBounds.Width - 56,
                    context.ContentBounds.Height - 112);

            int columnGap = 34;

            int leftWidth =
                Math.Min(
                    520,
                    (workingBounds.Width - columnGap) / 2);

            Rectangle leftPanel =
                new Rectangle(
                    workingBounds.Left,
                    workingBounds.Top,
                    leftWidth,
                    workingBounds.Height);

            Rectangle rightPanel =
                new Rectangle(
                    leftPanel.Right + columnGap,
                    workingBounds.Top,
                    Math.Max(
                        0,
                        workingBounds.Right -
                        leftPanel.Right -
                        columnGap),
                    workingBounds.Height);

            DrawEnginePanel(
                context,
                leftPanel,
                telemetry);

            DrawResourcePanel(
                context,
                rightPanel,
                telemetry);
        }

        private static void DrawEnginePanel(
    MissionRenderContext context,
    Rectangle bounds,
    MissionTelemetry telemetry)
        {
            DrawPanelFrame(
                context,
                bounds,
                "ENGINE / THRUST");

            int labelX =
                bounds.Left + PanelPadding;

            int valueX =
                bounds.Left +
                Math.Max(
                    220,
                    bounds.Width / 2);

            const int rowCount = 14;
            const int sectionCount = 2;

            int contentTop =
                bounds.Top + 58;

            int contentBottom =
                bounds.Bottom - 18;

            int availableHeight =
                Math.Max(
                    1,
                    contentBottom - contentTop);

            int sectionGap =
                Math.Min(
                    SectionGap,
                    Math.Max(
                        4,
                        availableHeight / 30));

            int availableRowHeight =
                Math.Max(
                    1,
                    availableHeight -
                    sectionGap * sectionCount);

            int rowHeight =
                Math.Max(
                    1,
                    Math.Min(
                        RowHeight,
                        availableRowHeight /
                        rowCount));

            int y = contentTop;

            DrawTextRow(
                context,
                "STAGE",
                telemetry.CurrentStage.ToString("00"),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "THROTTLE",
                FormatPercent(
                    telemetry.Throttle),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "VESSEL MASS",
                FormatMass(
                    telemetry.VesselMass),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "TWR",
                FormatRatio(
                    telemetry.ThrustToWeightRatio),
                labelX,
                valueX,
                y);

            y += rowHeight +
                sectionGap;

            DrawTextRow(
                context,
                "CURRENT THRUST",
                FormatThrust(
                    telemetry.CurrentThrust),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "MAX THRUST",
                FormatThrust(
                    telemetry.MaximumThrust),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "THRUST LOAD",
                FormatThrustLoad(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "THRUST MARGIN",
                FormatThrustMargin(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust),
                labelX,
                valueX,
                y);

            y += rowHeight +
                sectionGap;

            DrawTextRow(
                context,
                "ENGINES",
                telemetry.EngineCount.ToString("00"),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "IGNITED",
                telemetry.IgnitedEngineCount.ToString("00"),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "PRODUCING",
                telemetry
                    .ProducingThrustEngineCount
                    .ToString("00"),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "FLAMEOUTS",
                telemetry.FlameoutEngineCount.ToString("00"),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "AVG ISP",
                FormatSpecificImpulse(
                    telemetry.AverageSpecificImpulse),
                labelX,
                valueX,
                y);

            y += rowHeight;

            DrawTextRow(
                context,
                "ENGINE STATUS",
                GetEngineStatus(
                    telemetry),
                labelX,
                valueX,
                y);
        }

        private void DrawResourcePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            DrawPanelFrame(
                context,
                bounds,
                "PROPELLANT");

            Rectangle contentBounds =
                new Rectangle(
                    bounds.Left + PanelPadding,
                    bounds.Top + 58,
                    Math.Max(
                        0,
                        bounds.Width -
                        PanelPadding * 2),
                    Math.Max(
                        0,
                        bounds.Height - 78));

            if (contentBounds.Width <= 0 ||
                contentBounds.Height <= 0)
            {
                return;
            }

            int widgetGap =
                Math.Min(
                    12,
                Math.Max(
                    4,
                contentBounds.Height / 50));

            int availableWidgetHeight =
                Math.Max(
                    1,
                    contentBounds.Height -
                    widgetGap *
                    (_resourceWidgets.Length - 1));

            int widgetHeight =
                Math.Max(
                    1,
                    availableWidgetHeight /
                    _resourceWidgets.Length);

            int y =
                contentBounds.Top;

            for (int index = 0;
     index < _resourceWidgets.Length;
     index++)
            {
                IMissionWidget widget =
                    _resourceWidgets[index];

                int remainingHeight =
                    Math.Max(
                        1,
                        contentBounds.Bottom - y);

                int currentWidgetHeight =
                    index ==
                    _resourceWidgets.Length - 1
                        ? remainingHeight
                        : Math.Min(
                            widgetHeight,
                            remainingHeight);

                Rectangle widgetBounds =
                    new Rectangle(
                        contentBounds.Left,
                        y,
                        contentBounds.Width,
                        currentWidgetHeight);

                widget.Draw(
                    context,
                    widgetBounds,
                    telemetry);

                y +=
                    currentWidgetHeight +
                    widgetGap;
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        72,
                        3,
                        18,
                        23)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        135,
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
                    bounds.Left + PanelPadding,
                    bounds.Top + 14);

                int dividerY =
                    bounds.Top + 48;

                context.Graphics.DrawLine(
                    borderPen,
                    bounds.Left + PanelPadding,
                    dividerY,
                    bounds.Right - PanelPadding,
                    dividerY);
            }
        }

        private static void DrawTextRow(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelX,
                    y);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueX,
                    y);
            }
        }

        private static string GetEngineStatus(
            MissionTelemetry telemetry)
        {
            if (telemetry.EngineCount <= 0)
            {
                return "NO ENGINES";
            }

            if (telemetry.FlameoutEngineCount > 0)
            {
                return "FLAMEOUT";
            }

            if (telemetry.ProducingThrustEngineCount > 0)
            {
                if (telemetry.ProducingThrustEngineCount ==
                    telemetry.IgnitedEngineCount)
                {
                    return "GO";
                }

                return "PARTIAL";
            }

            if (telemetry.IgnitedEngineCount > 0)
            {
                return "ARMED";
            }

            return "STANDBY";
        }

        private static string FormatSpecificImpulse(
            double seconds)
        {
            if (!IsFinite(seconds) ||
                seconds <= 0.0)
            {
                return "---";
            }

            return
                seconds.ToString("0.0") +
                " S";
        }

        private static string FormatMass(
            double tonnes)
        {
            if (!IsFinite(tonnes))
            {
                return "---";
            }

            return
                Math.Max(
                    0.0,
                    tonnes)
                .ToString("0.0") +
                " T";
        }

        private static string FormatThrust(
            double kilonewtons)
        {
            if (!IsFinite(kilonewtons))
            {
                return "---";
            }

            return
                Math.Max(
                    0.0,
                    kilonewtons)
                .ToString("0.0") +
                " KN";
        }

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                Math.Max(
                    0.0,
                    value)
                .ToString("0.00");
        }

        private static string FormatPercent(
            double fraction)
        {
            if (!IsFinite(fraction))
            {
                return "---";
            }

            double percent =
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        fraction * 100.0));

            return
                percent.ToString("0") +
                "%";
        }

        private static string FormatThrustLoad(
            double currentThrust,
            double maximumThrust)
        {
            if (!IsFinite(currentThrust) ||
                !IsFinite(maximumThrust) ||
                maximumThrust <= 0.0)
            {
                return "---";
            }

            double percent =
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        currentThrust /
                        maximumThrust *
                        100.0));

            return
                percent.ToString("0.0") +
                "%";
        }

        private static string FormatThrustMargin(
            double currentThrust,
            double maximumThrust)
        {
            if (!IsFinite(currentThrust) ||
                !IsFinite(maximumThrust))
            {
                return "---";
            }

            double margin =
                Math.Max(
                    0.0,
                    maximumThrust -
                    currentThrust);

            return
                margin.ToString("0.0") +
                " KN";
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
