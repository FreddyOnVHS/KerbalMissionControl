using KMC.MissionControl.Themes;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Apollo-style recessed CRT telemetry display.
    /// </summary>
    public sealed class TelemetryValue : UserControl
    {
        private readonly Label _nameLabel;
        private readonly Label _valueLabel;

        public TelemetryValue()
        {
            Width = 290;
            Height = 62;

            BackColor = Color.Transparent;
            Padding = new Padding(14, 8, 14, 8);

            DoubleBuffered = true;
            ResizeRedraw = true;

            _nameLabel = new Label
            {
                Text = "TELEMETRY",
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ApolloTheme.CrtBlue,
                BackColor = Color.Transparent,
                Font = ApolloTheme.CreateConsoleFont(
                    9f,
                    FontStyle.Bold)
            };

            _valueLabel = new Label
            {
                Text = "---",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = ApolloTheme.CrtBlue,
                BackColor = Color.Transparent,
                Font = ApolloTheme.CreateDisplayFont(
                    19f,
                    FontStyle.Bold)
            };

            Controls.Add(_valueLabel);
            Controls.Add(_nameLabel);
        }

        public string TelemetryName
        {
            get
            {
                return _nameLabel.Text;
            }

            set
            {
                _nameLabel.Text =
                    string.IsNullOrWhiteSpace(value)
                        ? "TELEMETRY"
                        : value.ToUpperInvariant();
            }
        }

        public string DisplayValue
        {
            get
            {
                return _valueLabel.Text;
            }

            set
            {
                _valueLabel.Text =
                    string.IsNullOrWhiteSpace(value)
                        ? "---"
                        : value;
            }
        }

        public Color ValueColor
        {
            get
            {
                return _valueLabel.ForeColor;
            }

            set
            {
                _valueLabel.ForeColor = value;
            }
        }

        public Color LabelColor
        {
            get
            {
                return _nameLabel.ForeColor;
            }

            set
            {
                _nameLabel.ForeColor = value;
            }
        }

        public int LabelWidth
        {
            get
            {
                return Width;
            }

            set
            {
            }
        }

        public void SetOffline()
        {
            DisplayValue = "---";
            ValueColor = ApolloTheme.CrtDim;
        }

        public void SetNormal(string value)
        {
            DisplayValue = value;
            ValueColor = ApolloTheme.CrtBlue;
        }

        public void SetCaution(string value)
        {
            DisplayValue = value;
            ValueColor = ApolloTheme.LampAmber;
        }

        public void SetWarning(string value)
        {
            DisplayValue = value;
            ValueColor = ApolloTheme.LampRed;
        }

        protected override void OnPaintBackground(
            PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? ApolloTheme.InstrumentWell);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            Rectangle bezelRectangle =
                new Rectangle(
                    1,
                    1,
                    Width - 3,
                    Height - 3);

            Rectangle screenRectangle =
                new Rectangle(
                    7,
                    7,
                    Width - 15,
                    Height - 15);

            using (GraphicsPath bezelPath =
                CreateRoundedRectangle(
                    bezelRectangle,
                    12))
            using (SolidBrush bezelBrush =
                new SolidBrush(
                    Color.FromArgb(42, 47, 45)))
            using (Pen bezelBorder =
                new Pen(
                    Color.FromArgb(88, 94, 87),
                    2f))
            {
                e.Graphics.FillPath(
                    bezelBrush,
                    bezelPath);

                e.Graphics.DrawPath(
                    bezelBorder,
                    bezelPath);
            }

            using (GraphicsPath screenPath =
                CreateRoundedRectangle(
                    screenRectangle,
                    9))
            using (LinearGradientBrush screenBrush =
                new LinearGradientBrush(
                    screenRectangle,
                    Color.FromArgb(18, 40, 53),
                    ApolloTheme.CrtBackground,
                    LinearGradientMode.Vertical))
            {
                e.Graphics.FillPath(
                    screenBrush,
                    screenPath);
            }

            using (Pen highlightPen =
                new Pen(
                    Color.FromArgb(42, 73, 82),
                    1f))
            {
                e.Graphics.DrawArc(
                    highlightPen,
                    screenRectangle,
                    190,
                    135);
            }

            DrawScanLines(
                e.Graphics,
                screenRectangle);

            base.OnPaint(e);
        }

        private static void DrawScanLines(
            Graphics graphics,
            Rectangle screenRectangle)
        {
            using (Pen scanLinePen =
                new Pen(
                    Color.FromArgb(
                        8,
                        120,
                        180,
                        200),
                    1f))
            {
                for (int y =
                    screenRectangle.Top + 4;
                    y < screenRectangle.Bottom - 3;
                    y += 4)
                {
                    graphics.DrawLine(
                        scanLinePen,
                        screenRectangle.Left + 5,
                        y,
                        screenRectangle.Right - 5,
                        y);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(
            Rectangle rectangle,
            int radius)
        {
            GraphicsPath path =
                new GraphicsPath();

            int diameter = radius * 2;

            path.AddArc(
                rectangle.Left,
                rectangle.Top,
                diameter,
                diameter,
                180,
                90);

            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Top,
                diameter,
                diameter,
                270,
                90);

            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);

            path.AddArc(
                rectangle.Left,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                90,
                90);

            path.CloseFigure();

            return path;
        }
    }
}