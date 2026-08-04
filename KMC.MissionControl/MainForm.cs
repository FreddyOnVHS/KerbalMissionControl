using KMC.MissionControl.Controls;
using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
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

        private readonly TableLayoutPanel _rootLayout;

        private readonly MissionControlReceiver _receiver;
        private readonly LatestTelemetryBuffer _telemetryBuffer;
        private readonly FormsTimer _connectionTimer;
        private readonly FormsTimer _displayRefreshTimer;

        private readonly Label _connectionLabel;
        private readonly Label _displayRefreshLabel;
        private readonly TrackBar _displayRefreshSlider;
        private readonly ConsolePanel _displayPanel;
        private readonly MissionDisplay _missionDisplay;
        private readonly NavigationBar _navigationBar;
        private readonly MissionSummary _missionSummary;

        private long _lastDisplayedPacketSequence;
        private long _displayedPacketCount;
        private DateTime _lastPerformanceReportUtc;

        public MainForm()
        {
            Text =
                "KMC - Kerbal Mission Control";

            /*
             * Baseline console size. Existing mission-page text and widgets
             * are designed around the CRT area produced by this client size.
             */
            Size preferredClientSize =
                new Size(
                    1920,
                    1080);

            Size minimumClientSize =
                new Size(
                    1440,
                    900);

            ClientSize =
                preferredClientSize;

            MinimumSize =
                SizeFromClientSize(
                    minimumClientSize);

            StartPosition =
                FormStartPosition.CenterScreen;

            WindowState =
                FormWindowState.Maximized;

            BackColor =
                ApolloTheme.WindowBackground;

            ForeColor =
                Color.FromArgb(
                    190,
                    255,
                    190);

            Font =
                new Font(
                    "Consolas",
                    12.0f,
                    FontStyle.Regular);

            AutoScaleMode =
                AutoScaleMode.Dpi;

            _connectionLabel =
                CreateConnectionLabel();

            _displayRefreshLabel =
                CreateDisplayRefreshLabel();

            _displayRefreshSlider =
                CreateDisplayRefreshSlider();

            _displayPanel =
                new ConsolePanel
                {
                    PanelTitle =
                        "ASCENT DISPLAY",
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty
                };

            _missionDisplay =
                new MissionDisplay
                {
                    ScreenTitle =
                        "ASCENT DATA",
                    PhosphorMode =
                        CrtPhosphorMode.Blue,
                    ShowScanLines =
                        true,
                    ShowScalingDiagnostics =
                        false,
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    MinimumSize =
                        new Size(
                            320,
                            180)
                };

            _navigationBar =
                new NavigationBar
                {
                    Dock =
                        DockStyle.Top,
                    Height =
                        NavigationHeight,
                    Margin =
                        Padding.Empty
                };

            ConfigureNavigation();

            MissionTelemetry initialTelemetry =
                new MissionTelemetry();

            _missionDisplay.SetPage(
                new AscentPage());

            _missionDisplay.UpdateTelemetry(
                initialTelemetry);

            /*
             * Docking order matters in WinForms.
             * Add the fill control first and the top control second.
             */
            _displayPanel.Controls.Add(
                _missionDisplay);

            _displayPanel.Controls.Add(
                _navigationBar);

            _missionSummary =
                new MissionSummary
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    MinimumSize =
                        new Size(
                            320,
                            180)
                };

            _missionSummary.UpdateTelemetry(
                initialTelemetry);

            _rootLayout =
                CreateMainLayout();

            Controls.Add(
                _rootLayout);

            Resize +=
                OnMainFormResize;

            UpdateResponsiveLayout();

            _telemetryBuffer =
                new LatestTelemetryBuffer();

            _receiver =
                new MissionControlReceiver();

            _receiver.TelemetryReceived +=
                OnTelemetryReceived;

            _displayRefreshTimer =
                new FormsTimer();

            _displayRefreshTimer.Tick +=
                OnDisplayRefreshTimerTick;

            ApplyDisplayRefreshRate(
                DefaultDisplayRefreshRate);

            _connectionTimer =
                new FormsTimer
                {
                    Interval = 500
                };

            _connectionTimer.Tick +=
                OnConnectionTimerTick;

            _lastPerformanceReportUtc =
                DateTime.UtcNow;

            Load +=
                OnFormLoad;

            FormClosing +=
                OnFormClosing;
        }

        private TableLayoutPanel CreateMainLayout()
        {
            TableLayoutPanel rootLayout =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    Padding =
                        new Padding(
                            OuterMargin),
                    Margin =
                        Padding.Empty,
                    ColumnCount =
                        1,
                    RowCount =
                        5
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

            Control header =
                CreateHeader();

            rootLayout.Controls.Add(
                header,
                0,
                0);

            rootLayout.Controls.Add(
                _displayPanel,
                0,
                1);

            /*
             * Row 2 is intentionally empty and acts as the space between
             * the console display and the mission summary.
             */
            rootLayout.Controls.Add(
                _missionSummary,
                0,
                3);

            return rootLayout;
        }

        private Control CreateHeader()
        {
            TableLayoutPanel headerLayout =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    Margin =
                        Padding.Empty,
                    Padding =
                        Padding.Empty,
                    ColumnCount =
                        4,
                    RowCount =
                        1
                };

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

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
                    Text =
                        "KERBAL MISSION CONTROL",
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    ForeColor =
                        Color.FromArgb(
                            190,
                            255,
                            190),
                    Font =
                        new Font(
                            "Consolas",
                            20.0f,
                            FontStyle.Bold),
                    AutoEllipsis =
                        true
                };

            headerLayout.Controls.Add(
                titleLabel,
                0,
                0);

            headerLayout.Controls.Add(
                _displayRefreshLabel,
                1,
                0);

            headerLayout.Controls.Add(
                _displayRefreshSlider,
                2,
                0);

            headerLayout.Controls.Add(
                _connectionLabel,
                3,
                0);

            return headerLayout;
        }

        private static Label CreateConnectionLabel()
        {
            return new Label
            {
                Text =
                    "LINK OFFLINE",
                Dock =
                    DockStyle.Fill,
                Margin =
                    Padding.Empty,
                TextAlign =
                    ContentAlignment.MiddleRight,
                ForeColor =
                    Color.OrangeRed,
                Font =
                    new Font(
                        "Consolas",
                        12.0f,
                        FontStyle.Bold)
            };
        }

        private static Label CreateDisplayRefreshLabel()
        {
            return new Label
            {
                Text =
                    "DISPLAY 10 FPS",
                Dock =
                    DockStyle.Fill,
                Margin =
                    Padding.Empty,
                TextAlign =
                    ContentAlignment.MiddleRight,
                ForeColor =
                    Color.FromArgb(
                        150,
                        220,
                        255),
                Font =
                    new Font(
                        "Consolas",
                        10.0f,
                        FontStyle.Bold)
            };
        }

        private TrackBar CreateDisplayRefreshSlider()
        {
            TrackBar slider =
                new TrackBar
                {
                    Minimum =
                        MinimumDisplayRefreshRate,
                    Maximum =
                        MaximumDisplayRefreshRate,
                    Value =
                        DefaultDisplayRefreshRate,
                    TickFrequency =
                        2,
                    SmallChange =
                        1,
                    LargeChange =
                        2,
                    AutoSize =
                        false,
                    Height =
                        34,
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        new Padding(
                            8,
                            8,
                            8,
                            6),
                    BackColor =
                        ApolloTheme.WindowBackground
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
                "PROP",
                new PropulsionPage());

            _navigationBar.AddPage(
                "GUID",
                new AscentPage(),
                enabled: false);

            _navigationBar.AddPage(
                "POWER",
                new AscentPage(),
                enabled: false);

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
            _missionDisplay.SetPage(
                page);

            _missionDisplay.ScreenTitle =
                title + " DATA";

            _displayPanel.PanelTitle =
                title + " DISPLAY";
        }

        private void OnMainFormResize(
            object sender,
            EventArgs e)
        {
            UpdateResponsiveLayout();
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
                _missionSummary.Visible =
                    visible;

                _rootLayout.RowStyles[3].Height =
                    Math.Max(
                        0,
                        height);

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Mission Control could not start "
                    + "the telemetry receiver.\n\n"
                    + ex.Message,
                    "KMC Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Receiver-thread callback.
        ///
        /// Do not marshal every packet to the UI thread. Publish it into the
        /// single-slot buffer and let the display timer consume the newest
        /// available state at the selected refresh rate.
        /// </summary>
        private void OnTelemetryReceived(
            TelemetryPacket packet)
        {
            _telemetryBuffer.Publish(
                packet);
        }

        private void OnDisplayRefreshTimerTick(
            object sender,
            EventArgs e)
        {
            TelemetryPacket packet;

            if (!_telemetryBuffer.TryReadLatest(
                ref _lastDisplayedPacketSequence,
                out packet))
            {
                ReportPerformanceIfDue();
                return;
            }

            MissionTelemetry telemetry =
                CreateMissionTelemetry(
                    packet);

            if (_missionDisplay.Visible)
            {
                _missionDisplay.UpdateTelemetry(
                    telemetry);
            }

            /*
             * Hidden controls are deliberately not updated. MissionSummary
             * performs its own invalidation, so skipping this call prevents
             * unnecessary layout and paint work at compact window heights.
             */
            if (_missionSummary.Visible &&
                _missionSummary.Height > 0)
            {
                _missionSummary.UpdateTelemetry(
                    telemetry);
            }

            _displayedPacketCount++;

            ReportPerformanceIfDue();
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

            /*
             * The slider changes only how often the display receives the
             * newest telemetry. It does not reduce antialiasing, interpolation,
             * compositing, text quality, or any other rendering setting.
             */
            _displayRefreshTimer.Interval =
                Math.Max(
                    1,
                    (int)Math.Round(
                        1000.0 /
                        clampedRate));

            _displayRefreshLabel.Text =
                "DISPLAY "
                + clampedRate
                    .ToString()
                + " FPS";
        }

        private static MissionTelemetry
            CreateMissionTelemetry(
                TelemetryPacket packet)
        {
            return new MissionTelemetry
            {
                VesselName =
                    packet.VesselName,

                BodyName =
                    packet.BodyName,

                MissionTime =
                    packet.MissionTime,

                Altitude =
                    packet.Altitude,

                RadarAltitude =
                    packet.RadarAltitude,

                Apoapsis =
                    packet.Apoapsis,

                Periapsis =
                    packet.Periapsis,

                TimeToApoapsis =
                    packet.TimeToApoapsis,

                TimeToPeriapsis =
                    packet.TimeToPeriapsis,

                Eccentricity =
                    packet.Eccentricity,

                SemiMajorAxis =
                    packet.SemiMajorAxis,

                TrueAnomalyDegrees =
                    packet.TrueAnomalyDegrees,

                ArgumentOfPeriapsisDegrees =
                    packet.ArgumentOfPeriapsisDegrees,

                InclinationDegrees =
                    packet.InclinationDegrees,

                LongitudeOfAscendingNodeDegrees =
                    packet.LongitudeOfAscendingNodeDegrees,

                OrbitalPeriod =
                    packet.OrbitalPeriod,

                SurfaceSpeed =
                    packet.SurfaceSpeed,

                HorizontalSpeed =
                    packet.HorizontalSpeed,

                VerticalSpeed =
                    packet.VerticalSpeed,

                OrbitalSpeed =
                    packet.OrbitalSpeed,

                Throttle =
                    packet.Throttle,

                CurrentStage =
                    packet.CurrentStage,

                GForce =
                    packet.GForce,

                Pitch =
                    packet.Pitch,

                Heading =
                    packet.Heading,

                Roll =
                    packet.Roll,

                DynamicPressureKpa =
                    packet.DynamicPressureKpa,

                StaticPressureKpa =
                    packet.StaticPressureKpa,

                Mach =
                    packet.Mach,

                VesselMass =
                    packet.VesselMass,

                CurrentThrust =
                    packet.CurrentThrust,

                MaximumThrust =
                    packet.MaximumThrust,

                ThrustToWeightRatio =
                    packet.ThrustToWeightRatio,

                EngineCount =
                    packet.EngineCount,

                IgnitedEngineCount =
                    packet.IgnitedEngineCount,

                ProducingThrustEngineCount =
                    packet.ProducingThrustEngineCount,

                FlameoutEngineCount =
                    packet.FlameoutEngineCount,

                AverageSpecificImpulse =
                    packet.AverageSpecificImpulse,

                StageLiquidFuelAmount =
                    packet.StageLiquidFuelAmount,

                StageLiquidFuelCapacity =
                    packet.StageLiquidFuelCapacity,

                StageOxidizerAmount =
                    packet.StageOxidizerAmount,

                StageOxidizerCapacity =
                    packet.StageOxidizerCapacity,

                StageMonopropellantAmount =
                    packet.StageMonopropellantAmount,

                StageMonopropellantCapacity =
                    packet.StageMonopropellantCapacity,

                TotalLiquidFuelAmount =
                    packet.TotalLiquidFuelAmount,

                TotalLiquidFuelCapacity =
                    packet.TotalLiquidFuelCapacity,

                TotalOxidizerAmount =
                    packet.TotalOxidizerAmount,

                TotalOxidizerCapacity =
                    packet.TotalOxidizerCapacity,

                TotalMonopropellantAmount =
                    packet.TotalMonopropellantAmount,

                TotalMonopropellantCapacity =
                    packet.TotalMonopropellantCapacity
            };
        }

        private void OnConnectionTimerTick(
            object sender,
            EventArgs e)
        {
            DateTime lastReceivedUtc =
                _telemetryBuffer.LastReceivedUtc;

            bool online =
                lastReceivedUtc !=
                    default(DateTime) &&
                DateTime.UtcNow -
                    lastReceivedUtc <
                TimeSpan.FromSeconds(
                    2.0);

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
                TimeSpan.FromSeconds(
                    5.0))
            {
                return;
            }

            _lastPerformanceReportUtc =
                nowUtc;

            Debug.WriteLine(
                "[KMC PERFORMANCE] "
                + "received="
                + _telemetryBuffer
                    .ReceivedPacketCount
                + " displayed="
                + _displayedPacketCount
                + " superseded="
                + _telemetryBuffer
                    .SupersededPacketCount
                + " refresh="
                + _displayRefreshSlider
                    .Value
                + "fps");
        }

        private void OnFormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            _displayRefreshTimer.Stop();
            _connectionTimer.Stop();

            _receiver.TelemetryReceived -=
                OnTelemetryReceived;

            _receiver.Dispose();
        }
    }
}
