using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Debugging.Electrical
{
    public sealed class ElectricalStackPreview :
        Control
    {
        private ElectricalTopologyModel _model;

        private readonly Font _titleFont;
        private readonly Font _labelFont;
        private readonly Font _smallFont;

        public ElectricalStackPreview()
        {
            DoubleBuffered =
                true;

            BackColor =
                Color.FromArgb(
                    4,
                    14,
                    18);

            ForeColor =
                Color.FromArgb(
                    170,
                    255,
                    190);

            _titleFont =
                new Font(
                    "Consolas",
                    11.0f,
                    FontStyle.Bold);

            _labelFont =
                new Font(
                    "Consolas",
                    9.0f,
                    FontStyle.Bold);

            _smallFont =
                new Font(
                    "Consolas",
                    8.0f,
                    FontStyle.Regular);
        }

        public void SetModel(
            ElectricalTopologyModel model)
        {
            _model =
                model;

            Invalidate();
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _labelFont.Dispose();
                _smallFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics =
                e.Graphics;

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            graphics.Clear(
                BackColor);

            if (_model == null ||
                _model.Sections.Count == 0)
            {
                DrawCentered(
                    graphics,
                    "NO ELECTRICAL TOPOLOGY",
                    ClientRectangle,
                    _titleFont,
                    Color.FromArgb(
                        120,
                        155,
                        165));

                return;
            }

            Rectangle title =
                new Rectangle(
                    12,
                    8,
                    Width - 24,
                    24);

            TextRenderer.DrawText(
                graphics,
                "CANDIDATE SPACECRAFT STACK — " +
                _model.VesselName,
                _titleFont,
                title,
                ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int sectionCount =
                _model.Sections.Count;

            int availableHeight =
                Math.Max(
                    80,
                    Height - 58);

            int gap =
                7;

            int sectionHeight =
                Math.Max(
                    46,
                    Math.Min(
                        105,
                        (availableHeight -
                         gap *
                         (sectionCount - 1)) /
                        Math.Max(
                            1,
                            sectionCount)));

            int coreWidth =
                Math.Max(
                    170,
                    Math.Min(
                        310,
                        Width /
                        3));

            int centerX =
                Width /
                2;

            int y =
                40;

            for (int index = 0;
                 index < sectionCount;
                 index++)
            {
                ElectricalSectionModel section =
                    _model.Sections[index];

                int width =
                    section.IsRadialSection
                        ? Math.Max(
                            135,
                            coreWidth -
                            50)
                        : coreWidth;

                int x =
                    section.IsRadialSection
                        ? section.AverageY >= 0.0
                            ? centerX +
                              coreWidth /
                              2 +
                              28
                            : centerX -
                              coreWidth /
                              2 -
                              width -
                              28
                        : centerX -
                          width /
                          2;

                Rectangle bounds =
                    new Rectangle(
                        x,
                        y,
                        width,
                        sectionHeight);

                DrawSection(
                    graphics,
                    bounds,
                    section);

                if (!section.IsRadialSection &&
                    index <
                        sectionCount - 1)
                {
                    using (Pen connector =
                        new Pen(
                            Color.FromArgb(
                                95,
                                140,
                                150),
                            1.0f))
                    {
                        graphics.DrawLine(
                            connector,
                            centerX,
                            bounds.Bottom,
                            centerX,
                            bounds.Bottom +
                            gap);
                    }
                }

                y +=
                    sectionHeight +
                    gap;
            }
        }

        private void DrawSection(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section)
        {
            Color state =
                ResolveStateColor(
                    section);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        26,
                        state)))
            using (Pen outline =
                new Pen(
                    state,
                    section.IsCommandSection
                        ? 2.2f
                        : 1.4f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);
            }

            Rectangle heading =
                new Rectangle(
                    bounds.Left + 5,
                    bounds.Top + 4,
                    bounds.Width - 10,
                    18);

            TextRenderer.DrawText(
                graphics,
                section.Name,
                _labelFont,
                heading,
                state,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            string line1 =
                "SEP " +
                FormatStage(
                    section.SeparationStage) +
                "  PARTS " +
                section.PartCount.ToString("00");

            string line2 =
                "EC " +
                section.ElectricChargeAmount
                    .ToString("0.0") +
                " / " +
                section.ElectricChargeCapacity
                    .ToString("0.0") +
                "  " +
                section.ElectricChargePercent
                    .ToString("0") +
                "%";

            string line3 =
                "BAT " +
                section.BatteryPartCount +
                "  SOL " +
                section.SolarPartCount +
                "  GEN " +
                section.GeneratorPartCount;

            DrawCentered(
                graphics,
                line1,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 23,
                    bounds.Width - 8,
                    16),
                _smallFont,
                Color.FromArgb(
                    185,
                    215,
                    220));

            DrawCentered(
                graphics,
                line2,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 39,
                    bounds.Width - 8,
                    16),
                _smallFont,
                state);

            if (bounds.Height >= 72)
            {
                DrawCentered(
                    graphics,
                    line3,
                    new Rectangle(
                        bounds.Left + 4,
                        bounds.Top + 55,
                        bounds.Width - 8,
                        15),
                    _smallFont,
                    Color.FromArgb(
                        150,
                        180,
                        185));
            }

            DrawComponentTicks(
                graphics,
                bounds,
                section,
                state);
        }

        private static void DrawComponentTicks(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section,
            Color state)
        {
            int leftCount =
                section.BatteryPartCount;

            int rightCount =
                section.SolarPartCount +
                section.GeneratorPartCount +
                section.FuelCellPartCount;

            using (Pen pen =
                new Pen(
                    state,
                    2.0f))
            {
                int leftTicks =
                    Math.Min(
                        4,
                        leftCount);

                for (int index = 0;
                     index < leftTicks;
                     index++)
                {
                    int y =
                        bounds.Top +
                        14 +
                        index *
                        11;

                    graphics.DrawLine(
                        pen,
                        bounds.Left - 8,
                        y,
                        bounds.Left,
                        y);
                }

                int rightTicks =
                    Math.Min(
                        4,
                        rightCount);

                for (int index = 0;
                     index < rightTicks;
                     index++)
                {
                    int y =
                        bounds.Top +
                        14 +
                        index *
                        11;

                    graphics.DrawLine(
                        pen,
                        bounds.Right,
                        y,
                        bounds.Right + 8,
                        y);
                }
            }
        }

        private static Color ResolveStateColor(
            ElectricalSectionModel section)
        {
            if (section.ElectricChargeCapacity <=
                0.0001)
            {
                return Color.FromArgb(
                    100,
                    135,
                    145);
            }

            double percent =
                section.ElectricChargePercent;

            if (percent <= 5.0)
            {
                return Color.FromArgb(
                    255,
                    75,
                    55);
            }

            if (percent <= 15.0)
            {
                return Color.FromArgb(
                    255,
                    190,
                    45);
            }

            return Color.FromArgb(
                75,
                235,
                105);
        }

        private static string FormatStage(
            int stage)
        {
            return
                stage >= 0
                    ? stage.ToString("00")
                    : "--";
        }

        private static void DrawCentered(
            Graphics graphics,
            string text,
            Rectangle bounds,
            Font font,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }
    }
}
