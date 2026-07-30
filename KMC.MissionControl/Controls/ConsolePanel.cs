using KMC.MissionControl.Themes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Apollo-era console module with a painted metal face,
    /// recessed equipment bay, engraved title, and fasteners.
    /// </summary>
    public sealed class ConsolePanel : Panel
    {
        private const int TitleHeight = 36;
        private const int ScrewRadius = 5;

        private string _panelTitle;

        public ConsolePanel()
        {
            _panelTitle = "CONSOLE";

            BackColor = ApolloTheme.ConsoleFace;
            ForeColor = ApolloTheme.EngravedLabel;

            Padding = new Padding(
                14,
                TitleHeight + 12,
                14,
                14);

            DoubleBuffered = true;
            ResizeRedraw = true;

            MinimumSize = new Size(240, 120);
        }

        public string PanelTitle
        {
            get
            {
                return _panelTitle;
            }

            set
            {
                _panelTitle =
                    string.IsNullOrWhiteSpace(value)
                        ? "CONSOLE"
                        : value.ToUpperInvariant();

                Invalidate();
            }
        }

        protected override void OnPaintBackground(
            PaintEventArgs e)
        {
            Rectangle area = ClientRectangle;

            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    area,
                    ApolloTheme.ConsoleFaceLight,
                    ApolloTheme.ConsoleFace,
                    LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(
                    brush,
                    area);
            }
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            DrawOuterFrame(e.Graphics);
            DrawTitlePlate(e.Graphics);
            DrawInstrumentWell(e.Graphics);
            DrawScrews(e.Graphics);
        }

        private void DrawOuterFrame(
            Graphics graphics)
        {
            Rectangle outer =
                new Rectangle(
                    0,
                    0,
                    Width - 1,
                    Height - 1);

            using (Pen darkPen =
                new Pen(
                    ApolloTheme.ConsoleEdge,
                    2f))
            {
                graphics.DrawRectangle(
                    darkPen,
                    outer);
            }

            using (Pen lightPen =
                new Pen(
                    ApolloTheme.ConsoleFaceLight,
                    1f))
            {
                graphics.DrawLine(
                    lightPen,
                    2,
                    2,
                    Width - 3,
                    2);

                graphics.DrawLine(
                    lightPen,
                    2,
                    2,
                    2,
                    Height - 3);
            }

            using (Pen shadowPen =
                new Pen(
                    ApolloTheme.ConsoleFaceDark,
                    1f))
            {
                graphics.DrawLine(
                    shadowPen,
                    2,
                    Height - 3,
                    Width - 3,
                    Height - 3);

                graphics.DrawLine(
                    shadowPen,
                    Width - 3,
                    2,
                    Width - 3,
                    Height - 3);
            }
        }

        private void DrawTitlePlate(
            Graphics graphics)
        {
            Rectangle plate =
                new Rectangle(
                    18,
                    8,
                    Width - 36,
                    24);

            using (SolidBrush plateBrush =
                new SolidBrush(
                    Color.FromArgb(184, 188, 171)))
            {
                graphics.FillRectangle(
                    plateBrush,
                    plate);
            }

            using (Pen plateBorder =
                new Pen(
                    ApolloTheme.ConsoleFaceDark,
                    1f))
            {
                graphics.DrawRectangle(
                    plateBorder,
                    plate);
            }

            Rectangle textArea =
                new Rectangle(
                    plate.Left + 10,
                    plate.Top,
                    plate.Width - 20,
                    plate.Height);

            using (Font titleFont =
                ApolloTheme.CreateLabelFont(
                    9f,
                    FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    graphics,
                    _panelTitle,
                    titleFont,
                    textArea,
                    ApolloTheme.EngravedLabel,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawInstrumentWell(
            Graphics graphics)
        {
            Rectangle well =
                new Rectangle(
                    10,
                    TitleHeight + 8,
                    Width - 20,
                    Height - TitleHeight - 18);

            if (well.Width <= 0 || well.Height <= 0)
            {
                return;
            }

            using (SolidBrush wellBrush =
                new SolidBrush(
                    ApolloTheme.InstrumentWell))
            {
                graphics.FillRectangle(
                    wellBrush,
                    well);
            }

            using (Pen deepShadow =
                new Pen(
                    Color.FromArgb(20, 22, 20),
                    4f))
            {
                graphics.DrawLine(
                    deepShadow,
                    well.Left,
                    well.Top,
                    well.Right,
                    well.Top);

                graphics.DrawLine(
                    deepShadow,
                    well.Left,
                    well.Top,
                    well.Left,
                    well.Bottom);
            }

            using (Pen rim =
                new Pen(
                    ApolloTheme.ConsoleFaceDark,
                    2f))
            {
                graphics.DrawLine(
                    rim,
                    well.Left,
                    well.Bottom,
                    well.Right,
                    well.Bottom);

                graphics.DrawLine(
                    rim,
                    well.Right,
                    well.Top,
                    well.Right,
                    well.Bottom);
            }
        }

        private void DrawScrews(
            Graphics graphics)
        {
            DrawScrew(graphics, 8, 8);
            DrawScrew(graphics, Width - 9, 8);
            DrawScrew(graphics, 8, Height - 9);
            DrawScrew(graphics, Width - 9, Height - 9);
        }

        private static void DrawScrew(
            Graphics graphics,
            int centerX,
            int centerY)
        {
            Rectangle screw =
                new Rectangle(
                    centerX - ScrewRadius,
                    centerY - ScrewRadius,
                    ScrewRadius * 2,
                    ScrewRadius * 2);

            using (LinearGradientBrush screwBrush =
                new LinearGradientBrush(
                    screw,
                    ApolloTheme.ScrewLight,
                    ApolloTheme.ScrewDark,
                    LinearGradientMode.ForwardDiagonal))
            {
                graphics.FillEllipse(
                    screwBrush,
                    screw);
            }

            using (Pen outline =
                new Pen(
                    ApolloTheme.ScrewDark,
                    1f))
            {
                graphics.DrawEllipse(
                    outline,
                    screw);

                graphics.DrawLine(
                    outline,
                    centerX - 3,
                    centerY,
                    centerX + 3,
                    centerY);
            }
        }
    }
}