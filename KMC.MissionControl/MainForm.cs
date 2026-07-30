using KMC.MissionControl.Controls;
using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Themes;
using KMC.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;
using FormsTimer = System.Windows.Forms.Timer;
using Label = System.Windows.Forms.Label;

namespace KMC.MissionControl
{
    public sealed class MainForm : Form
    {
        private const int OuterMargin = 24;
        private const int HeaderHeight = 58;
        private const int SectionSpacing = 16;
        private const int NavigationHeight = 44;
        private const int NormalSummaryHeight = 250;
        private const int CompactSummaryHeight = 190;

        private const int CompactHeightBreakpoint = 820;
        private const int HideSummaryHeightBreakpoint = 700;

        private readonly TableLayoutPanel _rootLayout;

        private readonly MissionControlReceiver _receiver;
        private readonly FormsTimer _connectionTimer;

        private readonly Label _connectionLabel;
        private readonly ConsolePanel _displayPanel;
        private readonly MissionDisplay _missionDisplay;
        private readonly NavigationBar _navigationBar;
        private readonly MissionSummary _missionSummary;

        private DateTime _lastPacketUtc;

        public MainForm()
        {
            Text = "KMC - Kerbal Mission Control";

            ClientSize = new Size(
                1180,
                900);

            MinimumSize = new Size(
                760,
                600);

            StartPosition =
                FormStartPosition.CenterScreen;

            BackColor =
                ApolloTheme.WindowBackground;

            ForeColor =
                Color.FromArgb(
                    190,
                    255,
                    190);

            Font = new Font(
                "Consolas",
                12.0f,
                FontStyle.Regular);

            AutoScaleMode =
                AutoScaleMode.Dpi;

            _connectionLabel =
                CreateConnectionLabel();

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
                    PhosphorMode =
                        CrtPhosphorMode.Blue,
                    ShowScanLines = true,
                    ShowScalingDiagnostics = false,
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    MinimumSize = new Size(
                        320,
                        180)
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

            _missionDisplay.SetPage(
                new AscentPage());

            _missionDisplay.UpdateTelemetry(
                initialTelemetry);

            /*
             * Docking order matters in WinForms.
             *
             * Add the fill control first and the top control second.
             * The navigation bar then occupies the top of the panel,
             * while the mission display fills the remaining space.
             */
            _displayPanel.Controls.Add(
                _missionDisplay);

            _displayPanel.Controls.Add(
                _navigationBar);

            _missionSummary =
                new MissionSummary
                {
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    MinimumSize = new Size(
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

            _receiver =
                new MissionControlReceiver();

            _receiver.TelemetryReceived +=
                OnTelemetryReceived;

            _connectionTimer =
                new FormsTimer
                {
                    Interval = 500
                };

            _connectionTimer.Tick +=
                OnConnectionTimerTick;

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
                    Dock = DockStyle.Fill,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    Padding = new Padding(
                        OuterMargin),
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
                    NormalSummaryHeight));

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
             * Row 2 is intentionally empty.
             * It acts as the space between the console display
             * and the mission summary.
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
                    Dock = DockStyle.Fill,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    ColumnCount = 2,
                    RowCount = 1
                };

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

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
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    ForeColor =
                        Color.FromArgb(
                            190,
                            255,
                            190),
                    Font = new Font(
                        "Consolas",
                        20.0f,
                        FontStyle.Bold),
                    AutoEllipsis = true
                };

            headerLayout.Controls.Add(
                titleLabel,
                0,
                0);

            headerLayout.Controls.Add(
                _connectionLabel,
                1,
                0);

            return headerLayout;
        }

        private static Label CreateConnectionLabel()
        {
            return new Label
            {
                Text = "LINK OFFLINE",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TextAlign =
                    ContentAlignment.MiddleRight,
                ForeColor =
                    Color.OrangeRed,
                Font = new Font(
                    "Consolas",
                    12.0f,
                    FontStyle.Bold)
            };
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

                /*
                 * Remove the gap above the summary when the
                 * summary is hidden.
                 */
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

        private void OnTelemetryReceived(
            TelemetryPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<TelemetryPacket>(
                        OnTelemetryReceived),
                    packet);

                return;
            }

            _lastPacketUtc =
                DateTime.UtcNow;

            MissionTelemetry telemetry =
                CreateMissionTelemetry(
                    packet);

            _missionDisplay.UpdateTelemetry(
                telemetry);

            _missionSummary.UpdateTelemetry(
                telemetry);
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
            bool online =
                _lastPacketUtc !=
                    default(DateTime) &&
                DateTime.UtcNow -
                    _lastPacketUtc <
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

        private void OnFormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            _connectionTimer.Stop();

            _receiver.Dispose();
        }
    }
}