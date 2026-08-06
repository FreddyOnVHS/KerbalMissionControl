using KMC.MissionControl.Models;
using KMC.MissionControl.Telemetry;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Persistent lower-console annunciator panel.
    /// Build 2.0.1 provides the panel foundation and lamp test only.
    /// Live event evaluation will be connected in later milestones.
    /// </summary>
    public sealed class MissionSummary : Control
    {
        private const int NormalAnnunciatorHeight = 180;
        private const int CompactAnnunciatorHeight = 140;
        private const int CompactHostHeightBreakpoint = 1050;

        private const int SasTelemetryPort = 5060;
        private const string SasProtocolId = "KMCSAS1";
        private const int SystemsTelemetryPort = 5061;
        private const string SystemsProtocolId = "KMCSYS1";
        private const double TimedEventSeconds = 5.0;

        private sealed class SasStateReceiver :
            IDisposable
        {
            private readonly object _syncRoot =
                new object();

            private UdpClient _client;
            private bool _disposed;
            private bool _sasEnabled;
            private DateTime _lastReceivedUtc =
                DateTime.MinValue;

            public void Start()
            {
                if (_client != null)
                {
                    return;
                }

                try
                {
                    _client =
                        new UdpClient(
                            SasTelemetryPort);

                    BeginReceive();
                }
                catch
                {
                    Dispose();
                }
            }

            public bool IsSasEnabled
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return
                            DateTime.UtcNow -
                                _lastReceivedUtc <
                            TimeSpan.FromSeconds(
                                2.0) &&
                            _sasEnabled;
                    }
                }
            }

            private void BeginReceive()
            {
                UdpClient client =
                    _client;

                if (client == null ||
                    _disposed)
                {
                    return;
                }

                try
                {
                    client.BeginReceive(
                        OnReceive,
                        client);
                }
                catch
                {
                    // Receiver shutdown or socket disposal.
                }
            }

            private void OnReceive(
                IAsyncResult result)
            {
                UdpClient client =
                    result.AsyncState as UdpClient;

                if (client == null ||
                    _disposed)
                {
                    return;
                }

                try
                {
                    IPEndPoint endpoint =
                        new IPEndPoint(
                            IPAddress.Loopback,
                            0);

                    byte[] payload =
                        client.EndReceive(
                            result,
                            ref endpoint);

                    string message =
                        Encoding.UTF8.GetString(
                            payload);

                    string[] parts =
                        message.Split('|');

                    if (parts.Length == 2 &&
                        string.Equals(
                            parts[0],
                            SasProtocolId,
                            StringComparison.Ordinal))
                    {
                        bool enabled =
                            parts[1] == "1";

                        lock (_syncRoot)
                        {
                            _sasEnabled =
                                enabled;

                            _lastReceivedUtc =
                                DateTime.UtcNow;
                        }
                    }
                }
                catch
                {
                    // Ignore malformed packets and shutdown races.
                }
                finally
                {
                    BeginReceive();
                }
            }

            public void Dispose()
            {
                _disposed =
                    true;

                UdpClient client =
                    _client;

                _client =
                    null;

                if (client != null)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private sealed class SystemsStateSnapshot
        {
            public bool Online { get; set; }
            public double ElectricChargeAmount { get; set; }
            public double ElectricChargeCapacity { get; set; }
            public double MaximumThermalRatio { get; set; }
            public bool Docked { get; set; }

            public double ElectricChargeFraction
            {
                get
                {
                    if (ElectricChargeCapacity <= 0.0001)
                    {
                        return 1.0;
                    }

                    return Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            ElectricChargeAmount /
                            ElectricChargeCapacity));
                }
            }
        }

        private sealed class SystemsStateReceiver :
            IDisposable
        {
            private readonly object _syncRoot =
                new object();

            private UdpClient _client;
            private bool _disposed;
            private double _electricChargeAmount;
            private double _electricChargeCapacity;
            private double _maximumThermalRatio;
            private bool _docked;
            private DateTime _lastReceivedUtc =
                DateTime.MinValue;

            public void Start()
            {
                if (_client != null)
                {
                    return;
                }

                try
                {
                    _client =
                        new UdpClient(
                            SystemsTelemetryPort);

                    BeginReceive();
                }
                catch
                {
                    Dispose();
                }
            }

            public SystemsStateSnapshot GetSnapshot()
            {
                lock (_syncRoot)
                {
                    bool online =
                        DateTime.UtcNow -
                            _lastReceivedUtc <
                        TimeSpan.FromSeconds(
                            2.0);

                    return new SystemsStateSnapshot
                    {
                        Online = online,
                        ElectricChargeAmount =
                            online
                                ? _electricChargeAmount
                                : 0.0,
                        ElectricChargeCapacity =
                            online
                                ? _electricChargeCapacity
                                : 0.0,
                        MaximumThermalRatio =
                            online
                                ? _maximumThermalRatio
                                : 0.0,
                        Docked =
                            online &&
                            _docked
                    };
                }
            }

            private void BeginReceive()
            {
                UdpClient client =
                    _client;

                if (client == null ||
                    _disposed)
                {
                    return;
                }

                try
                {
                    client.BeginReceive(
                        OnReceive,
                        client);
                }
                catch
                {
                }
            }

            private void OnReceive(
                IAsyncResult result)
            {
                UdpClient client =
                    result.AsyncState as UdpClient;

                if (client == null ||
                    _disposed)
                {
                    return;
                }

                try
                {
                    IPEndPoint endpoint =
                        new IPEndPoint(
                            IPAddress.Loopback,
                            0);

                    byte[] payload =
                        client.EndReceive(
                            result,
                            ref endpoint);

                    string[] parts =
                        Encoding.UTF8
                            .GetString(payload)
                            .Split('|');

                    double amount;
                    double capacity;
                    double thermalRatio;

                    if (parts.Length == 5 &&
                        string.Equals(
                            parts[0],
                            SystemsProtocolId,
                            StringComparison.Ordinal) &&
                        double.TryParse(
                            parts[1],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out amount) &&
                        double.TryParse(
                            parts[2],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out capacity) &&
                        double.TryParse(
                            parts[3],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out thermalRatio))
                    {
                        lock (_syncRoot)
                        {
                            _electricChargeAmount =
                                Math.Max(0.0, amount);
                            _electricChargeCapacity =
                                Math.Max(0.0, capacity);
                            _maximumThermalRatio =
                                Math.Max(0.0, thermalRatio);
                            _docked =
                                parts[4] == "1";
                            _lastReceivedUtc =
                                DateTime.UtcNow;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    BeginReceive();
                }
            }

            public void Dispose()
            {
                _disposed = true;

                UdpClient client =
                    _client;

                _client = null;

                if (client != null)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private enum LampColor
        {
            Blue,
            Green,
            Amber,
            Red
        }

        private sealed class LampDefinition
        {
            public LampDefinition(
                string id,
                string label,
                LampColor color)
            {
                Id = id;
                Label = label;
                Color = color;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public LampColor Color { get; private set; }

            public bool Active { get; set; }
        }

        private readonly Font _titleFont;
        private readonly Font _lampFont;
        private readonly Font _smallFont;
        private readonly Timer _lampTestTimer;
        private readonly Timer _linkStateTimer;
        private readonly LampDefinition[] _lamps;
        private readonly SasStateReceiver _sasStateReceiver;
        private readonly SystemsStateReceiver _systemsStateReceiver;

        private MissionTelemetry _telemetry;
        private DateTime _lastTelemetryUtc;

        private Rectangle _ackBounds;
        private Rectangle _lampTestBounds;
        private bool _lampTestActive;

        private bool _masterCautionLatched;
        private bool _masterWarningLatched;
        private bool _masterCautionAcknowledged;
        private bool _masterWarningAcknowledged;
        private bool _previousCautionCondition;
        private bool _previousWarningCondition;
        private bool _alarmFlashOn = true;

        private bool _stageTrackingInitialized;
        private int _previousStage;

        private bool _srbTrackingInitialized;
        private int _previousSrbBoosterCount;

        private DateTime _stageSeparationUntilUtc =
            DateTime.MinValue;

        private DateTime _srbSeparationUntilUtc =
            DateTime.MinValue;

        public MissionSummary()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            BackColor =
                Color.FromArgb(
                    35,
                    40,
                    38);

            _titleFont =
                new Font(
                    "Consolas",
                    9.5f,
                    FontStyle.Bold);

            _lampFont =
                new Font(
                    "Consolas",
                    7.5f,
                    FontStyle.Bold);

            _smallFont =
                new Font(
                    "Consolas",
                    7.5f,
                    FontStyle.Bold);

            _telemetry =
                new MissionTelemetry();

            _lastTelemetryUtc =
                DateTime.MinValue;

            _lamps =
                CreateLampDefinitions();

            _sasStateReceiver =
                new SasStateReceiver();

            _sasStateReceiver.Start();

            _systemsStateReceiver =
                new SystemsStateReceiver();

            _systemsStateReceiver.Start();

            _lampTestTimer =
                new Timer
                {
                    Interval = 3000
                };

            _lampTestTimer.Tick +=
                OnLampTestTimerTick;

            _linkStateTimer =
                new Timer
                {
                    Interval = 500
                };

            _linkStateTimer.Tick +=
                OnLinkStateTimerTick;

            _linkStateTimer.Start();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            Cursor = Cursors.Hand;

            /*
             * Do not impose a vertical MinimumSize here. MainForm may assign a
             * compact row during resize; a fixed 180-pixel minimum would make
             * WinForms draw the control outside that row and clip its bottom.
             */
            MinimumSize =
                new Size(
                    320,
                    0);
        }

        protected override void OnParentChanged(
            EventArgs e)
        {
            TableLayoutPanel oldLayout =
                Parent as TableLayoutPanel;

            if (oldLayout != null)
            {
                oldLayout.SizeChanged -=
                    OnHostLayoutSizeChanged;
            }

            base.OnParentChanged(
                e);

            TableLayoutPanel layout =
                Parent as TableLayoutPanel;

            if (layout != null)
            {
                layout.SizeChanged -=
                    OnHostLayoutSizeChanged;

                layout.SizeChanged +=
                    OnHostLayoutSizeChanged;
            }

            QueueHostRowCorrection();
        }

        protected override void OnVisibleChanged(
            EventArgs e)
        {
            base.OnVisibleChanged(
                e);

            if (Visible)
            {
                QueueHostRowCorrection();
            }
        }

        protected override void OnSizeChanged(
            EventArgs e)
        {
            base.OnSizeChanged(
                e);

            Invalidate();
        }

        private void OnHostLayoutSizeChanged(
            object sender,
            EventArgs e)
        {
            /*
             * MainForm also updates the same row from its Resize event.
             * Queue this correction so it runs after MainForm completes that
             * layout pass rather than racing it synchronously.
             */
            QueueHostRowCorrection();
        }

        private void QueueHostRowCorrection()
        {
            if (!Visible ||
                IsDisposed ||
                Disposing ||
                !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(
                new MethodInvoker(
                    EnsureHostRowHeight));
        }

        private void EnsureHostRowHeight()
        {
            if (!Visible ||
                IsDisposed ||
                Disposing)
            {
                return;
            }

            TableLayoutPanel layout =
                Parent as TableLayoutPanel;

            if (layout == null)
            {
                return;
            }

            Form hostForm =
                FindForm();

            int hostHeight =
                hostForm != null
                    ? hostForm.ClientSize.Height
                    : layout.ClientSize.Height;

            int requiredHeight =
                hostHeight <
                    CompactHostHeightBreakpoint
                    ? CompactAnnunciatorHeight
                    : NormalAnnunciatorHeight;

            TableLayoutPanelCellPosition position =
                layout.GetPositionFromControl(
                    this);

            if (position.Row < 0 ||
                position.Row >=
                    layout.RowStyles.Count)
            {
                return;
            }

            RowStyle row =
                layout.RowStyles[position.Row];

            if (row.SizeType !=
                    SizeType.Absolute ||
                Math.Abs(
                    row.Height -
                    requiredHeight) >
                    0.5f)
            {
                layout.SuspendLayout();

                try
                {
                    row.SizeType =
                        SizeType.Absolute;

                    row.Height =
                        requiredHeight;
                }
                finally
                {
                    layout.ResumeLayout(
                        performLayout: true);
                }
            }
        }

        public void UpdateTelemetry(
            MissionTelemetry telemetry)
        {
            _telemetry =
                telemetry ??
                new MissionTelemetry();

            _lastTelemetryUtc =
                DateTime.UtcNow;

            UpdateTimedMissionEvents();
            EvaluateLiveIndicators();

            Invalidate();
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _lampTestTimer.Stop();
                _lampTestTimer.Dispose();

                _linkStateTimer.Stop();
                _linkStateTimer.Dispose();

                _sasStateReceiver.Dispose();
                _systemsStateReceiver.Dispose();

                _titleFont.Dispose();
                _lampFont.Dispose();
                _smallFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus();

            if (_lampTestBounds.Contains(
                    e.Location))
            {
                StartLampTest();
                return;
            }

            if (_ackBounds.Contains(
                    e.Location))
            {
                AcknowledgeMasterAlarms();
                return;
            }
        }

        protected override void OnKeyDown(
            KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.T)
            {
                StartLampTest();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.A)
            {
                AcknowledgeMasterAlarms();
                e.Handled = true;
            }
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics =
                e.Graphics;

            GraphicsState savedState =
                graphics.Save();

            graphics.SetClip(
                ClientRectangle);

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            DrawPanelFrame(
                graphics);

            Rectangle inner =
                new Rectangle(
                    18,
                    9,
                    Math.Max(
                        1,
                        Width - 36),
                    Math.Max(
                        1,
                        Height - 18));

            DrawHeader(
                graphics,
                inner);

            Rectangle grid =
                new Rectangle(
                    inner.Left + 5,
                    inner.Top + 25,
                    Math.Max(
                        1,
                        inner.Width - 10),
                    Math.Max(
                        1,
                        inner.Height - 29));

            DrawLampGrid(
                graphics,
                grid);

            graphics.Restore(
                savedState);
        }

        private void DrawPanelFrame(
            Graphics graphics)
        {
            Rectangle bounds =
                new Rectangle(
                    0,
                    0,
                    Math.Max(
                        1,
                        Width - 1),
                    Math.Max(
                        1,
                        Height - 1));

            using (LinearGradientBrush frame =
                new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(
                        102,
                        106,
                        99),
                    Color.FromArgb(
                        35,
                        39,
                        36),
                    LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(
                    frame,
                    bounds);
            }

            using (Pen dark =
                new Pen(
                    Color.FromArgb(
                        14,
                        17,
                        15),
                    2.0f))
            using (Pen highlight =
                new Pen(
                    Color.FromArgb(
                        145,
                        150,
                        140),
                    1.0f))
            {
                graphics.DrawRectangle(
                    dark,
                    bounds);

                graphics.DrawLine(
                    highlight,
                    3,
                    3,
                    Width - 4,
                    3);

                graphics.DrawLine(
                    highlight,
                    3,
                    3,
                    3,
                    Height - 4);
            }

            DrawFastener(
                graphics,
                7,
                7);

            DrawFastener(
                graphics,
                Width - 15,
                7);

            DrawFastener(
                graphics,
                7,
                Height - 15);

            DrawFastener(
                graphics,
                Width - 15,
                Height - 15);
        }

        private void DrawHeader(
            Graphics graphics,
            Rectangle inner)
        {
            Rectangle title =
                new Rectangle(
                    inner.Left + 4,
                    inner.Top,
                    310,
                    21);

            int buttonWidth =
                Math.Max(
                    72,
                    Math.Min(
                        104,
                        inner.Width / 12));

            _lampTestBounds =
                new Rectangle(
                    inner.Right - buttonWidth,
                    inner.Top,
                    buttonWidth,
                    20);

            _ackBounds =
                new Rectangle(
                    _lampTestBounds.Left -
                    buttonWidth -
                    7,
                    inner.Top,
                    buttonWidth,
                    20);

            Rectangle status =
                new Rectangle(
                    title.Right + 8,
                    inner.Top,
                    Math.Max(
                        1,
                        _ackBounds.Left -
                        title.Right -
                        14),
                    20);

            TextRenderer.DrawText(
                graphics,
                "EVENT / CAUTION INDICATOR",
                _titleFont,
                title,
                Color.FromArgb(
                    205,
                    240,
                    245),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                _lampTestActive
                    ? "LAMP TEST ACTIVE"
                    : GetPanelStatusText(),
                _smallFont,
                status,
                _lampTestActive
                    ? Color.FromArgb(
                        255,
                        215,
                        70)
                    : Color.FromArgb(
                        135,
                        175,
                        185),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawControlButton(
                graphics,
                _ackBounds,
                "ACK",
                HasUnacknowledgedMasterAlarm());

            DrawControlButton(
                graphics,
                _lampTestBounds,
                "LAMP TEST",
                _lampTestActive);
        }

        private void DrawLampGrid(
            Graphics graphics,
            Rectangle bounds)
        {
            const int columns = 12;
            const int rows = 2;
            const int gap = 3;

            int availableWidth =
                Math.Max(
                    columns,
                    bounds.Width -
                    gap *
                    (columns - 1));

            int availableHeight =
                Math.Max(
                    rows,
                    bounds.Height -
                    gap *
                    (rows - 1));

            int cellWidth =
                Math.Max(
                    1,
                    availableWidth /
                    columns);

            int cellHeight =
                Math.Max(
                    1,
                    availableHeight /
                    rows);

            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                int row =
                    index /
                    columns;

                int column =
                    index %
                    columns;

                Rectangle lamp =
                    new Rectangle(
                        bounds.Left +
                        column *
                        (cellWidth + gap),
                        bounds.Top +
                        row *
                        (cellHeight + gap),
                        cellWidth,
                        cellHeight);

                DrawLamp(
                    graphics,
                    lamp,
                    _lamps[index],
                    _lampTestActive ||
                    ShouldIlluminateLamp(
                        _lamps[index]));
            }
        }

        private void DrawLamp(
            Graphics graphics,
            Rectangle bounds,
            LampDefinition lamp,
            bool illuminated)
        {
            if (bounds.Width < 8 ||
                bounds.Height < 8)
            {
                return;
            }

            Color active =
                GetLampColor(
                    lamp.Color);

            int bezelRadius =
                Math.Max(
                    2,
                    Math.Min(
                        7,
                        bounds.Height / 8));

            Rectangle shadowBounds =
                new Rectangle(
                    bounds.Left + 2,
                    bounds.Top + 3,
                    Math.Max(
                        1,
                        bounds.Width - 2),
                    Math.Max(
                        1,
                        bounds.Height - 2));

            Rectangle bezelBounds =
                Rectangle.Inflate(
                    bounds,
                    -1,
                    -1);

            Rectangle innerBezel =
                Rectangle.Inflate(
                    bezelBounds,
                    -2,
                    -2);

            Rectangle lensBounds =
                Rectangle.Inflate(
                    innerBezel,
                    -3,
                    -3);

            int lensRadius =
                Math.Max(
                    2,
                    bezelRadius - 2);

            using (GraphicsPath shadowPath =
                CreateRoundedRectanglePath(
                    shadowBounds,
                    bezelRadius))
            using (SolidBrush shadow =
                new SolidBrush(
                    Color.FromArgb(
                        105,
                        0,
                        0,
                        0)))
            {
                graphics.FillPath(
                    shadow,
                    shadowPath);
            }

            using (GraphicsPath bezelPath =
                CreateRoundedRectanglePath(
                    bezelBounds,
                    bezelRadius))
            using (LinearGradientBrush bezelBrush =
                new LinearGradientBrush(
                    bezelBounds,
                    Color.FromArgb(
                        104,
                        101,
                        88),
                    Color.FromArgb(
                        25,
                        24,
                        21),
                    LinearGradientMode.Vertical))
            using (Pen bezelOutline =
                new Pen(
                    Color.FromArgb(
                        10,
                        10,
                        9),
                    1.4f))
            {
                graphics.FillPath(
                    bezelBrush,
                    bezelPath);

                graphics.DrawPath(
                    bezelOutline,
                    bezelPath);
            }

            using (GraphicsPath innerPath =
                CreateRoundedRectanglePath(
                    innerBezel,
                    Math.Max(
                        2,
                        bezelRadius - 1)))
            using (LinearGradientBrush innerBrush =
                new LinearGradientBrush(
                    innerBezel,
                    Color.FromArgb(
                        18,
                        18,
                        16),
                    Color.FromArgb(
                        52,
                        50,
                        43),
                    LinearGradientMode.Vertical))
            using (Pen innerOutline =
                new Pen(
                    Color.FromArgb(
                        126,
                        119,
                        98),
                    0.8f))
            {
                graphics.FillPath(
                    innerBrush,
                    innerPath);

                graphics.DrawPath(
                    innerOutline,
                    innerPath);
            }

            Color lensTop;
            Color lensMiddle;
            Color lensBottom;

            if (illuminated)
            {
                lensTop =
                    Darken(
                        active,
                        0.30);

                lensMiddle =
                    Lighten(
                        active,
                        0.18);

                lensBottom =
                    Darken(
                        active,
                        0.12);
            }
            else
            {
                lensTop =
                    Color.FromArgb(
                        24,
                        24,
                        20);

                lensMiddle =
                    Color.FromArgb(
                        38,
                        37,
                        31);

                lensBottom =
                    Color.FromArgb(
                        13,
                        13,
                        11);
            }

            using (GraphicsPath lensPath =
                CreateRoundedRectanglePath(
                    lensBounds,
                    lensRadius))
            using (LinearGradientBrush lensBrush =
                new LinearGradientBrush(
                    lensBounds,
                    lensTop,
                    lensBottom,
                    LinearGradientMode.Vertical))
            {
                ColorBlend blend =
                    new ColorBlend
                    {
                        Colors =
                            new[]
                            {
                                lensTop,
                                lensMiddle,
                                lensBottom
                            },

                        Positions =
                            new[]
                            {
                                0.0f,
                                0.55f,
                                1.0f
                            }
                    };

                lensBrush.InterpolationColors =
                    blend;

                graphics.FillPath(
                    lensBrush,
                    lensPath);

                if (illuminated)
                {
                    using (PathGradientBrush glow =
                        new PathGradientBrush(
                            lensPath))
                    {
                        glow.CenterColor =
                            Color.FromArgb(
                                100,
                                Lighten(
                                    active,
                                    0.40));

                        glow.SurroundColors =
                            new[]
                            {
                                Color.FromArgb(
                                    0,
                                    active)
                            };

                        graphics.FillPath(
                            glow,
                            lensPath);
                    }
                }

                DrawDiffusedLensTexture(
                    graphics,
                    lensPath,
                    lensBounds,
                    illuminated
                        ? active
                        : Color.FromArgb(
                            72,
                            66,
                            52));

                using (Pen lensOutline =
                    new Pen(
                        illuminated
                            ? Lighten(
                                active,
                                0.30)
                            : Color.FromArgb(
                                74,
                                70,
                                59),
                        1.0f))
                {
                    graphics.DrawPath(
                        lensOutline,
                        lensPath);
                }

                using (Pen upperReflection =
                    new Pen(
                        Color.FromArgb(
                            illuminated
                                ? 88
                                : 35,
                            255,
                            245,
                            215),
                        1.0f))
                {
                    int reflectionY =
                        lensBounds.Top + 2;

                    graphics.DrawLine(
                        upperReflection,
                        lensBounds.Left +
                            lensRadius,
                        reflectionY,
                        lensBounds.Right -
                            lensRadius,
                        reflectionY);
                }
            }

            Color textColor;

            if (!illuminated)
            {
                textColor =
                    Color.FromArgb(
                        111,
                        101,
                        77);
            }
            else if (lamp.Color ==
                     LampColor.Blue)
            {
                textColor =
                    Color.FromArgb(
                        238,
                        236,
                        213);
            }
            else
            {
                textColor =
                    Color.FromArgb(
                        24,
                        21,
                        14);
            }

            Rectangle textBounds =
                new Rectangle(
                    lensBounds.Left + 3,
                    lensBounds.Top + 2,
                    Math.Max(
                        1,
                        lensBounds.Width - 6),
                    Math.Max(
                        1,
                        lensBounds.Height - 4));

            TextRenderer.DrawText(
                graphics,
                lamp.Label,
                _lampFont,
                textBounds,
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawDiffusedLensTexture(
            Graphics graphics,
            GraphicsPath lensPath,
            Rectangle lensBounds,
            Color color)
        {
            GraphicsState state =
                graphics.Save();

            graphics.SetClip(
                lensPath);

            using (Pen vertical =
                new Pen(
                    Color.FromArgb(
                        33,
                        color),
                    1.0f))
            using (Pen horizontal =
                new Pen(
                    Color.FromArgb(
                        24,
                        0,
                        0,
                        0),
                    1.0f))
            {
                for (int x =
                        lensBounds.Left + 2;
                     x <
                        lensBounds.Right;
                     x += 3)
                {
                    graphics.DrawLine(
                        vertical,
                        x,
                        lensBounds.Top + 1,
                        x,
                        lensBounds.Bottom - 1);
                }

                for (int y =
                        lensBounds.Top + 3;
                     y <
                        lensBounds.Bottom;
                     y += 4)
                {
                    graphics.DrawLine(
                        horizontal,
                        lensBounds.Left + 1,
                        y,
                        lensBounds.Right - 1,
                        y);
                }
            }

            graphics.Restore(
                state);
        }

        private static GraphicsPath
            CreateRoundedRectanglePath(
                Rectangle bounds,
                int radius)
        {
            GraphicsPath path =
                new GraphicsPath();

            radius =
                Math.Max(
                    1,
                    Math.Min(
                        radius,
                        Math.Min(
                            bounds.Width,
                            bounds.Height) /
                        2));

            int diameter =
                radius * 2;

            Rectangle arc =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    diameter,
                    diameter);

            path.AddArc(
                arc,
                180,
                90);

            arc.X =
                bounds.Right -
                diameter;

            path.AddArc(
                arc,
                270,
                90);

            arc.Y =
                bounds.Bottom -
                diameter;

            path.AddArc(
                arc,
                0,
                90);

            arc.X =
                bounds.Left;

            path.AddArc(
                arc,
                90,
                90);

            path.CloseFigure();

            return path;
        }

        private void DrawControlButton(
            Graphics graphics,
            Rectangle bounds,
            string text,
            bool active)
        {
            Color accent =
                active
                    ? Color.FromArgb(
                        255,
                        210,
                        55)
                    : Color.FromArgb(
                        125,
                        160,
                        165);

            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    bounds,
                    active
                        ? Color.FromArgb(
                            125,
                            105,
                            24)
                        : Color.FromArgb(
                            58,
                            63,
                            59),
                    Color.FromArgb(
                        22,
                        25,
                        23),
                    LinearGradientMode.Vertical))
            using (Pen pen =
                new Pen(
                    accent,
                    1.0f))
            {
                graphics.FillRectangle(
                    brush,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);
            }

            TextRenderer.DrawText(
                graphics,
                text,
                _smallFont,
                bounds,
                accent,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private void OnLinkStateTimerTick(
            object sender,
            EventArgs e)
        {
            bool previousLinkOk =
                IsLampActive(
                    "comm.link_ok");

            bool previousLinkLost =
                IsLampActive(
                    "comm.link_lost");

            _alarmFlashOn =
                !_alarmFlashOn;

            EvaluateLinkIndicators();
            EvaluateTimedEventIndicators();
            EvaluateGuidanceIndicators();
            EvaluateSystemsIndicators();
            EvaluateAbortRecommendation();
            EvaluateMasterIndicators();

            bool linkChanged =
                previousLinkOk !=
                    IsLampActive(
                        "comm.link_ok") ||
                previousLinkLost !=
                    IsLampActive(
                        "comm.link_lost");

            if (linkChanged ||
                HasUnacknowledgedMasterAlarm() ||
                IsTimedEventActive(
                    _stageSeparationUntilUtc) ||
                IsTimedEventActive(
                    _srbSeparationUntilUtc) ||
                _sasStateReceiver.IsSasEnabled ||
                _systemsStateReceiver
                    .GetSnapshot()
                    .Online)
            {
                Invalidate();
            }
        }

        private void EvaluateLiveIndicators()
        {
            ClearLiveIndicators();

            bool linkOnline =
                IsLinkOnline();

            SetLampActive(
                "comm.link_ok",
                linkOnline);

            SetLampActive(
                "comm.link_lost",
                !linkOnline);

            EvaluateFlightPhaseIndicators();
            EvaluatePropulsionIndicators();
            EvaluateTimedEventIndicators();
            EvaluateGuidanceIndicators();
            EvaluateResourceIndicators();
            EvaluateLoadIndicators();
            EvaluateSystemsIndicators();
            EvaluateAbortRecommendation();
            EvaluateMasterIndicators();
        }

        private void EvaluateLinkIndicators()
        {
            bool linkOnline =
                IsLinkOnline();

            SetLampActive(
                "comm.link_ok",
                linkOnline);

            SetLampActive(
                "comm.link_lost",
                !linkOnline);
        }

        private void UpdateTimedMissionEvents()
        {
            int currentStage =
                _telemetry.CurrentStage;

            if (!_stageTrackingInitialized)
            {
                _stageTrackingInitialized =
                    true;

                _previousStage =
                    currentStage;
            }
            else
            {
                if (currentStage <
                    _previousStage)
                {
                    _stageSeparationUntilUtc =
                        DateTime.UtcNow +
                        TimeSpan.FromSeconds(
                            TimedEventSeconds);
                }

                _previousStage =
                    currentStage;
            }

            SolidFuelTelemetrySnapshot solidFuel =
                SolidFuelTelemetryStore.GetSnapshot();

            int boosterCount =
                Math.Max(
                    0,
                    solidFuel.BoosterCount);

            if (!_srbTrackingInitialized)
            {
                _srbTrackingInitialized =
                    true;

                _previousSrbBoosterCount =
                    boosterCount;
            }
            else
            {
                if (_previousSrbBoosterCount > 0 &&
                    boosterCount <
                        _previousSrbBoosterCount)
                {
                    _srbSeparationUntilUtc =
                        DateTime.UtcNow +
                        TimeSpan.FromSeconds(
                            TimedEventSeconds);
                }

                _previousSrbBoosterCount =
                    boosterCount;
            }
        }

        private void EvaluateTimedEventIndicators()
        {
            DateTime nowUtc =
                DateTime.UtcNow;

            SetLampActive(
                "prop.stage_separation",
                IsTimedEventActive(
                    _stageSeparationUntilUtc,
                    nowUtc));

            SetLampActive(
                "prop.srb_separation",
                IsTimedEventActive(
                    _srbSeparationUntilUtc,
                    nowUtc));
        }

        private void EvaluateGuidanceIndicators()
        {
            SetLampActive(
                "guidance.sas",
                _sasStateReceiver.IsSasEnabled);
        }

        private static bool IsTimedEventActive(
            DateTime untilUtc)
        {
            return IsTimedEventActive(
                untilUtc,
                DateTime.UtcNow);
        }

        private static bool IsTimedEventActive(
            DateTime untilUtc,
            DateTime nowUtc)
        {
            return
                untilUtc !=
                    DateTime.MinValue &&
                nowUtc <
                    untilUtc;
        }

        private void EvaluateFlightPhaseIndicators()
        {
            double radarAltitude =
                Math.Max(
                    0.0,
                    _telemetry.RadarAltitude);

            double verticalSpeed =
                _telemetry.VerticalSpeed;

            double horizontalSpeed =
                Math.Abs(
                    _telemetry.HorizontalSpeed);

            bool landed =
                radarAltitude <= 5.0 &&
                Math.Abs(
                    verticalSpeed) <= 2.0 &&
                horizontalSpeed <= 2.0;

            bool orbit =
                !landed &&
                _telemetry.Apoapsis > 0.0 &&
                _telemetry.Periapsis > 0.0;

            bool ascent =
                !landed &&
                !orbit &&
                verticalSpeed >= 5.0;

            bool descent =
                !landed &&
                !orbit &&
                verticalSpeed <= -5.0;

            SetLampActive(
                "phase.landed",
                landed);

            SetLampActive(
                "phase.orbit",
                orbit);

            SetLampActive(
                "phase.ascent",
                ascent);

            SetLampActive(
                "phase.descent",
                descent);
        }

        private void EvaluatePropulsionIndicators()
        {
            System.Collections.Generic.Dictionary<
                uint,
                EngineStateTelemetry> engines =
                    EngineStateTelemetryStore.GetSnapshot();

            bool hasPerEngineTelemetry =
                engines.Count > 0;

            bool liquidIgnited =
                false;

            bool liquidProducing =
                false;

            bool liquidFlameout =
                false;

            foreach (
                EngineStateTelemetry engine
                in engines.Values)
            {
                if (engine == null ||
                    engine.IsSolidBooster)
                {
                    continue;
                }

                if (engine.OperatingState ==
                        EngineOperatingState.Ignited ||
                    engine.OperatingState ==
                        EngineOperatingState.Producing)
                {
                    liquidIgnited =
                        true;
                }

                if (engine.OperatingState ==
                        EngineOperatingState.Producing &&
                    engine.CurrentThrust >
                        0.05)
                {
                    liquidProducing =
                        true;
                }

                if (engine.OperatingState ==
                    EngineOperatingState.Flameout)
                {
                    liquidFlameout =
                        true;
                }
            }

            if (!hasPerEngineTelemetry)
            {
                /*
                 * Preserve useful behavior during receiver startup. Once the
                 * first per-engine packet arrives, propulsion lamps become
                 * fully type-aware and SRBs cannot trigger liquid-engine
                 * indications.
                 */
                liquidIgnited =
                    _telemetry.IgnitedEngineCount >
                        0;

                liquidProducing =
                    _telemetry.ProducingThrustEngineCount >
                        0 &&
                    _telemetry.CurrentThrust >
                        0.05;

                liquidFlameout =
                    _telemetry.FlameoutEngineCount >
                        0;
            }

            SetLampActive(
                "prop.engine_ignition",
                liquidIgnited);

            SetLampActive(
                "prop.main_engine",
                liquidProducing);

            SolidFuelTelemetrySnapshot solidFuel =
                SolidFuelTelemetryStore.GetSnapshot();

            SetLampActive(
                "prop.srb_burn",
                solidFuel.BurningBoosterCount >
                    0);

            SetLampActive(
                "prop.flameout",
                liquidFlameout);

            SetLampActive(
                "prop.engine_fault",
                liquidFlameout);
        }

        private void EvaluateSystemsIndicators()
        {
            SystemsStateSnapshot systems =
                _systemsStateReceiver.GetSnapshot();

            SetLampActive(
                "power.low",
                systems.Online &&
                systems.ElectricChargeCapacity > 0.0001 &&
                systems.ElectricChargeFraction <= 0.15);

            SetLampActive(
                "flight.heat",
                systems.Online &&
                systems.MaximumThermalRatio >= 0.90);

            SetLampActive(
                "vessel.docked",
                systems.Docked);
        }

        private void EvaluateAbortRecommendation()
        {
            /*
             * ABORT REQ is intentionally disabled.
             *
             * Mission intent is not yet available to KMC, so a generic abort
             * recommendation can be misleading for suborbital, atmospheric,
             * test, or intentionally non-orbital vehicles. The lamp remains
             * installed and participates in LAMP TEST, but runtime telemetry
             * cannot illuminate it until a future mission-profile system is
             * introduced.
             */
            SetLampActive(
                "flight.abort",
                false);
        }

        private void EvaluateResourceIndicators()
        {
            SetLampActive(
                "resource.low_lf",
                IsLowResource(
                    _telemetry.StageLiquidFuelAmount,
                    _telemetry.StageLiquidFuelCapacity));

            SetLampActive(
                "resource.low_ox",
                IsLowResource(
                    _telemetry.StageOxidizerAmount,
                    _telemetry.StageOxidizerCapacity));

            SetLampActive(
                "resource.low_mono",
                IsLowResource(
                    _telemetry.StageMonopropellantAmount,
                    _telemetry.StageMonopropellantCapacity));
        }

        private void EvaluateLoadIndicators()
        {
            SetLampActive(
                "flight.gforce",
                _telemetry.GForce >= 5.0);
        }

        private void EvaluateMasterIndicators()
        {
            bool warningCondition =
                HasCurrentWarningCondition();

            bool cautionCondition =
                HasCurrentCautionCondition();

            /*
             * A new occurrence always re-arms the master alarm, even if an
             * earlier occurrence was acknowledged.
             */
            if (warningCondition &&
                !_previousWarningCondition)
            {
                _masterWarningLatched =
                    true;

                _masterWarningAcknowledged =
                    false;

                _alarmFlashOn =
                    true;
            }

            if (cautionCondition &&
                !_previousCautionCondition)
            {
                _masterCautionLatched =
                    true;

                _masterCautionAcknowledged =
                    false;

                _alarmFlashOn =
                    true;
            }

            if (warningCondition)
            {
                _masterWarningLatched =
                    true;
            }
            else if (_masterWarningAcknowledged)
            {
                _masterWarningLatched =
                    false;

                _masterWarningAcknowledged =
                    false;
            }

            if (cautionCondition)
            {
                _masterCautionLatched =
                    true;
            }
            else if (_masterCautionAcknowledged)
            {
                _masterCautionLatched =
                    false;

                _masterCautionAcknowledged =
                    false;
            }

            _previousWarningCondition =
                warningCondition;

            _previousCautionCondition =
                cautionCondition;

            SetLampActive(
                "master.warning",
                _masterWarningLatched);

            SetLampActive(
                "master.caution",
                _masterCautionLatched);
        }

        private void AcknowledgeMasterAlarms()
        {
            bool warningCondition =
                HasCurrentWarningCondition();

            bool cautionCondition =
                HasCurrentCautionCondition();

            if (_masterWarningLatched)
            {
                _masterWarningAcknowledged =
                    true;

                if (!warningCondition)
                {
                    _masterWarningLatched =
                        false;

                    _masterWarningAcknowledged =
                        false;
                }
            }

            if (_masterCautionLatched)
            {
                _masterCautionAcknowledged =
                    true;

                if (!cautionCondition)
                {
                    _masterCautionLatched =
                        false;

                    _masterCautionAcknowledged =
                        false;
                }
            }

            SetLampActive(
                "master.warning",
                _masterWarningLatched);

            SetLampActive(
                "master.caution",
                _masterCautionLatched);

            Invalidate();
        }

        private bool HasCurrentWarningCondition()
        {
            return
                IsLampActive(
                    "prop.engine_fault") ||
                IsLampActive(
                    "comm.link_lost") ||
                IsLampActive(
                    "prop.flameout") ||
                IsLampActive(
                    "flight.heat");
        }

        private bool HasCurrentCautionCondition()
        {
            return
                IsLampActive(
                    "resource.low_lf") ||
                IsLampActive(
                    "resource.low_ox") ||
                IsLampActive(
                    "resource.low_mono") ||
                IsLampActive(
                    "flight.gforce") ||
                IsLampActive(
                    "power.low");
        }

        private bool HasUnacknowledgedMasterAlarm()
        {
            return
                (_masterWarningLatched &&
                 !_masterWarningAcknowledged) ||
                (_masterCautionLatched &&
                 !_masterCautionAcknowledged);
        }

        private bool ShouldIlluminateLamp(
            LampDefinition lamp)
        {
            if (lamp == null ||
                !lamp.Active)
            {
                return false;
            }

            if (string.Equals(
                    lamp.Id,
                    "master.warning",
                    StringComparison.Ordinal))
            {
                return
                    _masterWarningAcknowledged ||
                    _alarmFlashOn;
            }

            if (string.Equals(
                    lamp.Id,
                    "master.caution",
                    StringComparison.Ordinal))
            {
                return
                    _masterCautionAcknowledged ||
                    _alarmFlashOn;
            }

            return true;
        }

        private void ClearLiveIndicators()
        {
            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                _lamps[index].Active =
                    false;
            }
        }

        private bool IsLinkOnline()
        {
            return
                _lastTelemetryUtc !=
                    DateTime.MinValue &&
                DateTime.UtcNow -
                    _lastTelemetryUtc <
                TimeSpan.FromSeconds(
                    2.0);
        }

        private static bool IsLowResource(
            double amount,
            double capacity)
        {
            if (capacity <= 0.0001)
            {
                return false;
            }

            return
                amount /
                capacity <=
                0.15;
        }

        private void SetLampActive(
            string id,
            bool active)
        {
            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                if (string.Equals(
                        _lamps[index].Id,
                        id,
                        StringComparison.Ordinal))
                {
                    _lamps[index].Active =
                        active;

                    return;
                }
            }
        }

        private bool IsLampActive(
            string id)
        {
            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                if (string.Equals(
                        _lamps[index].Id,
                        id,
                        StringComparison.Ordinal))
                {
                    return
                        _lamps[index].Active;
                }
            }

            return false;
        }

        private string GetPanelStatusText()
        {
            int activeCount =
                0;

            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                if (_lamps[index].Active)
                {
                    activeCount++;
                }
            }

            int unacknowledgedCount =
                0;

            if (_masterWarningLatched &&
                !_masterWarningAcknowledged)
            {
                unacknowledgedCount++;
            }

            if (_masterCautionLatched &&
                !_masterCautionAcknowledged)
            {
                unacknowledgedCount++;
            }

            return
                activeCount.ToString("00") +
                " ACTIVE  •  " +
                unacknowledgedCount.ToString("00") +
                " UNACK";
        }

        private void StartLampTest()
        {
            _lampTestActive = true;

            _lampTestTimer.Stop();
            _lampTestTimer.Start();

            Invalidate();
        }

        private void OnLampTestTimerTick(
            object sender,
            EventArgs e)
        {
            _lampTestTimer.Stop();

            _lampTestActive = false;

            Invalidate();
        }

        private static LampDefinition[]
            CreateLampDefinitions()
        {
            return new[]
            {
                new LampDefinition(
                    "master.caution",
                    "MASTER\nCAUTION",
                    LampColor.Amber),
                new LampDefinition(
                    "master.warning",
                    "MASTER\nWARNING",
                    LampColor.Red),
                new LampDefinition(
                    "prop.engine_fault",
                    "ENGINE\nFAULT",
                    LampColor.Red),
                new LampDefinition(
                    "power.low",
                    "LOW\nPOWER",
                    LampColor.Amber),
                new LampDefinition(
                    "comm.link_lost",
                    "LINK\nLOST",
                    LampColor.Red),
                new LampDefinition(
                    "flight.abort",
                    "ABORT\nREQ",
                    LampColor.Red),
                new LampDefinition(
                    "phase.ascent",
                    "ASCENT",
                    LampColor.Blue),
                new LampDefinition(
                    "phase.orbit",
                    "ORBIT",
                    LampColor.Blue),
                new LampDefinition(
                    "phase.descent",
                    "DESCENT",
                    LampColor.Blue),
                new LampDefinition(
                    "phase.landed",
                    "LANDED",
                    LampColor.Green),
                new LampDefinition(
                    "vessel.docked",
                    "DOCKED",
                    LampColor.Green),
                new LampDefinition(
                    "comm.link_ok",
                    "LINK\nOK",
                    LampColor.Green),

                new LampDefinition(
                    "prop.engine_ignition",
                    "ENG\nIGN",
                    LampColor.Blue),
                new LampDefinition(
                    "prop.main_engine",
                    "MAIN\nENG",
                    LampColor.Green),
                new LampDefinition(
                    "prop.srb_burn",
                    "SRB\nBURN",
                    LampColor.Amber),
                new LampDefinition(
                    "prop.srb_separation",
                    "SRB\nSEP",
                    LampColor.Green),
                new LampDefinition(
                    "prop.stage_separation",
                    "STAGE\nSEP",
                    LampColor.Green),
                new LampDefinition(
                    "prop.flameout",
                    "FLAMEOUT",
                    LampColor.Red),
                new LampDefinition(
                    "resource.low_lf",
                    "LOW LF",
                    LampColor.Amber),
                new LampDefinition(
                    "resource.low_ox",
                    "LOW OX",
                    LampColor.Amber),
                new LampDefinition(
                    "resource.low_mono",
                    "LOW\nMONO",
                    LampColor.Amber),
                new LampDefinition(
                    "flight.heat",
                    "HEAT\nHIGH",
                    LampColor.Red),
                new LampDefinition(
                    "flight.gforce",
                    "G FORCE",
                    LampColor.Amber),
                new LampDefinition(
                    "guidance.sas",
                    "SAS ON",
                    LampColor.Blue)
            };
        }

        private static Color GetLampColor(
            LampColor color)
        {
            switch (color)
            {
                case LampColor.Blue:
                    return Color.FromArgb(
                        58,
                        88,
                        160);

                case LampColor.Green:
                    return Color.FromArgb(
                        126,
                        176,
                        55);

                case LampColor.Amber:
                    return Color.FromArgb(
                        224,
                        151,
                        28);

                case LampColor.Red:
                    return Color.FromArgb(
                        206,
                        57,
                        35);

                default:
                    return Color.White;
            }
        }

        private static Color GetReadableTextColor(
            LampColor color)
        {
            switch (color)
            {
                case LampColor.Blue:
                case LampColor.Red:
                    return Color.White;

                default:
                    return Color.FromArgb(
                        12,
                        16,
                        13);
            }
        }

        private static Color Lighten(
            Color color,
            double amount)
        {
            amount =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        amount));

            return Color.FromArgb(
                color.A,
                color.R +
                (int)
                ((255 - color.R) *
                 amount),
                color.G +
                (int)
                ((255 - color.G) *
                 amount),
                color.B +
                (int)
                ((255 - color.B) *
                 amount));
        }

        private static Color Darken(
            Color color,
            double amount)
        {
            amount =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        amount));

            return Color.FromArgb(
                color.A,
                (int)
                (color.R *
                 (1.0 - amount)),
                (int)
                (color.G *
                 (1.0 - amount)),
                (int)
                (color.B *
                 (1.0 - amount)));
        }

        private static void DrawFastener(
            Graphics graphics,
            int x,
            int y)
        {
            Rectangle bounds =
                new Rectangle(
                    x,
                    y,
                    8,
                    8);

            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(
                        170,
                        174,
                        164),
                    Color.FromArgb(
                        45,
                        48,
                        44),
                    LinearGradientMode.Vertical))
            using (Pen outline =
                new Pen(
                    Color.FromArgb(
                        18,
                        20,
                        18)))
            using (Pen slot =
                new Pen(
                    Color.FromArgb(
                        35,
                        37,
                        34)))
            {
                graphics.FillEllipse(
                    brush,
                    bounds);

                graphics.DrawEllipse(
                    outline,
                    bounds);

                graphics.DrawLine(
                    slot,
                    x + 2,
                    y + 6,
                    x + 6,
                    y + 2);
            }
        }
    }
}
