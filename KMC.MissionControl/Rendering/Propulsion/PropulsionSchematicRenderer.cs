using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// MOCR-style propulsion system diagram. This renderer intentionally
    /// displays grouped systems instead of every individual KSP part.
    /// </summary>
    public sealed class PropulsionSchematicRenderer
    {
        private readonly PropulsionSystemModelBuilder _modelBuilder =
            new PropulsionSystemModelBuilder();

        public void Draw(
            Graphics graphics,
            Rectangle bounds,
            PropulsionRenderGraph graph,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            if (graphics == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            GraphicsState state =
                graphics.Save();

            try
            {
                graphics.SmoothingMode =
                    SmoothingMode.AntiAlias;

                DrawBackground(
                    graphics,
                    bounds,
                    dimPhosphor);

                if (graph == null)
                {
                    DrawCenteredText(
                        graphics,
                        bounds,
                        "AWAITING VESSEL TOPOLOGY",
                        labelFont,
                        dimPhosphor);
                    return;
                }

                PropulsionSystemModel model =
                    _modelBuilder.Build(graph);

                DrawSystem(
                    graphics,
                    bounds,
                    model,
                    telemetry,
                    labelFont,
                    smallFont,
                    phosphor,
                    dimPhosphor);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawSystem(
            Graphics graphics,
            Rectangle bounds,
            PropulsionSystemModel model,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            int centerX =
                bounds.Left +
                bounds.Width / 2;

            int top =
                bounds.Top + 12;

            Rectangle command =
                new Rectangle(
                    centerX - 70,
                    top,
                    140,
                    38);

            DrawBox(
                graphics,
                command,
                "COMMAND",
                model.CommandCount > 0
                    ? model.CommandCount + " UNIT"
                    : "CONTROL",
                Color.White,
                labelFont,
                smallFont);

            int enginesY =
                command.Bottom + 34;

            DrawEngineGroups(
                graphics,
                bounds,
                enginesY,
                model,
                telemetry,
                labelFont,
                smallFont,
                dimPhosphor);

            int manifoldY =
                enginesY + 105;

            Rectangle fuelTank =
                new Rectangle(
                    bounds.Left + 28,
                    manifoldY + 60,
                    138,
                    76);

            Rectangle oxidizerTank =
                new Rectangle(
                    bounds.Right - 166,
                    manifoldY + 60,
                    138,
                    76);

            Rectangle monoTank =
                new Rectangle(
                    bounds.Right - 166,
                    manifoldY + 184,
                    138,
                    68);

            Rectangle mixer =
                new Rectangle(
                    centerX - 48,
                    manifoldY + 106,
                    96,
                    36);

            Rectangle mainValve =
                new Rectangle(
                    centerX - 42,
                    manifoldY + 170,
                    84,
                    34);

            Rectangle chamber =
                new Rectangle(
                    centerX - 62,
                    manifoldY + 230,
                    124,
                    55);

            double lfFraction =
                Fraction(
                    telemetry != null
                        ? telemetry.TotalLiquidFuelAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry.TotalLiquidFuelCapacity
                        : 0.0);

            double oxFraction =
                Fraction(
                    telemetry != null
                        ? telemetry.TotalOxidizerAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry.TotalOxidizerCapacity
                        : 0.0);

            double monoFraction =
                Fraction(
                    telemetry != null
                        ? telemetry
                            .TotalMonopropellantAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry
                            .TotalMonopropellantCapacity
                        : 0.0);

            Color liquidFuel =
                Color.FromArgb(
                    255,
                    45,
                    225,
                    85);

            Color oxidizer =
                Color.FromArgb(
                    255,
                    35,
                    205,
                    255);

            Color monoprop =
                Color.FromArgb(
                    255,
                    255,
                    145,
                    30);

            Color electrical =
                Color.FromArgb(
                    255,
                    245,
                    215,
                    55);

            DrawTank(
                graphics,
                fuelTank,
                "LIQUID FUEL",
                lfFraction,
                model.HasLiquidFuel,
                liquidFuel,
                labelFont,
                smallFont);

            DrawTank(
                graphics,
                oxidizerTank,
                "OXIDIZER",
                oxFraction,
                model.HasOxidizer,
                oxidizer,
                labelFont,
                smallFont);

            DrawTank(
                graphics,
                monoTank,
                "MONOPROP",
                monoFraction,
                model.HasMonopropellant,
                monoprop,
                labelFont,
                smallFont);

            Rectangle fuelPump =
                CircleRect(
                    fuelTank.Right + 24,
                    mixer.Top - 7,
                    24);

            Rectangle oxPump =
                CircleRect(
                    oxidizerTank.Left - 48,
                    mixer.Top - 7,
                    24);

            DrawPump(
                graphics,
                fuelPump,
                "LF",
                liquidFuel,
                smallFont);

            DrawPump(
                graphics,
                oxPump,
                "OX",
                oxidizer,
                smallFont);

            DrawFlow(
                graphics,
                new Point(
                    fuelTank.Right,
                    fuelTank.Top +
                    fuelTank.Height / 2),
                new Point(
                    fuelPump.Left,
                    fuelPump.Top +
                    fuelPump.Height / 2),
                liquidFuel);

            DrawFlow(
                graphics,
                new Point(
                    fuelPump.Right,
                    fuelPump.Top +
                    fuelPump.Height / 2),
                new Point(
                    mixer.Left,
                    mixer.Top +
                    mixer.Height / 2),
                liquidFuel);

            DrawFlow(
                graphics,
                new Point(
                    oxidizerTank.Left,
                    oxidizerTank.Top +
                    oxidizerTank.Height / 2),
                new Point(
                    oxPump.Right,
                    oxPump.Top +
                    oxPump.Height / 2),
                oxidizer);

            DrawFlow(
                graphics,
                new Point(
                    oxPump.Left,
                    oxPump.Top +
                    oxPump.Height / 2),
                new Point(
                    mixer.Right,
                    mixer.Top +
                    mixer.Height / 2),
                oxidizer);

            DrawBox(
                graphics,
                mixer,
                "MIXER",
                "LF / OX",
                phosphor,
                labelFont,
                smallFont);

            DrawFlow(
                graphics,
                new Point(
                    centerX,
                    mixer.Bottom),
                new Point(
                    centerX,
                    mainValve.Top),
                liquidFuel);

            DrawBox(
                graphics,
                mainValve,
                "MAIN VALVE",
                "OPEN",
                phosphor,
                smallFont,
                smallFont);

            DrawFlow(
                graphics,
                new Point(
                    centerX,
                    mainValve.Bottom),
                new Point(
                    centerX,
                    chamber.Top),
                liquidFuel);

            DrawBox(
                graphics,
                chamber,
                "THRUST CHAMBER",
                model.TotalEngineCount +
                " ENGINE" +
                (model.TotalEngineCount == 1
                    ? string.Empty
                    : "S"),
                Color.White,
                labelFont,
                smallFont);

            DrawNozzle(
                graphics,
                chamber,
                phosphor);

            if (model.HasMonopropellant)
            {
                Rectangle monoValve =
                    new Rectangle(
                        monoTank.Left - 90,
                        monoTank.Top + 18,
                        66,
                        30);

                DrawBox(
                    graphics,
                    monoValve,
                    "RCS",
                    model.RcsThrusterCount +
                    " JETS",
                    monoprop,
                    smallFont,
                    smallFont);

                DrawFlow(
                    graphics,
                    new Point(
                        monoTank.Left,
                        monoTank.Top +
                        monoTank.Height / 2),
                    new Point(
                        monoValve.Right,
                        monoValve.Top +
                        monoValve.Height / 2),
                    monoprop);
            }

            Rectangle electricalBus =
                new Rectangle(
                    fuelTank.Left,
                    chamber.Top + 10,
                    92,
                    42);

            DrawBox(
                graphics,
                electricalBus,
                "ELEC BUS",
                model.BatteryCount +
                " BAT",
                electrical,
                smallFont,
                smallFont);

            using (Pen electricalPen =
                new Pen(
                    electrical,
                    1.4f))
            {
                electricalPen.DashStyle =
                    DashStyle.Dash;

                graphics.DrawLine(
                    electricalPen,
                    electricalBus.Right,
                    electricalBus.Top +
                    electricalBus.Height / 2,
                    mainValve.Left,
                    mainValve.Top +
                    mainValve.Height / 2);
            }

            DrawStageRail(
                graphics,
                bounds,
                model,
                smallFont,
                dimPhosphor);
        }

        private static void DrawEngineGroups(
            Graphics graphics,
            Rectangle bounds,
            int y,
            PropulsionSystemModel model,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color dimPhosphor)
        {
            int groupCount =
                Math.Max(
                    1,
                    Math.Min(
                        6,
                        model.EngineGroups.Count));

            int diameter = 48;
            int gap = 22;
            int total =
                groupCount * diameter +
                Math.Max(0, groupCount - 1) *
                gap;

            int startX =
                bounds.Left +
                (bounds.Width - total) / 2;

            for (int index = 0;
                 index < groupCount;
                 index++)
            {
                PropulsionEngineGroup group =
                    model.EngineGroups.Count > index
                        ? model.EngineGroups[index]
                        : null;

                Rectangle circle =
                    new Rectangle(
                        startX +
                        index *
                        (diameter + gap),
                        y,
                        diameter,
                        diameter);

                Color color =
                    IsGroupLikelyActive(
                        group,
                        model.CurrentStage)
                        ? Color.FromArgb(
                            255,
                            75,
                            255,
                            110)
                        : dimPhosphor;

                using (Pen pen =
                    new Pen(color, 1.8f))
                using (SolidBrush titleBrush =
                    new SolidBrush(color))
                using (SolidBrush detailBrush =
                    new SolidBrush(dimPhosphor))
                using (StringFormat centered =
                    new StringFormat
                    {
                        Alignment =
                            StringAlignment.Center,
                        LineAlignment =
                            StringAlignment.Center
                    })
                {
                    graphics.DrawEllipse(
                        pen,
                        circle);

                    string count =
                        group != null
                            ? group.Count.ToString()
                            : "0";

                    graphics.DrawString(
                        count,
                        labelFont,
                        titleBrush,
                        circle,
                        centered);

                    string name =
                        group != null
                            ? Shorten(
                                group.DisplayName,
                                10)
                            : "NO ENGINE";

                    Rectangle nameBounds =
                        new Rectangle(
                            circle.Left - 20,
                            circle.Bottom + 3,
                            circle.Width + 40,
                            18);

                    graphics.DrawString(
                        name,
                        smallFont,
                        titleBrush,
                        nameBounds,
                        centered);

                    Rectangle stageBounds =
                        new Rectangle(
                            circle.Left - 20,
                            circle.Bottom + 19,
                            circle.Width + 40,
                            16);

                    graphics.DrawString(
                        group != null &&
                        group.ActivationStage >= 0
                            ? "STG " +
                              group.ActivationStage
                                  .ToString("00")
                            : "---",
                        smallFont,
                        detailBrush,
                        stageBounds,
                        centered);
                }
            }

            int busY =
                y + diameter + 44;

            using (Pen busPen =
                new Pen(
                    Color.FromArgb(
                        210,
                        45,
                        225,
                        85),
                    1.8f))
            {
                graphics.DrawLine(
                    busPen,
                    startX +
                    diameter / 2,
                    busY,
                    startX +
                    (groupCount - 1) *
                    (diameter + gap) +
                    diameter / 2,
                    busY);

                for (int index = 0;
                     index < groupCount;
                     index++)
                {
                    int centerX =
                        startX +
                        index *
                        (diameter + gap) +
                        diameter / 2;

                    graphics.DrawLine(
                        busPen,
                        centerX,
                        y + diameter,
                        centerX,
                        busY);
                }
            }
        }

        private static void DrawStageRail(
            Graphics graphics,
            Rectangle bounds,
            PropulsionSystemModel model,
            Font font,
            Color color)
        {
            int x =
                bounds.Right - 48;

            int y =
                bounds.Top + 16;

            using (SolidBrush brush =
                new SolidBrush(color))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        130,
                        color)))
            {
                graphics.DrawString(
                    "STAGES",
                    font,
                    brush,
                    x - 10,
                    y);

                y += 23;

                int count =
                    Math.Min(
                        6,
                        model.SeparationStages.Count);

                for (int index = 0;
                     index < count;
                     index++)
                {
                    int stage =
                        model.SeparationStages[index];

                    graphics.DrawLine(
                        pen,
                        x,
                        y + 7,
                        bounds.Right - 8,
                        y + 7);

                    graphics.DrawString(
                        stage.ToString("00"),
                        font,
                        brush,
                        x - 25,
                        y);

                    y += 25;
                }
            }
        }

        private static bool IsGroupLikelyActive(
            PropulsionEngineGroup group,
            int currentStage)
        {
            if (group == null ||
                group.ActivationStage < 0)
            {
                return false;
            }

            return group.ActivationStage ==
                currentStage ||
                group.ActivationStage ==
                currentStage - 1;
        }

        private static void DrawTank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            double fraction,
            bool present,
            Color color,
            Font labelFont,
            Font smallFont)
        {
            Color actual =
                present
                    ? color
                    : Color.FromArgb(
                        100,
                        color);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        175,
                        3,
                        16,
                        22)))
            using (SolidBrush level =
                new SolidBrush(
                    Color.FromArgb(
                        55,
                        actual)))
            using (Pen pen =
                new Pen(actual, 1.6f))
            using (SolidBrush text =
                new SolidBrush(actual))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                Rectangle levelBounds =
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Bottom -
                        2 -
                        (int)
                        ((bounds.Height - 4) *
                         fraction),
                        bounds.Width - 4,
                        (int)
                        ((bounds.Height - 4) *
                         fraction));

                graphics.FillRectangle(
                    level,
                    levelBounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    title,
                    labelFont,
                    text,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 8,
                        bounds.Width,
                        22),
                    centered);

                graphics.DrawString(
                    (fraction * 100.0)
                        .ToString("0") +
                    "%",
                    smallFont,
                    text,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 38,
                        bounds.Width,
                        20),
                    centered);
            }
        }

        private static void DrawPump(
            Graphics graphics,
            Rectangle bounds,
            string label,
            Color color,
            Font font)
        {
            using (Pen pen =
                new Pen(color, 1.5f))
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawEllipse(
                    pen,
                    bounds);

                graphics.DrawString(
                    label,
                    font,
                    brush,
                    bounds,
                    centered);
            }
        }

        private static void DrawBox(
            Graphics graphics,
            Rectangle bounds,
            string title,
            string detail,
            Color color,
            Font titleFont,
            Font detailFont)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        190,
                        3,
                        16,
                        22)))
            using (Pen pen =
                new Pen(color, 1.4f))
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    Trimming =
                        StringTrimming.EllipsisCharacter
                })
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    title,
                    titleFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Top + 2,
                        bounds.Width - 4,
                        bounds.Height / 2),
                    centered);

                graphics.DrawString(
                    detail,
                    detailFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Top +
                        bounds.Height / 2,
                        bounds.Width - 4,
                        bounds.Height / 2 - 2),
                    centered);
            }
        }

        private static void DrawFlow(
            Graphics graphics,
            Point start,
            Point end,
            Color color)
        {
            using (Pen pen =
                new Pen(color, 1.8f))
            {
                pen.EndCap =
                    LineCap.ArrowAnchor;

                int middleX =
                    (start.X + end.X) / 2;

                graphics.DrawLines(
                    pen,
                    new[]
                    {
                        start,
                        new Point(
                            middleX,
                            start.Y),
                        new Point(
                            middleX,
                            end.Y),
                        end
                    });
            }
        }

        private static void DrawNozzle(
            Graphics graphics,
            Rectangle chamber,
            Color color)
        {
            int centerX =
                chamber.Left +
                chamber.Width / 2;

            Point[] nozzle =
            {
                new Point(
                    centerX - 26,
                    chamber.Bottom),
                new Point(
                    centerX + 26,
                    chamber.Bottom),
                new Point(
                    centerX + 43,
                    chamber.Bottom + 34),
                new Point(
                    centerX - 43,
                    chamber.Bottom + 34)
            };

            using (Pen pen =
                new Pen(color, 1.5f))
            {
                graphics.DrawPolygon(
                    pen,
                    nozzle);

                graphics.DrawLine(
                    pen,
                    centerX - 53,
                    chamber.Bottom + 40,
                    centerX + 53,
                    chamber.Bottom + 40);
            }
        }

        private static void DrawBackground(
            Graphics graphics,
            Rectangle bounds,
            Color dimPhosphor)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        48,
                        2,
                        13,
                        18)))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        18,
                        dimPhosphor)))
            {
                graphics.FillRectangle(
                    brush,
                    bounds);

                for (int x = bounds.Left;
                     x < bounds.Right;
                     x += 40)
                {
                    graphics.DrawLine(
                        gridPen,
                        x,
                        bounds.Top,
                        x,
                        bounds.Bottom);
                }

                for (int y = bounds.Top;
                     y < bounds.Bottom;
                     y += 40)
                {
                    graphics.DrawLine(
                        gridPen,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private static void DrawCenteredText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    bounds,
                    format);
            }
        }

        private static Rectangle CircleRect(
            int x,
            int y,
            int diameter)
        {
            return new Rectangle(
                x,
                y,
                diameter,
                diameter);
        }

        private static double Fraction(
            double amount,
            double capacity)
        {
            if (capacity <= 0.0 ||
                double.IsNaN(amount) ||
                double.IsInfinity(amount))
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    amount / capacity));
        }

        private static string Shorten(
            string value,
            int maximum)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length <= maximum)
            {
                return value ?? string.Empty;
            }

            return value.Substring(
                0,
                Math.Max(1, maximum - 1)) +
                "…";
        }
    }
}
