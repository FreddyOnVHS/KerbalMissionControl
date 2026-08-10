using KMC.MissionControl.Controls;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.MissionControl.Telemetry;
using KMC.MissionControl.Themes;
using KMC.Shared;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FormsTimer = System.Windows.Forms.Timer;
using Label = System.Windows.Forms.Label;

namespace KMC.MissionControl
{
    public sealed class MainForm : Form
    {
        private const int OuterMargin = 16;
        private const int HeaderHeight = 52;
        private const int SectionSpacing = 16;
        private const int NavigationHeight = 44;
        private const int NormalSummaryHeight = 110;
        private const int CompactSummaryHeight = 0;
        private const int CompactHeightBreakpoint = 1050;
        private const int HideSummaryHeightBreakpoint = 950;
        private const int MinimumDisplayRefreshRate = 2;
        private const int MaximumDisplayRefreshRate = 20;
        private const int DefaultDisplayRefreshRate = 10;
        private const int WmEnterSizeMove = 0x0231;
        private const int WmExitSizeMove = 0x0232;

        private readonly TableLayoutPanel _rootLayout;
        private readonly MissionControlReceiver _receiver;
        private readonly LatestTelemetryBuffer _telemetryBuffer;
        private readonly FormsTimer _connectionTimer;
        private readonly FormsTimer _displayRefreshTimer;
        private readonly FormsTimer _performanceOverlayTimer;
        private readonly Label _connectionLabel;
        private readonly Label _displayRefreshLabel;
        private readonly Button _maneuverUploadButton;
        private readonly TrackBar _displayRefreshSlider;
        private readonly ConsolePanel _displayPanel;
        private readonly MissionDisplay _missionDisplay;
        private readonly NavigationBar _navigationBar;
        private readonly MissionSummary _missionSummary;
        private readonly PerformanceOverlay _performanceOverlay;

        private long _lastDisplayedPacketSequence;
        private long _displayedPacketCount;
        private DateTime _lastPerformanceReportUtc;
        private bool _isMovingOrResizing;

        public MainForm()
        {
            Text = "KMC - Kerbal Mission Control";

            Size preferredClientSize =
                new Size(1920, 1080);

            Size minimumClientSize =
                new Size(1440, 900);

            ClientSize = preferredClientSize;
            MinimumSize = SizeFromClientSize(minimumClientSize);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = ApolloTheme.WindowBackground;
            ForeColor = Color.FromArgb(190, 255, 190);
            Font = new Font("Consolas", 12.0f, FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.Dpi;

            _connectionLabel = CreateConnectionLabel();
            _displayRefreshLabel = CreateDisplayRefreshLabel();
            _maneuverUploadButton = CreateManeuverUploadButton();
            _displayRefreshSlider = CreateDisplayRefreshSlider();

            _displayPanel =
                new ConsolePanel
                {
                    PanelTitle = "ASCENT DISPLAY",
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty
                };

            _missionDisplay =
                new MissionDisplay
                {
                    ScreenTitle = "ASCENT DATA",
                    PhosphorMode = CrtPhosphorMode.Blue,
                    ShowScanLines = true,
                    ShowScalingDiagnostics = false,
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    MinimumSize = new Size(320, 180)
                };

            _navigationBar =
                new NavigationBar
                {
                    Dock = DockStyle.Top,
                    Height = NavigationHeight,
                    Margin = Padding.Empty
                };

            ConfigureNavigation();

            MissionTelemetry initialTelemetry =
                new MissionTelemetry();

            _missionDisplay.SetPage(new AscentPage());
            _missionDisplay.UpdateTelemetry(initialTelemetry);

            _displayPanel.Controls.Add(_missionDisplay);
            _displayPanel.Controls.Add(_navigationBar);

            _missionSummary =
                new MissionSummary
                {
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    MinimumSize = new Size(320, 180)
                };

            _missionSummary.UpdateTelemetry(initialTelemetry);

            _performanceOverlay =
                new PerformanceOverlay
                {
                    Left = ClientSize.Width - 410,
                    Top = HeaderHeight + OuterMargin + 12
                };

            _rootLayout = CreateMainLayout();

            Controls.Add(_rootLayout);
            Controls.Add(_performanceOverlay);
            _performanceOverlay.BringToFront();

            Resize += OnMainFormResize;
            UpdateResponsiveLayout();

            _telemetryBuffer = new LatestTelemetryBuffer();

            _receiver = new MissionControlReceiver();
            _receiver.TelemetryReceived += OnTelemetryReceived;
            _receiver.ManeuverAcknowledgmentReceived += OnManeuverAcknowledgmentReceived;

            _displayRefreshTimer = new FormsTimer();
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
            ApplyDisplayRefreshRate(DefaultDisplayRefreshRate);

            _connectionTimer =
                new FormsTimer
                {
                    Interval = 500
                };

            _connectionTimer.Tick += OnConnectionTimerTick;

            _performanceOverlayTimer =
                new FormsTimer
                {
                    Interval = 250
                };

            _performanceOverlayTimer.Tick +=
                OnPerformanceOverlayTimerTick;

            _lastPerformanceReportUtc =
                DateTime.UtcNow;

            KeyPreview = true;
            KeyDown += OnMainFormKeyDown;
            Load += OnFormLoad;
            FormClosing += OnFormClosing;
        }

        private TableLayoutPanel CreateMainLayout()
        {
            TableLayoutPanel rootLayout =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = ApolloTheme.WindowBackground,
                    Padding = new Padding(OuterMargin),
                    Margin = Padding.Empty,
                    ColumnCount = 1,
                    RowCount = 5
                };

            rootLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            rootLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    HeaderHeight));

            rootLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100.0f));

            rootLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    SectionSpacing));

            rootLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    NormalSummaryHeight));

            rootLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    1.0f));

            Control header = CreateHeader();

            rootLayout.Controls.Add(header, 0, 0);
            rootLayout.Controls.Add(_displayPanel, 0, 1);
            rootLayout.Controls.Add(_missionSummary, 0, 3);

            return rootLayout;
        }

        private Control CreateHeader()
        {
            TableLayoutPanel headerLayout =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = ApolloTheme.WindowBackground,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    ColumnCount = 5,
                    RowCount = 1
                };

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    150.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    180.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    210.0f));

            headerLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100.0f));

            Label titleLabel =
                new Label
                {
                    Text = "KERBAL MISSION CONTROL",
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 20.0f, FontStyle.Bold),
                    AutoEllipsis = true
                };

            headerLayout.Controls.Add(titleLabel, 0, 0);
            headerLayout.Controls.Add(_maneuverUploadButton, 1, 0);
            headerLayout.Controls.Add(_displayRefreshLabel, 2, 0);
            headerLayout.Controls.Add(_displayRefreshSlider, 3, 0);
            headerLayout.Controls.Add(_connectionLabel, 4, 0);

            return headerLayout;
        }

        private static Label CreateConnectionLabel()
        {
            return
                new Label
                {
                    Text = "LINK OFFLINE",
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Color.OrangeRed,
                    Font = new Font("Consolas", 12.0f, FontStyle.Bold)
                };
        }

        private static Label CreateDisplayRefreshLabel()
        {
            return
                new Label
                {
                    Text = "DISPLAY 10 FPS",
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Color.FromArgb(150, 220, 255),
                    Font = new Font("Consolas", 10.0f, FontStyle.Bold)
                };
        }

        private Button CreateManeuverUploadButton()
        {
            Button button =
                new Button
                {
                    Text = "UPLOAD MNV",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 8, 6, 8),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(45, 55, 49),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                    Visible = false,
                    TabStop = false
                };

            button.FlatAppearance.BorderColor =
                Color.FromArgb(120, 150, 125);

            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(58, 70, 62);

            button.Click +=
                OnManeuverUploadClick;

            return button;
        }

        private TrackBar CreateDisplayRefreshSlider()
        {
            TrackBar slider =
                new TrackBar
                {
                    Minimum = MinimumDisplayRefreshRate,
                    Maximum = MaximumDisplayRefreshRate,
                    Value = DefaultDisplayRefreshRate,
                    TickFrequency = 2,
                    SmallChange = 1,
                    LargeChange = 2,
                    AutoSize = false,
                    Height = 34,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(8, 8, 8, 6),
                    BackColor = ApolloTheme.WindowBackground
                };

            slider.ValueChanged +=
                OnDisplayRefreshSliderValueChanged;

            return slider;
        }

        private void ConfigureNavigation()
        {
            _navigationBar.AddPage(
                "ASCENT",
                new AscentPage());

            _navigationBar.AddPage(
                "ORBIT",
                new OrbitPage());

            _navigationBar.AddPage(
                "MNV",
                new ManeuverPage());

            _navigationBar.AddPage(
                "PROP",
                new PropulsionPage());

            _navigationBar.AddPage(
                "GUID",
                new AscentPage(),
                enabled: false);

            _navigationBar.AddPage(
                "POWER",
                new PowerPage());

            _navigationBar.AddPage(
                "COMM",
                new AscentPage(),
                enabled: false);

            _navigationBar.AddPage(
                "SYS",
                new AscentPage(),
                enabled: false);

            _navigationBar.AddPage(
                "MAP",
                new AscentPage(),
                enabled: false);

            _navigationBar.PageChanged +=
                OnPageChanged;
        }

        private void OnPageChanged(
            IMissionPage page,
            string title)
        {
            _missionDisplay.SetPage(page);
            _missionDisplay.ScreenTitle = title + " DATA";
            _displayPanel.PanelTitle = title + " DISPLAY";

            _maneuverUploadButton.Visible =
                string.Equals(
                    title,
                    "MNV",
                    StringComparison.OrdinalIgnoreCase);
        }

        private void OnManeuverUploadClick(
            object sender,
            EventArgs e)
        {
            if (_receiver == null)
            {
                return;
            }

            string resultText;

            _receiver.UploadLatestManeuver(
                out resultText);

            _missionDisplay.RequestRender();
        }

        private void OnManeuverAcknowledgmentReceived(
            ManeuverUplinkAck ack)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<ManeuverUplinkAck>(
                        OnManeuverAcknowledgmentReceived),
                    ack);

                return;
            }

            _missionDisplay.RequestRender();
        }

        private void OnMainFormResize(
            object sender,
            EventArgs e)
        {
            UpdateResponsiveLayout();

            if (!_isMovingOrResizing)
            {
                _missionDisplay.RequestRender();
            }
        }

        protected override void WndProc(
            ref Message message)
        {
            if (message.Msg == WmEnterSizeMove)
            {
                BeginInteractiveMoveResize();
            }

            base.WndProc(ref message);

            if (message.Msg == WmExitSizeMove)
            {
                EndInteractiveMoveResize();
            }
        }

        private void BeginInteractiveMoveResize()
        {
            if (_isMovingOrResizing)
            {
                return;
            }

            _isMovingOrResizing = true;
            _displayRefreshTimer.Stop();
            _missionDisplay.SuspendRendering();
        }

        private void EndInteractiveMoveResize()
        {
            if (!_isMovingOrResizing)
            {
                return;
            }

            _isMovingOrResizing = false;
            RefreshLatestTelemetry();

            _missionDisplay.ResumeRendering(
                renderImmediately: true);

            _displayRefreshTimer.Start();
        }

        private void UpdateResponsiveLayout()
        {
            if (_rootLayout == null ||
                _missionSummary == null)
            {
                return;
            }

            int availableHeight =
                ClientSize.Height;

            if (availableHeight <
                HideSummaryHeightBreakpoint)
            {
                SetSummaryLayout(
                    visible: false,
                    height: 0);
            }
            else if (availableHeight <
                     CompactHeightBreakpoint)
            {
                SetSummaryLayout(
                    visible: true,
                    height: CompactSummaryHeight);
            }
            else
            {
                SetSummaryLayout(
                    visible: true,
                    height: NormalSummaryHeight);
            }
        }

        private void SetSummaryLayout(
            bool visible,
            int height)
        {
            _rootLayout.SuspendLayout();

            try
            {
                _missionSummary.Visible = visible;

                _rootLayout.RowStyles[3].Height =
                    Math.Max(0, height);

                _rootLayout.RowStyles[2].Height =
                    visible
                        ? SectionSpacing
                        : 0;
            }
            finally
            {
                _rootLayout.ResumeLayout(
                    performLayout: true);
            }
        }

        private void OnFormLoad(
            object sender,
            EventArgs e)
        {
            try
            {
                _receiver.Start();
                _displayRefreshTimer.Start();
                _connectionTimer.Start();
                _performanceOverlayTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Mission Control could not start " +
                    "the telemetry receiver.\n\n" +
                    ex.Message,
                    "KMC Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnTelemetryReceived(
            TelemetryPacket packet)
        {
            _telemetryBuffer.Publish(packet);
        }

        private void OnDisplayRefreshTimerTick(
            object sender,
            EventArgs e)
        {
            RefreshLatestTelemetry();
            ReportPerformanceIfDue();
        }

        private bool RefreshLatestTelemetry()
        {
            TelemetryPacket packet;

            if (!_telemetryBuffer.TryReadLatest(
                    ref _lastDisplayedPacketSequence,
                    out packet))
            {
                return false;
            }

            MissionTelemetry telemetry =
                CreateMissionTelemetry(packet);

            if (_missionDisplay.Visible)
            {
                _missionDisplay.UpdateTelemetry(telemetry);
            }

            if (_missionSummary.Visible &&
                _missionSummary.Height > 0)
            {
                _missionSummary.UpdateTelemetry(telemetry);
            }

            _displayedPacketCount++;

            return true;
        }

        private void OnDisplayRefreshSliderValueChanged(
            object sender,
            EventArgs e)
        {
            ApplyDisplayRefreshRate(
                _displayRefreshSlider.Value);
        }

        private void ApplyDisplayRefreshRate(
            int refreshRate)
        {
            int clampedRate =
                Math.Max(
                    MinimumDisplayRefreshRate,
                    Math.Min(
                        MaximumDisplayRefreshRate,
                        refreshRate));

            _displayRefreshTimer.Interval =
                Math.Max(
                    1,
                    (int)Math.Round(
                        1000.0 /
                        clampedRate));

            _displayRefreshLabel.Text =
                "DISPLAY " +
                clampedRate.ToString() +
                " FPS";
        }

        private static MissionTelemetry CreateMissionTelemetry(
            TelemetryPacket packet)
        {
            return
                new MissionTelemetry
                {
                    VesselName = packet.VesselName,
                    BodyName = packet.BodyName,
                    MissionTime = packet.MissionTime,
                    Altitude = packet.Altitude,
                    RadarAltitude = packet.RadarAltitude,
                    Apoapsis = packet.Apoapsis,
                    Periapsis = packet.Periapsis,
                    TimeToApoapsis = packet.TimeToApoapsis,
                    TimeToPeriapsis = packet.TimeToPeriapsis,
                    Eccentricity = packet.Eccentricity,
                    SemiMajorAxis = packet.SemiMajorAxis,
                    TrueAnomalyDegrees = packet.TrueAnomalyDegrees,
                    ArgumentOfPeriapsisDegrees = packet.ArgumentOfPeriapsisDegrees,
                    InclinationDegrees = packet.InclinationDegrees,
                    LongitudeOfAscendingNodeDegrees = packet.LongitudeOfAscendingNodeDegrees,
                    OrbitalPeriod = packet.OrbitalPeriod,
                    SurfaceSpeed = packet.SurfaceSpeed,
                    HorizontalSpeed = packet.HorizontalSpeed,
                    VerticalSpeed = packet.VerticalSpeed,
                    OrbitalSpeed = packet.OrbitalSpeed,
                    Throttle = packet.Throttle,
                    CurrentStage = packet.CurrentStage,
                    GForce = packet.GForce,
                    Pitch = packet.Pitch,
                    Heading = packet.Heading,
                    Roll = packet.Roll,
                    DynamicPressureKpa = packet.DynamicPressureKpa,
                    StaticPressureKpa = packet.StaticPressureKpa,
                    Mach = packet.Mach,
                    VesselMass = packet.VesselMass,
                    CurrentThrust = packet.CurrentThrust,
                    MaximumThrust = packet.MaximumThrust,
                    ThrustToWeightRatio = packet.ThrustToWeightRatio,
                    EngineCount = packet.EngineCount,
                    IgnitedEngineCount = packet.IgnitedEngineCount,
                    ProducingThrustEngineCount = packet.ProducingThrustEngineCount,
                    FlameoutEngineCount = packet.FlameoutEngineCount,
                    AverageSpecificImpulse = packet.AverageSpecificImpulse,
                    StageLiquidFuelAmount = packet.StageLiquidFuelAmount,
                    StageLiquidFuelCapacity = packet.StageLiquidFuelCapacity,
                    StageOxidizerAmount = packet.StageOxidizerAmount,
                    StageOxidizerCapacity = packet.StageOxidizerCapacity,
                    StageMonopropellantAmount = packet.StageMonopropellantAmount,
                    StageMonopropellantCapacity = packet.StageMonopropellantCapacity,
                    TotalLiquidFuelAmount = packet.TotalLiquidFuelAmount,
                    TotalLiquidFuelCapacity = packet.TotalLiquidFuelCapacity,
                    TotalOxidizerAmount = packet.TotalOxidizerAmount,
                    TotalOxidizerCapacity = packet.TotalOxidizerCapacity,
                    TotalMonopropellantAmount = packet.TotalMonopropellantAmount,
                    TotalMonopropellantCapacity = packet.TotalMonopropellantCapacity
                };
        }

        private void OnMainFormKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control &&
                e.Shift &&
                e.KeyCode == Keys.D)
            {
                _performanceOverlay.Visible =
                    !_performanceOverlay.Visible;

                if (_performanceOverlay.Visible)
                {
                    UpdatePerformanceOverlay();
                    _performanceOverlay.BringToFront();
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void OnPerformanceOverlayTimerTick(
            object sender,
            EventArgs e)
        {
            if (_performanceOverlay.Visible)
            {
                UpdatePerformanceOverlay();
            }
        }

        private void UpdatePerformanceOverlay()
        {
            DateTime lastReceivedUtc =
                _telemetryBuffer.LastReceivedUtc;

            bool online =
                lastReceivedUtc != default(DateTime) &&
                DateTime.UtcNow - lastReceivedUtc <
                TimeSpan.FromSeconds(2.0);

            PropulsionAnalysisCacheSnapshot propulsionCache =
                PropulsionAnalysisCache.GetSnapshot();

            PerformanceSnapshot snapshot =
                new PerformanceSnapshot
                {
                    SelectedDisplayFps = _displayRefreshSlider.Value,
                    PacketsReceived = _telemetryBuffer.ReceivedPacketCount,
                    PacketsDisplayed = _displayedPacketCount,
                    PacketsSuperseded = _telemetryBuffer.SupersededPacketCount,
                    RenderCount = _missionDisplay.RenderCount,
                    LastRenderMilliseconds = _missionDisplay.LastRenderMilliseconds,
                    AverageRenderMilliseconds = _missionDisplay.AverageRenderMilliseconds,
                    PaintCount = _missionDisplay.PaintCount,
                    LastPaintMilliseconds = _missionDisplay.LastPaintMilliseconds,
                    AveragePaintMilliseconds = _missionDisplay.AveragePaintMilliseconds,
                    BitmapSize = _missionDisplay.VirtualCanvasSize,
                    BitmapBytes = _missionDisplay.CachedBitmapBytes,
                    BitmapAllocationCount = _missionDisplay.BitmapAllocationCount,
                    ManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false),
                    GenerationZeroCollections = GC.CollectionCount(0),
                    GenerationOneCollections = GC.CollectionCount(1),
                    GenerationTwoCollections = GC.CollectionCount(2),
                    RenderingSuspended = _missionDisplay.IsRenderingSuspended,
                    LinkOnline = online,
                    PropulsionCacheHits = propulsionCache.HitCount,
                    PropulsionCacheMisses = propulsionCache.MissCount,
                    PropulsionCacheRebuilds = propulsionCache.RebuildCount,
                    PropulsionCacheLastRebuildMilliseconds = propulsionCache.LastRebuildMilliseconds,
                    PropulsionCacheAverageRebuildMilliseconds = propulsionCache.AverageRebuildMilliseconds,
                    PropulsionCachedTopologyRevision = propulsionCache.CachedTopologyRevision,
                    PropulsionCachedStage = propulsionCache.CachedStage,
                    PropulsionCachedNodeCount = propulsionCache.CachedNodeCount,
                    PropulsionCachedVesselName = propulsionCache.CachedVesselName,
                    HasPropulsionCache = propulsionCache.HasCachedAnalysis
                };

            _performanceOverlay.UpdateSnapshot(snapshot);
        }

        private void OnConnectionTimerTick(
            object sender,
            EventArgs e)
        {
            DateTime lastReceivedUtc =
                _telemetryBuffer.LastReceivedUtc;

            bool online =
                lastReceivedUtc != default(DateTime) &&
                DateTime.UtcNow - lastReceivedUtc <
                TimeSpan.FromSeconds(2.0);

            _connectionLabel.Text =
                online
                    ? "LINK ONLINE"
                    : "LINK OFFLINE";

            _connectionLabel.ForeColor =
                online
                    ? Color.LimeGreen
                    : Color.OrangeRed;
        }

        private void ReportPerformanceIfDue()
        {
            DateTime nowUtc =
                DateTime.UtcNow;

            if (nowUtc -
                _lastPerformanceReportUtc <
                TimeSpan.FromSeconds(5.0))
            {
                return;
            }

            _lastPerformanceReportUtc =
                nowUtc;

            Debug.WriteLine(
                "[KMC PERFORMANCE] " +
                "received=" +
                _telemetryBuffer.ReceivedPacketCount +
                " displayed=" +
                _displayedPacketCount +
                " superseded=" +
                _telemetryBuffer.SupersededPacketCount +
                " refresh=" +
                _displayRefreshSlider.Value +
                "fps");
        }

        private void OnFormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            _displayRefreshTimer.Stop();
            _connectionTimer.Stop();
            _performanceOverlayTimer.Stop();

            _receiver.TelemetryReceived -=
                OnTelemetryReceived;

            _receiver.ManeuverAcknowledgmentReceived -=
                OnManeuverAcknowledgmentReceived;

            _receiver.Dispose();
        }
    }
}
