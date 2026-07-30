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
        private readonly MissionControlReceiver _receiver;
        private readonly FormsTimer _connectionTimer;
        private readonly Label _connectionLabel;
        private readonly ConsolePanel _testPanel;
        private readonly MissionDisplay _ascentCrt;
        private readonly NavigationBar _navigationBar;
        private readonly MissionSummary _missionSummary;

        private DateTime _lastPacketUtc;

        public MainForm()
        {
            Text = "KMC - Kerbal Mission Control";

            Width = 760;
            Height = 960;
            MinimumSize = new Size(700, 760);

            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = true;

            BackColor = ApolloTheme.WindowBackground;
            ForeColor = Color.FromArgb(190, 255, 190);

            Font = new Font(
                "Consolas",
                12f,
                FontStyle.Regular);

            Label titleLabel = new Label
            {
                Text = "KERBAL MISSION CONTROL",
                Left = 24,
                Top = 18,
                Width = 500,
                Height = 36,
                Font = new Font(
                    "Consolas",
                    20f,
                    FontStyle.Bold)
            };

            _connectionLabel = new Label
            {
                Text = "LINK OFFLINE",
                Left = 545,
                Top = 25,
                Width = 170,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.OrangeRed,
                Font = new Font(
                    "Consolas",
                    12f,
                    FontStyle.Bold)
            };

            Controls.Add(titleLabel);
            Controls.Add(_connectionLabel);

            _testPanel = new ConsolePanel
            {
                PanelTitle = "ASCENT DISPLAY",
                Left = 30,
                Top = 80,
                Width = 680,
                Height = 500
            };

            _ascentCrt = new MissionDisplay
            {
                ScreenTitle = "ASCENT DATA",
                PhosphorMode = CrtPhosphorMode.Blue,
                ShowScanLines = true,
                Left = 30,
                Top = 94,
                Width = 620,
                Height = 378
            };

            _navigationBar = new NavigationBar
            {
                Left = 30,
                Top = 42,
                Width = 620,
                Height = 44
            };

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
                (page, title) =>
                {
                    _ascentCrt.SetPage(page);
                    _ascentCrt.ScreenTitle = title + " DATA";
                    _testPanel.PanelTitle = title + " DISPLAY";
                };

            MissionTelemetry initialTelemetry =
                new MissionTelemetry();

            _ascentCrt.SetPage(
                new AscentPage());

            _ascentCrt.UpdateTelemetry(
                initialTelemetry);

            _testPanel.Controls.Add(
                _navigationBar);

            _testPanel.Controls.Add(
                _ascentCrt);

            Controls.Add(
                _testPanel);

            _missionSummary = new MissionSummary
            {
                Left = 30,
                Top = 600,
                Width = 680,
                Height = 270
            };

            _missionSummary.UpdateTelemetry(
                initialTelemetry);

            Controls.Add(
                _missionSummary);

            _receiver = new MissionControlReceiver();
            _receiver.TelemetryReceived += OnTelemetryReceived;

            _connectionTimer = new FormsTimer
            {
                Interval = 500
            };

            _connectionTimer.Tick += OnConnectionTimerTick;

            Load += OnFormLoad;
            FormClosing += OnFormClosing;
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
                    "Mission Control could not start the telemetry receiver.\n\n"
                    + ex.Message,
                    "KMC Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnTelemetryReceived(
            TelemetryPacket packet)
        {
            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<TelemetryPacket>(
                        OnTelemetryReceived),
                    packet);

                return;
            }

            _lastPacketUtc = DateTime.UtcNow;

            MissionTelemetry telemetry =
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

                    DynamicPressureKpa =
                        packet.DynamicPressureKpa,

                    StaticPressureKpa =
                        packet.StaticPressureKpa,

                    Mach = packet.Mach,

                    VesselMass = packet.VesselMass,
                    CurrentThrust = packet.CurrentThrust,
                    MaximumThrust = packet.MaximumThrust,

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

            _ascentCrt.UpdateTelemetry(
                telemetry);

            _missionSummary.UpdateTelemetry(
                telemetry);
        }

        private void OnConnectionTimerTick(
            object sender,
            EventArgs e)
        {
            bool online =
                _lastPacketUtc != default(DateTime) &&
                DateTime.UtcNow - _lastPacketUtc
                    < TimeSpan.FromSeconds(2);

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