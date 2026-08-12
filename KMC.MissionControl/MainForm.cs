using KMC.Engine.Analysis;
using KMC.Engine.Guidance;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Controls;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.MissionControl.Telemetry;
using KMC.MissionControl.Transport;
using KMC.MissionControl.Themes;
using KMC.Shared;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        private readonly OrbitNormalTelemetryReceiver _orbitNormalReceiver;
        private readonly RadialTelemetryReceiver _radialReceiver;
        private readonly LatestTelemetryBuffer _telemetryBuffer;
        private readonly FormsTimer _connectionTimer;
        private readonly FormsTimer _displayRefreshTimer;
        private readonly FormsTimer _performanceOverlayTimer;
        private readonly Label _connectionLabel;
        private readonly Label _displayRefreshLabel;
        private readonly ComboBox _maneuverTypeSelector;
        private readonly NumericUpDown _maneuverTargetKm;
        private readonly NumericUpDown _maneuverNodeDelaySeconds;
        private readonly Label _maneuverValueHeader;
        private readonly Label _maneuverNodeDelayHeader;
        private readonly Button _maneuverComputeButton;
        private readonly Button _maneuverUploadButton;
        private readonly ComboBox _maneuverNodeSelector;
        private readonly Button _maneuverActivatePlanButton;
        private readonly Button _maneuverDeleteButton;
        private readonly TableLayoutPanel _maneuverNodeActions;
        private readonly ManeuverQueueTransport _maneuverQueueTransport;
        private readonly ElectricalControlReceiver _electricalControlReceiver;
        private readonly FailureEffectAckReceiver _failureEffectAckReceiver;
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
            _maneuverTypeSelector = CreateManeuverTypeSelector();
            _maneuverTargetKm = CreateManeuverTargetControl();
            _maneuverNodeDelaySeconds = CreateManeuverNodeDelayControl();
            _maneuverValueHeader = CreateManeuverFieldHeader("TARGET ALT (KM)");
            _maneuverNodeDelayHeader = CreateManeuverFieldHeader("NODE T+ (SEC)");
            _maneuverComputeButton = CreateManeuverComputeButton();
            _maneuverUploadButton = CreateManeuverUploadButton();
            _maneuverNodeSelector = CreateManeuverNodeSelector();
            _maneuverActivatePlanButton = CreateManeuverActivatePlanButton();
            _maneuverDeleteButton = CreateManeuverDeleteButton();
            _maneuverNodeActions = CreateManeuverNodeActions();
            _displayRefreshSlider = CreateDisplayRefreshSlider();

            UpdateManeuverTargetControlState();

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

            _maneuverQueueTransport =
                new ManeuverQueueTransport();

            _electricalControlReceiver =
                new ElectricalControlReceiver();

            _failureEffectAckReceiver =
                new FailureEffectAckReceiver();

            _maneuverQueueTransport.InventoryReceived +=
                OnManeuverInventoryReceived;

            _orbitNormalReceiver =
                new OrbitNormalTelemetryReceiver();

            _radialReceiver =
                new RadialTelemetryReceiver();

            _orbitNormalReceiver.SampleReceived +=
                OnOrbitNormalTelemetryReceived;

            _radialReceiver.SampleReceived +=
                OnRadialTelemetryReceived;

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
                    ColumnCount = 9,
                    RowCount = 2
                };

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    205.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    125.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    110.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    105.0f));

            headerLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    135.0f));

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
                    SizeType.Absolute,
                    17.0f));

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
            headerLayout.SetRowSpan(titleLabel, 2);

            headerLayout.Controls.Add(_maneuverTypeSelector, 1, 0);
            headerLayout.SetRowSpan(_maneuverTypeSelector, 2);

            headerLayout.Controls.Add(_maneuverValueHeader, 2, 0);
            headerLayout.Controls.Add(_maneuverTargetKm, 2, 1);
            headerLayout.Controls.Add(_maneuverNodeDelayHeader, 3, 0);
            headerLayout.Controls.Add(_maneuverNodeDelaySeconds, 3, 1);

            headerLayout.Controls.Add(_maneuverComputeButton, 4, 0);
            headerLayout.SetRowSpan(_maneuverComputeButton, 2);
            headerLayout.Controls.Add(_maneuverUploadButton, 5, 0);
            headerLayout.SetRowSpan(_maneuverUploadButton, 2);
            headerLayout.Controls.Add(_displayRefreshLabel, 6, 0);
            headerLayout.SetRowSpan(_displayRefreshLabel, 2);
            headerLayout.Controls.Add(_displayRefreshSlider, 7, 0);
            headerLayout.SetRowSpan(_displayRefreshSlider, 2);

            // Build 13.6 reuses the display-refresh cells on MNV only.
            headerLayout.Controls.Add(_maneuverNodeSelector, 6, 0);
            headerLayout.SetRowSpan(_maneuverNodeSelector, 2);
            headerLayout.Controls.Add(_maneuverNodeActions, 7, 0);
            headerLayout.SetRowSpan(_maneuverNodeActions, 2);

            headerLayout.Controls.Add(_connectionLabel, 8, 0);
            headerLayout.SetRowSpan(_connectionLabel, 2);

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

        private static Label CreateManeuverFieldHeader(string text)
        {
            return
                new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 0, 6, 0),
                    TextAlign = ContentAlignment.BottomLeft,
                    ForeColor = Color.FromArgb(150, 220, 255),
                    Font = new Font("Consolas", 7.5f, FontStyle.Bold),
                    Visible = false,
                    AutoEllipsis = true
                };
        }

        private ComboBox CreateManeuverTypeSelector()
        {
            ComboBox selector =
                new ComboBox
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 1, 6, 3),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(35, 45, 40),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                    Visible = false,
                    TabStop = false
                };

            selector.Items.Add("CIRCULARIZE AP");
            selector.Items.Add("SET PE @ AP");
            selector.Items.Add("SET AP @ PE");
            selector.Items.Add("MANUAL PRO/RETRO");
            selector.Items.Add("MANUAL NORM/ANTI");
            selector.Items.Add("MANUAL RADIAL IN/OUT");
            selector.SelectedIndex = 0;

            selector.SelectedIndexChanged +=
                OnManeuverTypeChanged;

            return selector;
        }

        private static NumericUpDown CreateManeuverTargetControl()
        {
            return
                new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 1, 6, 3),
                    DecimalPlaces = 1,
                    Increment = 5.0M,
                    Minimum = 0.0M,
                    Maximum = 100000.0M,
                    Value = 100.0M,
                    ThousandsSeparator = true,
                    BackColor = Color.FromArgb(35, 45, 40),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                    Visible = false,
                    TabStop = false
                };
        }

        private static NumericUpDown CreateManeuverNodeDelayControl()
        {
            return
                new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 1, 6, 3),
                    DecimalPlaces = 0,
                    Increment = 10.0M,
                    Minimum = 10.0M,
                    Maximum = 86400.0M,
                    Value = 300.0M,
                    ThousandsSeparator = true,
                    BackColor = Color.FromArgb(35, 45, 40),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 8.0f, FontStyle.Bold),
                    Visible = false,
                    Enabled = false,
                    TabStop = false
                };
        }

        private Button CreateManeuverComputeButton()
        {
            Button button =
                new Button
                {
                    Text = "COMPUTE",
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
                OnManeuverComputeClick;

            return button;
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

        private ComboBox CreateManeuverNodeSelector()
        {
            ComboBox selector =
                new ComboBox
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 7, 6, 7),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(35, 45, 40),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 8.0f, FontStyle.Bold),
                    Visible = false,
                    TabStop = false
                };

            selector.SelectedIndexChanged +=
                OnManeuverNodeSelectionChanged;

            return selector;
        }

        private Button CreateManeuverActivatePlanButton()
        {
            Button button =
                new Button
                {
                    Text = "ACTIVATE",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2, 8, 2, 8),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(43, 56, 48),
                    ForeColor = Color.FromArgb(190, 255, 190),
                    Font = new Font("Consolas", 8.0f, FontStyle.Bold),
                    Visible = false,
                    Enabled = false,
                    TabStop = false
                };

            button.FlatAppearance.BorderColor =
                Color.FromArgb(100, 170, 115);

            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(55, 72, 60);

            button.Click +=
                OnManeuverActivatePlanClick;

            return button;
        }

        private TableLayoutPanel CreateManeuverNodeActions()
        {
            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    ColumnCount = 2,
                    RowCount = 1,
                    Visible = false,
                    BackColor = ApolloTheme.WindowBackground
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50.0f));

            panel.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100.0f));

            panel.Controls.Add(
                _maneuverActivatePlanButton,
                0,
                0);

            panel.Controls.Add(
                _maneuverDeleteButton,
                1,
                0);

            return panel;
        }

        private Button CreateManeuverDeleteButton()
        {
            Button button =
                new Button
                {
                    Text = "DELETE",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 8, 6, 8),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(62, 43, 43),
                    ForeColor = Color.FromArgb(255, 190, 170),
                    Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                    Visible = false,
                    Enabled = false,
                    TabStop = false
                };

            button.FlatAppearance.BorderColor =
                Color.FromArgb(170, 105, 90);

            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(82, 53, 50);

            button.Click +=
                OnManeuverDeleteNodeClick;

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
                new GuidancePage());

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

            bool maneuverPage =
                string.Equals(
                    title,
                    "MNV",
                    StringComparison.OrdinalIgnoreCase);

            _maneuverTypeSelector.Visible =
                maneuverPage;

            _maneuverTargetKm.Visible =
                maneuverPage;

            _maneuverNodeDelaySeconds.Visible =
                maneuverPage;

            _maneuverValueHeader.Visible =
                maneuverPage;

            _maneuverNodeDelayHeader.Visible =
                maneuverPage;

            _maneuverComputeButton.Visible =
                maneuverPage;

            _maneuverUploadButton.Visible =
                maneuverPage;

            _maneuverNodeSelector.Visible =
                maneuverPage;

            _maneuverNodeActions.Visible =
                maneuverPage;

            _maneuverActivatePlanButton.Visible =
                maneuverPage;

            _maneuverDeleteButton.Visible =
                maneuverPage;

            // The two cells are shared with the normal display-rate controls.
            _displayRefreshLabel.Visible =
                !maneuverPage;

            _displayRefreshSlider.Visible =
                !maneuverPage;

            if (maneuverPage)
            {
                UpdateManeuverTargetControlState();
            }
        }

        private void OnManeuverTypeChanged(
            object sender,
            EventArgs e)
        {
            UpdateManeuverTargetControlState();
        }

        private void UpdateManeuverTargetControlState()
        {
            if (_maneuverTargetKm == null ||
                _maneuverTypeSelector == null)
            {
                return;
            }

            bool apsisTargetRequired =
                _maneuverTypeSelector.SelectedIndex == 1 ||
                _maneuverTypeSelector.SelectedIndex == 2;

            bool manual =
                _maneuverTypeSelector.SelectedIndex == 3 ||
                _maneuverTypeSelector.SelectedIndex == 4 ||
                _maneuverTypeSelector.SelectedIndex == 5;

            _maneuverValueHeader.Text =
                manual
                    ? "DELTA-V (M/S)"
                    : "TARGET ALT (KM)";

            _maneuverTargetKm.Enabled =
                apsisTargetRequired ||
                manual;

            _maneuverNodeDelaySeconds.Enabled =
                manual;

            if (manual)
            {
                _maneuverTargetKm.Minimum =
                    -5000.0M;

                _maneuverTargetKm.Maximum =
                    5000.0M;

                _maneuverTargetKm.Increment =
                    1.0M;

                if (_maneuverTargetKm.Value == 0.0M ||
                    Math.Abs(
                        _maneuverTargetKm.Value) > 5000.0M)
                {
                    _maneuverTargetKm.Value =
                        10.0M;
                }
            }
            else
            {
                _maneuverTargetKm.Minimum =
                    0.0M;

                _maneuverTargetKm.Maximum =
                    100000.0M;

                _maneuverTargetKm.Increment =
                    5.0M;

                if (_maneuverTargetKm.Value < 0.0M)
                {
                    _maneuverTargetKm.Value =
                        100.0M;
                }
            }
        }

        private void OnManeuverComputeClick(
            object sender,
            EventArgs e)
        {
            /*
             * Build 13.8:
             * COMPUTE advances the Engine's single active planning identity.
             * Preserve the currently reviewed plan first so Mission Control
             * retains a multi-plan planning history even after Engine moves
             * on to the next request.
             */
            CaptureCurrentKmcPlan();

            KmcManeuverPlanStore.ClearSelection();
            ManeuverPlanPromotionStore.CancelPending();

            ManeuverRequestType type;

            switch (_maneuverTypeSelector.SelectedIndex)
            {
                case 1:
                    type =
                        ManeuverRequestType.SetPeriapsisAtApoapsis;
                    break;

                case 2:
                    type =
                        ManeuverRequestType.SetApoapsisAtPeriapsis;
                    break;

                case 3:
                    type =
                        ManeuverRequestType.ManualProgradeRetrograde;
                    break;

                case 4:
                    type =
                        ManeuverRequestType.ManualNormalAntiNormal;
                    break;

                case 5:
                    type =
                        ManeuverRequestType.ManualRadialInOut;
                    break;

                default:
                    type =
                        ManeuverRequestType.CircularizeAtApoapsis;
                    break;
            }

            ManeuverRequestStore.Set(
                new ManeuverRequestModel
                {
                    Type = type,
                    TargetAltitudeMeters =
                        type ==
                        ManeuverRequestType.SetPeriapsisAtApoapsis ||
                        type ==
                        ManeuverRequestType.SetApoapsisAtPeriapsis
                            ? (double)_maneuverTargetKm.Value *
                              1000.0
                            : double.NaN,

                    ManualProgradeDeltaVMetersPerSecond =
                        type ==
                        ManeuverRequestType.ManualProgradeRetrograde
                            ? (double)_maneuverTargetKm.Value
                            : double.NaN,

                    ManualNormalDeltaVMetersPerSecond =
                        type ==
                        ManeuverRequestType.ManualNormalAntiNormal
                            ? (double)_maneuverTargetKm.Value
                            : double.NaN,

                    ManualRadialDeltaVMetersPerSecond =
                        type ==
                        ManeuverRequestType.ManualRadialInOut
                            ? (double)_maneuverTargetKm.Value
                            : double.NaN,

                    NodeDelaySeconds =
                        type ==
                            ManeuverRequestType.ManualProgradeRetrograde ||
                        type ==
                            ManeuverRequestType.ManualNormalAntiNormal ||
                        type ==
                            ManeuverRequestType.ManualRadialInOut
                            ? (double)_maneuverNodeDelaySeconds.Value
                            : double.NaN,

                    RequestedUtc =
                        DateTime.UtcNow
                });

            _missionDisplay.RequestRender();
        }

        private void OnManeuverUploadClick(
            object sender,
            EventArgs e)
        {
            if (_receiver == null)
            {
                return;
            }

            /*
             * Build 13.8:
             * Any reviewed plan that is sent to KSP becomes part of the
             * Mission Control retained KMC plan set.
             */
            CaptureCurrentKmcPlan();

            string resultText;

            _receiver.UploadLatestManeuver(
                out resultText);

            _missionDisplay.RequestRender();
        }

        private static void CaptureCurrentKmcPlan()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.ManeuverPlan == null)
            {
                return;
            }

            ManeuverPlanModel plan =
                result.Snapshot.ManeuverPlan;

            if (string.IsNullOrWhiteSpace(
                    plan.PlanId) ||
                string.IsNullOrWhiteSpace(
                    plan.VesselId) ||
                !plan.NodeUniversalTimeAvailable ||
                double.IsNaN(
                    plan.NodeUniversalTimeSeconds) ||
                double.IsInfinity(
                    plan.NodeUniversalTimeSeconds))
            {
                return;
            }

            KmcManeuverPlanStore.Capture(
                plan);
        }

        private void OnManeuverInventoryReceived(
            ManeuverInventorySnapshot snapshot)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<ManeuverInventorySnapshot>(
                        OnManeuverInventoryReceived),
                    snapshot);

                return;
            }

            string selectedNodeId = string.Empty;

            ManeuverNodeSelectorItem selected =
                _maneuverNodeSelector.SelectedItem as
                ManeuverNodeSelectorItem;

            if (selected != null)
            {
                selectedNodeId =
                    selected.NodeId;
            }

            _maneuverNodeSelector.BeginUpdate();

            try
            {
                _maneuverNodeSelector.Items.Clear();

                if (snapshot != null)
                {
                    for (int index = 0;
                         index < snapshot.Nodes.Count;
                         index++)
                    {
                        ManeuverInventoryNode node =
                            snapshot.Nodes[index];

                        ManeuverNodeSelectorItem item =
                            new ManeuverNodeSelectorItem(
                                node,
                                snapshot.UniversalTimeSeconds,
                                index + 1);

                        int itemIndex =
                            _maneuverNodeSelector.Items.Add(
                                item);

                        if (!string.IsNullOrWhiteSpace(
                                selectedNodeId) &&
                            string.Equals(
                                selectedNodeId,
                                node.NodeId,
                                StringComparison.Ordinal))
                        {
                            _maneuverNodeSelector.SelectedIndex =
                                itemIndex;
                        }
                    }
                }

                if (_maneuverNodeSelector.SelectedIndex < 0 &&
                    _maneuverNodeSelector.Items.Count > 0)
                {
                    _maneuverNodeSelector.SelectedIndex = 0;
                }
            }
            finally
            {
                _maneuverNodeSelector.EndUpdate();
            }

            RefreshKmcManeuverLifecycle(
                snapshot);

            UpdateManeuverNodeActionState();

            _missionDisplay.RequestRender();
        }

        private void OnManeuverNodeSelectionChanged(
            object sender,
            EventArgs e)
        {
            UpdateManeuverNodeActionState();

            if (_missionDisplay != null)
            {
                _missionDisplay.RequestRender();
            }
        }

        private void UpdateManeuverNodeActionState()
        {
            ManeuverNodeSelectorItem selected =
                _maneuverNodeSelector != null
                    ? _maneuverNodeSelector.SelectedItem as
                      ManeuverNodeSelectorItem
                    : null;

            _maneuverDeleteButton.Enabled =
                selected != null;

            KmcQueuedManeuverPlan plan =
                ResolveSelectedRetainedPlan(
                    selected);

            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

            bool alreadyActive =
                plan != null &&
                string.Equals(
                    plan.PlanId,
                    activePlanId,
                    StringComparison.Ordinal);

            bool promotionAllowed =
                plan != null &&
                !alreadyActive &&
                CanPromoteManeuverNow();

            _maneuverActivatePlanButton.Enabled =
                promotionAllowed;

            bool nextKmcPlanAvailable =
                plan != null &&
                !alreadyActive &&
                IsNextKmcPlanAvailable(
                    plan);

            _maneuverActivatePlanButton.Text =
                alreadyActive
                    ? "ACTIVE"
                    : plan != null
                        ? nextKmcPlanAvailable
                            ? "ACTIVATE NEXT"
                            : "ACTIVATE"
                        : "MANUAL";
        }

        /*
         * Build 13.10:
         * Queue progression remains crew-controlled. When the previously
         * active KMC node has been confirmed NODE REMOVED, identify the first
         * remaining retained KMC node as the next plan available for explicit
         * crew activation. This never promotes or uploads a plan by itself.
         */
        private static bool IsNextKmcPlanAvailable(
            KmcQueuedManeuverPlan candidate)
        {
            if (candidate == null ||
                string.IsNullOrWhiteSpace(
                    candidate.PlanId))
            {
                return false;
            }

            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

            if (string.IsNullOrWhiteSpace(
                    activePlanId) ||
                string.Equals(
                    activePlanId,
                    candidate.PlanId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            ManeuverUplinkStatusSnapshot activeStatus =
                ManeuverUplinkStatusStore.GetForPlan(
                    activePlanId);

            if (activeStatus == null ||
                !string.Equals(
                    activeStatus.State,
                    "NODE REMOVED",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ManeuverInventorySnapshot inventory =
                ManeuverInventoryStore.GetLatest();

            if (inventory == null ||
                inventory.Nodes == null ||
                string.IsNullOrWhiteSpace(
                    inventory.VesselId))
            {
                return false;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                KmcQueuedManeuverPlan retained =
                    KmcManeuverPlanStore.FindForNode(
                        inventory.Nodes[index],
                        inventory.VesselId);

                if (retained == null)
                {
                    continue;
                }

                return
                    string.Equals(
                        retained.PlanId,
                        candidate.PlanId,
                        StringComparison.Ordinal);
            }

            return false;
        }

        /*
         * Build 13.11 — retained maneuver lifecycle.
         *
         * Live stock KSP nodes remain authoritative. The retained store adds
         * Mission Control operational history so completed/removed plans do
         * not disappear merely because their stock node left the inventory.
         */
        private static void RefreshKmcManeuverLifecycle(
            ManeuverInventorySnapshot inventory)
        {
            AnalysisPipelineResult result;
            ManeuverPlanModel currentPlan = null;
            GuidanceSolutionModel guidance = null;

            if (EngineeringSnapshotStore.TryGetLatest(
                    out result) &&
                result != null &&
                result.Snapshot != null)
            {
                currentPlan =
                    result.Snapshot.ManeuverPlan;

                guidance =
                    result.Snapshot.Guidance;
            }

            KmcManeuverPlanStore.RefreshLifecycle(
                inventory,
                currentPlan,
                guidance);
        }

        private static bool IsManeuverBurnComplete(
            string planId)
        {
            if (string.IsNullOrWhiteSpace(
                    planId))
            {
                return false;
            }

            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Guidance == null)
            {
                return false;
            }

            GuidanceSolutionModel guidance =
                result.Snapshot.Guidance;

            return
                guidance.BurnComplete &&
                string.Equals(
                    guidance.PlanId,
                    planId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    guidance.PostBurnResult,
                    "DV COMPLETE",
                    StringComparison.OrdinalIgnoreCase);
        }

        private bool CanPromoteManeuverNow()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Guidance == null)
            {
                return true;
            }

            GuidanceSolutionModel guidance =
                result.Snapshot.Guidance;

            if (guidance.BurnActive)
            {
                return false;
            }

            if (guidance.BurnComplete &&
                !guidance.ReacquisitionReady)
            {
                return false;
            }

            return true;
        }

        private static KmcQueuedManeuverPlan ResolveSelectedRetainedPlan(
            ManeuverNodeSelectorItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            ManeuverInventorySnapshot inventory =
                ManeuverInventoryStore.GetLatest();

            if (inventory == null ||
                inventory.Nodes == null ||
                string.IsNullOrWhiteSpace(
                    inventory.VesselId))
            {
                return null;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                ManeuverInventoryNode node =
                    inventory.Nodes[index];

                if (node != null &&
                    string.Equals(
                        node.NodeId ?? string.Empty,
                        selected.NodeId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    return
                        KmcManeuverPlanStore.FindForNode(
                            node,
                            inventory.VesselId);
                }
            }

            return null;
        }

        private void OnManeuverActivatePlanClick(
            object sender,
            EventArgs e)
        {
            ManeuverNodeSelectorItem selected =
                _maneuverNodeSelector.SelectedItem as
                ManeuverNodeSelectorItem;

            KmcQueuedManeuverPlan plan =
                ResolveSelectedRetainedPlan(
                    selected);

            if (plan == null)
            {
                return;
            }

            if (!CanPromoteManeuverNow())
            {
                MessageBox.Show(
                    "KMC cannot activate another retained plan while the current maneuver is executing or awaiting completed-node cleanup.",
                    "Maneuver Plan Interlock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            int sequence =
                KmcManeuverPlanStore.GetSequence(
                    plan.PlanId);

            DialogResult confirm =
                MessageBox.Show(
                    "Activate KMC #" +
                    Math.Max(
                        1,
                        sequence).ToString() +
                    " for maneuver review and GUID guidance?\n\n" +
                    "The existing KSP node will not be changed. " +
                    "Execution remains inhibited unless that exact PlanId/node is verified.",
                    "Activate Retained Maneuver Plan",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            KmcManeuverPlanStore.Select(
                plan.PlanId);

            ManeuverPlanPromotionStore.Publish(
                plan.CreatePromotionRequest());

            /*
             * If this PlanId already has fresh node verification in the
             * Mission Control history, republish it to the active Guidance
             * state. The Engine PlanId change still occurs on the next normal
             * analysis update, so the existing PlanId interlock remains intact.
             */
            ManeuverUplinkStatusStore.RepublishGuidanceForPlan(
                plan.PlanId);

            UpdateManeuverNodeActionState();
            _missionDisplay.RequestRender();
        }

        private void OnManeuverDeleteNodeClick(
            object sender,
            EventArgs e)
        {
            ManeuverNodeSelectorItem selected =
                _maneuverNodeSelector.SelectedItem as
                ManeuverNodeSelectorItem;

            ManeuverInventorySnapshot snapshot =
                ManeuverInventoryStore.GetLatest();

            if (selected == null ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.VesselId))
            {
                return;
            }

            DialogResult confirm =
                MessageBox.Show(
                    "Delete " + selected.Text +
                    " from the active KSP vessel?",
                    "Delete Maneuver Node",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            KmcQueuedManeuverPlan retainedPlan =
                ResolveSelectedRetainedPlan(
                    selected);

            if (retainedPlan != null)
            {
                KmcManeuverPlanStore.MarkRemovalRequested(
                    retainedPlan.PlanId,
                    IsManeuverBurnComplete(
                        retainedPlan.PlanId));
            }

            try
            {
                _maneuverQueueTransport.SendDelete(
                    snapshot.VesselId,
                    selected.NodeId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "KMC could not send the node delete command.\n\n" +
                    ex.Message,
                    "Maneuver Node Delete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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
                ManeuverRequestStore.Reset();
                KmcManeuverPlanStore.Clear();
                ManeuverPlanPromotionStore.Clear();
                OrbitNormalTelemetryStore.Clear();
                RadialTelemetryStore.Clear();
                _orbitNormalReceiver.Start();
                _radialReceiver.Start();
                _maneuverQueueTransport.Start();
                _electricalControlReceiver.Start();
                _failureEffectAckReceiver.Start();
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

        private void OnRadialTelemetryReceived(
            RadialTelemetrySample sample)
        {
            if (sample == null)
            {
                return;
            }

            RadialTelemetryStore.Publish(
                new RadialTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sample.SourceTimestampUtc,
                    ReceivedUtc = sample.ReceivedUtc,
                    VesselName = sample.VesselName ?? string.Empty,
                    RightComponent = sample.RightComponent,
                    NoseComponent = sample.NoseComponent,
                    ReferenceForwardComponent = sample.ReferenceForwardComponent
                });
        }

        private void OnOrbitNormalTelemetryReceived(
            OrbitNormalTelemetrySample sample)
        {
            if (sample == null)
            {
                return;
            }

            OrbitNormalTelemetryStore.Publish(
                new OrbitNormalTelemetryModel
                {
                    TelemetryAvailable =
                        true,

                    SourceTimestampUtc =
                        sample.SourceTimestampUtc,

                    ReceivedUtc =
                        sample.ReceivedUtc,

                    VesselName =
                        sample.VesselName ??
                        string.Empty,

                    RightComponent =
                        sample.RightComponent,

                    NoseComponent =
                        sample.NoseComponent,

                    ReferenceForwardComponent =
                        sample.ReferenceForwardComponent
                });
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

            if (e.Control &&
                e.Shift &&
                e.KeyCode == Keys.E)
            {
                string resultText;

                bool success =
                    _receiver.TogglePowerFailureTrainingLeak(
                        out resultText);

                Debug.WriteLine(
                    "KMC.MissionControl POWER FAILURE TEST" +
                    " | Success=" +
                    success +
                    " | Result=" +
                    resultText);

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

            _maneuverQueueTransport.InventoryReceived -=
                OnManeuverInventoryReceived;

            _orbitNormalReceiver.SampleReceived -=
                OnOrbitNormalTelemetryReceived;

            _radialReceiver.SampleReceived -=
                OnRadialTelemetryReceived;

            _orbitNormalReceiver.Dispose();
            _radialReceiver.Dispose();
            OrbitNormalTelemetryStore.Clear();
            RadialTelemetryStore.Clear();
            ManeuverInventoryStore.Clear();
            KmcManeuverPlanStore.Clear();
            ManeuverPlanPromotionStore.Clear();

            _maneuverQueueTransport.Dispose();
            _electricalControlReceiver.Dispose();
            _failureEffectAckReceiver.Dispose();
            ElectricalControlCommandStore.Clear();
            _receiver.Dispose();
        }


        /*
         * Build 13.8 — Multi-Maneuver Planning Foundation
         *
         * Engine intentionally remains the owner of ONE current maneuver plan.
         * Mission Control retains immutable snapshots of reviewed plans before
         * Engine advances to another request. This gives the queue persistent
         * KMC ownership for more than one stock maneuver node without changing
         * GuidanceSystem authorization or plugin protocols.
         */
        /// <summary>
        /// Build 14.4 KSP real-effect acknowledgment receiver.
        /// This is diagnostic/transport foundation only; later Build 14
        /// milestones will connect Engine failure truth to outgoing commands.
        /// </summary>
        private sealed class FailureEffectAckReceiver :
            IDisposable
        {
            private UdpClient _client;
            private Thread _thread;
            private volatile bool _running;

            public void Start()
            {
                if (_running)
                {
                    return;
                }

                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            FailureEffectPacket.AckPort));

                _running = true;

                _thread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name = "KMC Failure Effect ACK"
                    };

                _thread.Start();

                Debug.WriteLine(
                    "KMC.Transport BOUND | UDP " +
                    FailureEffectPacket.AckPort.ToString());
            }

            private void ReceiveLoop()
            {
                while (_running)
                {
                    try
                    {
                        IPEndPoint sender =
                            new IPEndPoint(
                                IPAddress.Any,
                                0);

                        byte[] data =
                            _client.Receive(
                                ref sender);

                        string text =
                            Encoding.UTF8.GetString(
                                data);

                        FailureEffectAck ack;

                        if (!FailureEffectAck.TryParse(
                                text,
                                out ack))
                        {
                            continue;
                        }

                        Debug.WriteLine(
                            "KMC.MissionControl FAILURE EFFECT ACK" +
                            " | VesselId=" +
                            ack.VesselId +
                            " | CommandId=" +
                            ack.CommandId +
                            " | Effect=" +
                            ack.EffectType +
                            " | Status=" +
                            ack.Status +
                            " | Part=" +
                            ack.PartPersistentId.ToString() +
                            " | Observed=" +
                            FormatObserved(
                                ack.ObservedValue) +
                            " | Detail=" +
                            ack.Detail);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (SocketException)
                    {
                        if (!_running)
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "KMC.MissionControl FAILURE EFFECT ACK ERROR | " +
                            ex.GetType().Name +
                            " | " +
                            ex.Message);
                    }
                }
            }

            private static string FormatObserved(
                double value)
            {
                return
                    double.IsNaN(value) ||
                    double.IsInfinity(value)
                        ? "---"
                        : value.ToString("0.###");
            }

            public void Dispose()
            {
                _running = false;

                if (_client != null)
                {
                    _client.Close();
                    _client = null;
                }

                if (_thread != null &&
                    _thread.IsAlive)
                {
                    _thread.Join(250);
                }

                _thread = null;
            }
        }

        private sealed class ManeuverNodeSelectorItem
        {
            public ManeuverNodeSelectorItem(
                ManeuverInventoryNode node,
                double currentUt,
                int sequence)
            {
                NodeId =
                    node != null
                        ? node.NodeId ?? string.Empty
                        : string.Empty;

                double tNode =
                    node != null
                        ? node.NodeUniversalTimeSeconds - currentUt
                        : double.NaN;

                string time =
                    double.IsNaN(tNode) ||
                    double.IsInfinity(tNode)
                        ? "---"
                        : Math.Max(0.0, tNode).ToString("0") + "S";

                Text =
                    "#" + sequence.ToString() +
                    " T+" + time +
                    " " +
                    ManeuverInventoryFormatting.DescribeVector(node);
            }

            public string NodeId { get; private set; }
            public string Text { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }
    }

    /*
     * Build 13.11:
     * Operational lifecycle for retained KMC-authored maneuver plans.
     * This is history/awareness only; Engine and GUID remain authoritative
     * for maneuver safety and execution.
     */
    internal enum KmcManeuverLifecycleState
    {
        Planned = 0,
        Verified = 1,
        Active = 2,
        Complete = 3,
        Removed = 4,
        Missed = 5,
        Modified = 6
    }

    internal sealed class KmcQueuedManeuverPlan
    {
        public KmcQueuedManeuverPlan()
        {
            PlanId = string.Empty;
            VesselId = string.Empty;
            Objective = string.Empty;
            Status = "PLAN VALID";
            Evidence =
                new System.Collections.Generic.List<string>();

            NodeUniversalTimeSeconds = double.NaN;
            NodeMissionTimeSeconds = double.NaN;
            TimeToNodeSeconds = double.NaN;
            ProgradeDeltaVMetersPerSecond = double.NaN;
            NormalDeltaVMetersPerSecond = double.NaN;
            RadialDeltaVMetersPerSecond = double.NaN;
            TotalDeltaVMetersPerSecond = double.NaN;
            EstimatedBurnDurationSeconds = double.NaN;
            IgnitionLeadSeconds = double.NaN;
            IgnitionMissionTimeSeconds = double.NaN;
            PredictedApoapsisMeters = double.NaN;
            PredictedPeriapsisMeters = double.NaN;
            PredictedInclinationDegrees = double.NaN;
            PredictedEccentricity = double.NaN;
            PredictedPeriodSeconds = double.NaN;

            LifecycleState =
                KmcManeuverLifecycleState.Planned;

            LifecycleUpdatedUtc =
                DateTime.UtcNow;

            TerminalUtc =
                DateTime.MinValue;

            DeliveredDeltaVMetersPerSecond =
                double.NaN;

            BurnProgressPercent =
                double.NaN;

            ActualNodeDeltaVMetersPerSecond =
                double.NaN;

            PostBurnResult =
                string.Empty;

            ResultCapturedUtc =
                DateTime.MinValue;
        }

        public bool Available { get; set; }
        public bool OrbitTargetVerificationRequired { get; set; }
        public string PlanId { get; set; }
        public string VesselId { get; set; }
        public string Objective { get; set; }
        public bool NodeUniversalTimeAvailable { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double NodeMissionTimeSeconds { get; set; }
        public double TimeToNodeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public double TotalDeltaVMetersPerSecond { get; set; }
        public double EstimatedBurnDurationSeconds { get; set; }
        public double IgnitionLeadSeconds { get; set; }
        public double IgnitionMissionTimeSeconds { get; set; }
        public double PredictedApoapsisMeters { get; set; }
        public double PredictedPeriapsisMeters { get; set; }
        public double PredictedInclinationDegrees { get; set; }
        public double PredictedEccentricity { get; set; }
        public double PredictedPeriodSeconds { get; set; }
        public string Status { get; set; }
        public DateTime CapturedUtc { get; set; }
        public System.Collections.Generic.List<string> Evidence { get; private set; }

        public KmcManeuverLifecycleState LifecycleState { get; set; }
        public DateTime LifecycleUpdatedUtc { get; set; }
        public DateTime TerminalUtc { get; set; }
        public bool RemovalRequested { get; set; }
        public bool RemovalRequestedAfterBurnComplete { get; set; }

        /*
         * Build 13.12 — immutable-ish flight result snapshot.
         * These values are captured while GUID still owns this exact PlanId
         * so the flight-plan log can retain actual burn performance after
         * Mission Control moves on to another maneuver.
         */
        public double DeliveredDeltaVMetersPerSecond { get; set; }
        public double BurnProgressPercent { get; set; }
        public double ActualNodeDeltaVMetersPerSecond { get; set; }
        public string PostBurnResult { get; set; }
        public DateTime ResultCapturedUtc { get; set; }

        public ManeuverPlanPromotionRequest CreatePromotionRequest()
        {
            ManeuverPlanPromotionRequest request =
                new ManeuverPlanPromotionRequest
                {
                    Available = Available,
                    OrbitTargetVerificationRequired =
                        OrbitTargetVerificationRequired,
                    PlanId = PlanId ?? string.Empty,
                    VesselId = VesselId ?? string.Empty,
                    Objective = Objective ?? string.Empty,
                    NodeUniversalTimeAvailable =
                        NodeUniversalTimeAvailable,
                    NodeUniversalTimeSeconds =
                        NodeUniversalTimeSeconds,
                    NodeMissionTimeSeconds =
                        NodeMissionTimeSeconds,
                    TimeToNodeSeconds =
                        TimeToNodeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        RadialDeltaVMetersPerSecond,
                    TotalDeltaVMetersPerSecond =
                        TotalDeltaVMetersPerSecond,
                    EstimatedBurnDurationSeconds =
                        EstimatedBurnDurationSeconds,
                    IgnitionLeadSeconds =
                        IgnitionLeadSeconds,
                    IgnitionMissionTimeSeconds =
                        IgnitionMissionTimeSeconds,
                    PredictedApoapsisMeters =
                        PredictedApoapsisMeters,
                    PredictedPeriapsisMeters =
                        PredictedPeriapsisMeters,
                    PredictedInclinationDegrees =
                        PredictedInclinationDegrees,
                    PredictedEccentricity =
                        PredictedEccentricity,
                    PredictedPeriodSeconds =
                        PredictedPeriodSeconds,
                    Status = Status ?? string.Empty
                };

            for (int index = 0;
                 index < Evidence.Count;
                 index++)
            {
                request.Evidence.Add(
                    Evidence[index]);
            }

            return request;
        }
    }

    internal static class KmcManeuverPlanStore
    {
        private const double NodeUtToleranceSeconds =
            0.25;

        private const double DeltaVToleranceMetersPerSecond =
            0.05;

        private static readonly object SyncRoot =
            new object();

        private static readonly
            System.Collections.Generic.List<KmcQueuedManeuverPlan>
            Plans =
                new System.Collections.Generic.List<KmcQueuedManeuverPlan>();

        private static string _selectedPlanId =
            string.Empty;

        public static void Capture(
            ManeuverPlanModel plan)
        {
            if (plan == null ||
                string.IsNullOrWhiteSpace(
                    plan.PlanId) ||
                string.IsNullOrWhiteSpace(
                    plan.VesselId) ||
                !plan.NodeUniversalTimeAvailable ||
                double.IsNaN(
                    plan.NodeUniversalTimeSeconds) ||
                double.IsInfinity(
                    plan.NodeUniversalTimeSeconds))
            {
                return;
            }

            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    if (string.Equals(
                            Plans[index].PlanId,
                            plan.PlanId,
                            StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                KmcQueuedManeuverPlan captured =
                    new KmcQueuedManeuverPlan
                    {
                        Available = plan.Available,
                        OrbitTargetVerificationRequired =
                            plan.OrbitTargetVerificationRequired,
                        PlanId = plan.PlanId ?? string.Empty,
                        VesselId = plan.VesselId ?? string.Empty,
                        Objective = plan.Objective ?? string.Empty,
                        NodeUniversalTimeAvailable =
                            plan.NodeUniversalTimeAvailable,
                        NodeUniversalTimeSeconds =
                            plan.NodeUniversalTimeSeconds,
                        NodeMissionTimeSeconds =
                            plan.NodeMissionTimeSeconds,
                        TimeToNodeSeconds =
                            plan.TimeToNodeSeconds,
                        ProgradeDeltaVMetersPerSecond =
                            plan.ProgradeDeltaVMetersPerSecond,
                        NormalDeltaVMetersPerSecond =
                            plan.NormalDeltaVMetersPerSecond,
                        RadialDeltaVMetersPerSecond =
                            plan.RadialDeltaVMetersPerSecond,
                        TotalDeltaVMetersPerSecond =
                            plan.TotalDeltaVMetersPerSecond,
                        EstimatedBurnDurationSeconds =
                            plan.EstimatedBurnDurationSeconds,
                        IgnitionLeadSeconds =
                            plan.IgnitionLeadSeconds,
                        IgnitionMissionTimeSeconds =
                            plan.IgnitionMissionTimeSeconds,
                        PredictedApoapsisMeters =
                            plan.PredictedApoapsisMeters,
                        PredictedPeriapsisMeters =
                            plan.PredictedPeriapsisMeters,
                        PredictedInclinationDegrees =
                            plan.PredictedInclinationDegrees,
                        PredictedEccentricity =
                            plan.PredictedEccentricity,
                        PredictedPeriodSeconds =
                            plan.PredictedPeriodSeconds,
                        Status = plan.Status ?? string.Empty,
                        CapturedUtc = DateTime.UtcNow
                    };

                if (plan.Evidence != null)
                {
                    for (int index = 0;
                         index < plan.Evidence.Count;
                         index++)
                    {
                        captured.Evidence.Add(
                            plan.Evidence[index]);
                    }
                }

                Plans.Add(
                    captured);

                Plans.Sort(
                    delegate(
                        KmcQueuedManeuverPlan left,
                        KmcQueuedManeuverPlan right)
                    {
                        return
                            left.NodeUniversalTimeSeconds.CompareTo(
                                right.NodeUniversalTimeSeconds);
                    });

                if (string.IsNullOrWhiteSpace(
                        _selectedPlanId))
                {
                    _selectedPlanId =
                        plan.PlanId;
                }
            }
        }

        public static void MarkRemovalRequested(
            string planId,
            bool burnComplete)
        {
            if (string.IsNullOrWhiteSpace(
                    planId))
            {
                return;
            }

            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    KmcQueuedManeuverPlan plan =
                        Plans[index];

                    if (!string.Equals(
                            plan.PlanId,
                            planId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    plan.RemovalRequested = true;
                    plan.RemovalRequestedAfterBurnComplete =
                        burnComplete;

                    return;
                }
            }
        }

        public static void RefreshLifecycle(
            ManeuverInventorySnapshot inventory,
            ManeuverPlanModel currentPlan,
            GuidanceSolutionModel guidance)
        {
            lock (SyncRoot)
            {
                string activePlanId =
                    ManeuverPlanPromotionStore.GetActivePlanId();

                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    KmcQueuedManeuverPlan plan =
                        Plans[index];

                    if (plan == null)
                    {
                        continue;
                    }

                    /*
                     * Build 13.11.1:
                     * Exact GUID DV COMPLETE evidence outranks every previous
                     * lifecycle classification, including a stale MISSED or
                     * REMOVED terminal state. This lets completed-burn truth
                     * correct history if completion is finalized after the
                     * node has crossed/left its planned UT.
                     */
                    bool sameGuidancePlan =
                        guidance != null &&
                        string.Equals(
                            guidance.PlanId,
                            plan.PlanId,
                            StringComparison.Ordinal);

                    if (sameGuidancePlan)
                    {
                        CaptureGuidanceResult(
                            plan,
                            guidance);
                    }

                    bool burnComplete =
                        sameGuidancePlan &&
                        guidance.BurnComplete &&
                        string.Equals(
                            guidance.PostBurnResult,
                            "DV COMPLETE",
                            StringComparison.OrdinalIgnoreCase);

                    if (burnComplete)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Complete,
                            true,
                            true);

                        continue;
                    }

                    if (IsTerminal(
                            plan.LifecycleState))
                    {
                        continue;
                    }

                    ManeuverInventoryNode liveNode =
                        FindLiveNode(
                            plan,
                            inventory);

                    ManeuverUplinkStatusSnapshot uplink =
                        ManeuverUplinkStatusStore.GetForPlan(
                            plan.PlanId);

                    bool sameActivePlan =
                        !string.IsNullOrWhiteSpace(
                            activePlanId) &&
                        string.Equals(
                            activePlanId,
                            plan.PlanId,
                            StringComparison.Ordinal);

                    /*
                     * A maneuver is MISSED only when the Engine explicitly
                     * declares MANEUVER WINDOW MISSED for this exact PlanId.
                     * Do not infer MISSED from elapsed UT alone: a legitimate
                     * burn/post-burn verification can extend beyond node UT.
                     */
                    bool maneuverMissed =
                        currentPlan != null &&
                        string.Equals(
                            currentPlan.PlanId,
                            plan.PlanId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            currentPlan.Status,
                            "MANEUVER WINDOW MISSED",
                            StringComparison.OrdinalIgnoreCase);

                    if (plan.RemovalRequestedAfterBurnComplete &&
                        string.Equals(
                            uplink.State,
                            "NODE REMOVED",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Complete,
                            true,
                            true);

                        continue;
                    }

                    if (maneuverMissed)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Missed,
                            true);

                        continue;
                    }

                    bool nodeRemoved =
                        string.Equals(
                            uplink.State,
                            "NODE REMOVED",
                            StringComparison.OrdinalIgnoreCase);

                    if (nodeRemoved &&
                        liveNode == null)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Removed,
                            true);

                        continue;
                    }

                    bool crewModified =
                        string.Equals(
                            uplink.State,
                            "CREW MODIFIED",
                            StringComparison.OrdinalIgnoreCase);

                    if (crewModified)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Modified,
                            false);

                        continue;
                    }

                    if (sameActivePlan &&
                        liveNode != null)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Active,
                            false);

                        continue;
                    }

                    bool nodeVerified =
                        liveNode != null &&
                        string.Equals(
                            uplink.State,
                            "NODE VERIFIED",
                            StringComparison.OrdinalIgnoreCase);

                    if (nodeVerified)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Verified,
                            false);

                        continue;
                    }

                    if (liveNode != null)
                    {
                        SetLifecycle(
                            plan,
                            KmcManeuverLifecycleState.Planned,
                            false);
                    }
                }
            }
        }

        private static void CaptureGuidanceResult(
            KmcQueuedManeuverPlan plan,
            GuidanceSolutionModel guidance)
        {
            if (plan == null ||
                guidance == null ||
                !string.Equals(
                    guidance.PlanId,
                    plan.PlanId,
                    StringComparison.Ordinal))
            {
                return;
            }

            bool captured = false;

            if (IsFinite(
                    guidance.DeliveredDeltaVMetersPerSecond))
            {
                plan.DeliveredDeltaVMetersPerSecond =
                    guidance.DeliveredDeltaVMetersPerSecond;

                captured = true;
            }

            if (IsFinite(
                    guidance.BurnProgressPercent))
            {
                plan.BurnProgressPercent =
                    guidance.BurnProgressPercent;

                captured = true;
            }

            if (IsFinite(
                    guidance.ActualNodeDeltaVMetersPerSecond))
            {
                plan.ActualNodeDeltaVMetersPerSecond =
                    guidance.ActualNodeDeltaVMetersPerSecond;

                captured = true;
            }

            if (!string.IsNullOrWhiteSpace(
                    guidance.PostBurnResult) &&
                !string.Equals(
                    guidance.PostBurnResult,
                    "NOT AVAILABLE",
                    StringComparison.OrdinalIgnoreCase))
            {
                plan.PostBurnResult =
                    guidance.PostBurnResult.Trim();

                captured = true;
            }

            if (captured)
            {
                plan.ResultCapturedUtc =
                    DateTime.UtcNow;
            }
        }

        public static string DescribeFlightPlanOutcome(
            KmcQueuedManeuverPlan plan)
        {
            if (plan == null)
            {
                return "---";
            }

            if (plan.LifecycleState ==
                    KmcManeuverLifecycleState.Complete)
            {
                return
                    !string.IsNullOrWhiteSpace(
                        plan.PostBurnResult)
                        ? plan.PostBurnResult
                        : "COMPLETE";
            }

            return
                DescribeLifecycle(
                    plan.LifecycleState);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }

        public static string DescribeLifecycle(
            KmcManeuverLifecycleState state)
        {
            switch (state)
            {
                case KmcManeuverLifecycleState.Verified:
                    return "VERIFIED";

                case KmcManeuverLifecycleState.Active:
                    return "ACTIVE";

                case KmcManeuverLifecycleState.Complete:
                    return "COMPLETE";

                case KmcManeuverLifecycleState.Removed:
                    return "REMOVED";

                case KmcManeuverLifecycleState.Missed:
                    return "MISSED";

                case KmcManeuverLifecycleState.Modified:
                    return "MODIFIED";

                default:
                    return "PLANNED";
            }
        }

        private static ManeuverInventoryNode FindLiveNode(
            KmcQueuedManeuverPlan plan,
            ManeuverInventorySnapshot inventory)
        {
            if (plan == null ||
                inventory == null ||
                inventory.Nodes == null ||
                !string.Equals(
                    plan.VesselId ?? string.Empty,
                    inventory.VesselId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return null;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                ManeuverInventoryNode node =
                    inventory.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                if (Math.Abs(
                        node.NodeUniversalTimeSeconds -
                        plan.NodeUniversalTimeSeconds) <=
                        NodeUtToleranceSeconds &&
                    Math.Abs(
                        node.ProgradeDeltaVMetersPerSecond -
                        plan.ProgradeDeltaVMetersPerSecond) <=
                        DeltaVToleranceMetersPerSecond &&
                    Math.Abs(
                        node.NormalDeltaVMetersPerSecond -
                        plan.NormalDeltaVMetersPerSecond) <=
                        DeltaVToleranceMetersPerSecond &&
                    Math.Abs(
                        node.RadialDeltaVMetersPerSecond -
                        plan.RadialDeltaVMetersPerSecond) <=
                        DeltaVToleranceMetersPerSecond)
                {
                    return node;
                }
            }

            return null;
        }

        private static bool IsTerminal(
            KmcManeuverLifecycleState state)
        {
            return
                state == KmcManeuverLifecycleState.Complete ||
                state == KmcManeuverLifecycleState.Removed ||
                state == KmcManeuverLifecycleState.Missed;
        }

        private static void SetLifecycle(
            KmcQueuedManeuverPlan plan,
            KmcManeuverLifecycleState state,
            bool terminal,
            bool authoritativeOverride = false)
        {
            if (plan == null ||
                plan.LifecycleState == state)
            {
                return;
            }

            if (IsTerminal(
                    plan.LifecycleState) &&
                !authoritativeOverride)
            {
                return;
            }

            plan.LifecycleState =
                state;

            plan.LifecycleUpdatedUtc =
                DateTime.UtcNow;

            if (terminal)
            {
                plan.TerminalUtc =
                    DateTime.UtcNow;
            }
        }

        public static
            System.Collections.Generic.List<KmcQueuedManeuverPlan>
            GetAll()
        {
            lock (SyncRoot)
            {
                System.Collections.Generic.List<KmcQueuedManeuverPlan>
                    copy =
                        new System.Collections.Generic.List<KmcQueuedManeuverPlan>();

                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    copy.Add(
                        Clone(
                            Plans[index]));
                }

                return copy;
            }
        }

        public static KmcQueuedManeuverPlan GetByPlanId(
            string planId)
        {
            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    if (string.Equals(
                            Plans[index].PlanId,
                            planId ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        return
                            Clone(
                                Plans[index]);
                    }
                }
            }

            return null;
        }

        public static KmcQueuedManeuverPlan FindForNode(
            ManeuverInventoryNode node,
            string vesselId)
        {
            if (node == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    KmcQueuedManeuverPlan plan =
                        Plans[index];

                    if (!string.Equals(
                            plan.VesselId ?? string.Empty,
                            vesselId ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Math.Abs(
                            node.NodeUniversalTimeSeconds -
                            plan.NodeUniversalTimeSeconds) <=
                            NodeUtToleranceSeconds &&
                        Math.Abs(
                            node.ProgradeDeltaVMetersPerSecond -
                            plan.ProgradeDeltaVMetersPerSecond) <=
                            DeltaVToleranceMetersPerSecond &&
                        Math.Abs(
                            node.NormalDeltaVMetersPerSecond -
                            plan.NormalDeltaVMetersPerSecond) <=
                            DeltaVToleranceMetersPerSecond &&
                        Math.Abs(
                            node.RadialDeltaVMetersPerSecond -
                            plan.RadialDeltaVMetersPerSecond) <=
                            DeltaVToleranceMetersPerSecond)
                    {
                        return
                            Clone(
                                plan);
                    }
                }
            }

            return null;
        }

        public static int GetSequence(
            string planId)
        {
            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    if (string.Equals(
                            Plans[index].PlanId,
                            planId ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        return index + 1;
                    }
                }
            }

            return -1;
        }

        public static void Select(
            string planId)
        {
            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Plans.Count;
                     index++)
                {
                    if (string.Equals(
                            Plans[index].PlanId,
                            planId ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        _selectedPlanId =
                            planId ?? string.Empty;

                        return;
                    }
                }
            }
        }

        public static string GetSelectedPlanId()
        {
            lock (SyncRoot)
            {
                return
                    _selectedPlanId ?? string.Empty;
            }
        }

        public static void ClearSelection()
        {
            lock (SyncRoot)
            {
                _selectedPlanId =
                    string.Empty;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Plans.Clear();
                _selectedPlanId =
                    string.Empty;
            }
        }

        private static KmcQueuedManeuverPlan Clone(
            KmcQueuedManeuverPlan source)
        {
            if (source == null)
            {
                return null;
            }

            KmcQueuedManeuverPlan copy =
                new KmcQueuedManeuverPlan
                {
                    Available = source.Available,
                    OrbitTargetVerificationRequired =
                        source.OrbitTargetVerificationRequired,
                    PlanId = source.PlanId,
                    VesselId = source.VesselId,
                    Objective = source.Objective,
                    NodeUniversalTimeAvailable =
                        source.NodeUniversalTimeAvailable,
                    NodeUniversalTimeSeconds =
                        source.NodeUniversalTimeSeconds,
                    NodeMissionTimeSeconds =
                        source.NodeMissionTimeSeconds,
                    TimeToNodeSeconds =
                        source.TimeToNodeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        source.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        source.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        source.RadialDeltaVMetersPerSecond,
                    TotalDeltaVMetersPerSecond =
                        source.TotalDeltaVMetersPerSecond,
                    EstimatedBurnDurationSeconds =
                        source.EstimatedBurnDurationSeconds,
                    IgnitionLeadSeconds =
                        source.IgnitionLeadSeconds,
                    IgnitionMissionTimeSeconds =
                        source.IgnitionMissionTimeSeconds,
                    PredictedApoapsisMeters =
                        source.PredictedApoapsisMeters,
                    PredictedPeriapsisMeters =
                        source.PredictedPeriapsisMeters,
                    PredictedInclinationDegrees =
                        source.PredictedInclinationDegrees,
                    PredictedEccentricity =
                        source.PredictedEccentricity,
                    PredictedPeriodSeconds =
                        source.PredictedPeriodSeconds,
                    Status = source.Status,
                    CapturedUtc = source.CapturedUtc,
                    LifecycleState =
                        source.LifecycleState,
                    LifecycleUpdatedUtc =
                        source.LifecycleUpdatedUtc,
                    TerminalUtc =
                        source.TerminalUtc,
                    RemovalRequested =
                        source.RemovalRequested,
                    RemovalRequestedAfterBurnComplete =
                        source.RemovalRequestedAfterBurnComplete,
                    DeliveredDeltaVMetersPerSecond =
                        source.DeliveredDeltaVMetersPerSecond,
                    BurnProgressPercent =
                        source.BurnProgressPercent,
                    ActualNodeDeltaVMetersPerSecond =
                        source.ActualNodeDeltaVMetersPerSecond,
                    PostBurnResult =
                        source.PostBurnResult ?? string.Empty,
                    ResultCapturedUtc =
                        source.ResultCapturedUtc
                };

            for (int index = 0;
                 index < source.Evidence.Count;
                 index++)
            {
                copy.Evidence.Add(
                    source.Evidence[index]);
            }

            return copy;
        }
    }

    /// <summary>
    /// Build 13.6 immutable Mission Control view of the complete stock KSP
    /// maneuver-node inventory for the active vessel.
    /// </summary>
    internal sealed class ManeuverInventoryNode
    {
        public string NodeId { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
    }

    internal sealed class ManeuverInventorySnapshot
    {
        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public System.Collections.Generic.List<ManeuverInventoryNode> Nodes { get; private set; }

        public ManeuverInventorySnapshot()
        {
            VesselId = string.Empty;
            VesselName = string.Empty;
            UniversalTimeSeconds = double.NaN;
            ReceivedUtc = DateTime.MinValue;
            Nodes = new System.Collections.Generic.List<ManeuverInventoryNode>();
        }
    }

    internal static class ManeuverInventoryStore
    {
        private static readonly object SyncRoot = new object();
        private static ManeuverInventorySnapshot _latest = new ManeuverInventorySnapshot();

        public static void Publish(ManeuverInventorySnapshot snapshot)
        {
            lock (SyncRoot)
            {
                _latest = Clone(snapshot);
            }
        }

        public static ManeuverInventorySnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                return Clone(_latest);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = new ManeuverInventorySnapshot();
            }
        }

        private static ManeuverInventorySnapshot Clone(ManeuverInventorySnapshot source)
        {
            ManeuverInventorySnapshot copy = new ManeuverInventorySnapshot();

            if (source == null)
            {
                return copy;
            }

            copy.VesselId = source.VesselId ?? string.Empty;
            copy.VesselName = source.VesselName ?? string.Empty;
            copy.UniversalTimeSeconds = source.UniversalTimeSeconds;
            copy.ReceivedUtc = source.ReceivedUtc;

            for (int index = 0; index < source.Nodes.Count; index++)
            {
                ManeuverInventoryNode node = source.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                copy.Nodes.Add(
                    new ManeuverInventoryNode
                    {
                        NodeId = node.NodeId ?? string.Empty,
                        NodeUniversalTimeSeconds = node.NodeUniversalTimeSeconds,
                        ProgradeDeltaVMetersPerSecond = node.ProgradeDeltaVMetersPerSecond,
                        NormalDeltaVMetersPerSecond = node.NormalDeltaVMetersPerSecond,
                        RadialDeltaVMetersPerSecond = node.RadialDeltaVMetersPerSecond
                    });
            }

            return copy;
        }
    }

    internal static class ManeuverInventoryFormatting
    {
        private const double AxisTolerance = 0.05;

        public static string DescribeVector(ManeuverInventoryNode node)
        {
            if (node == null)
            {
                return "---";
            }

            double p = node.ProgradeDeltaVMetersPerSecond;
            double n = node.NormalDeltaVMetersPerSecond;
            double r = node.RadialDeltaVMetersPerSecond;

            bool hasP = Math.Abs(p) > AxisTolerance;
            bool hasN = Math.Abs(n) > AxisTolerance;
            bool hasR = Math.Abs(r) > AxisTolerance;
            int axes = (hasP ? 1 : 0) + (hasN ? 1 : 0) + (hasR ? 1 : 0);

            if (axes == 0) return "ZERO DV";
            if (axes > 1) return "MIXED";
            if (hasP) return p >= 0.0 ? "PROGRADE" : "RETROGRADE";
            if (hasN) return n >= 0.0 ? "NORMAL" : "ANTI-NORMAL";
            return r >= 0.0 ? "RADIAL OUT" : "RADIAL IN";
        }

        public static double TotalDeltaV(ManeuverInventoryNode node)
        {
            if (node == null) return double.NaN;

            double p = node.ProgradeDeltaVMetersPerSecond;
            double n = node.NormalDeltaVMetersPerSecond;
            double r = node.RadialDeltaVMetersPerSecond;
            return Math.Sqrt(p * p + n * n + r * r);
        }
    }

    /// <summary>
    /// Build 13.6 dedicated node-inventory/delete link.
    /// KMC-MNVI1: KSP -> Mission Control UDP 5100.
    /// KMC-MNVD1: Mission Control -> KSP UDP 5101.
    /// </summary>
    internal sealed class ManeuverQueueTransport : IDisposable
    {
        private const int InventoryPort = 5100;
        private const int DeletePort = 5101;
        private const string InventoryProtocol = "KMC-MNVI1";
        private const string DeleteProtocol = "KMC-MNVD1";

        private System.Net.Sockets.UdpClient _inventoryClient;
        private System.Net.Sockets.UdpClient _commandClient;
        private System.Threading.Thread _thread;
        private volatile bool _running;

        public event Action<ManeuverInventorySnapshot> InventoryReceived;

        public void Start()
        {
            if (_running) return;

            _inventoryClient =
                new System.Net.Sockets.UdpClient(
                    new System.Net.IPEndPoint(
                        System.Net.IPAddress.Any,
                        InventoryPort));

            _commandClient = new System.Net.Sockets.UdpClient();
            _running = true;

            _thread = new System.Threading.Thread(ReceiveLoop);
            _thread.IsBackground = true;
            _thread.Name = "KMC Maneuver Inventory";
            _thread.Start();

            Debug.WriteLine("KMC.Transport BOUND | UDP " + InventoryPort);
        }

        public void SendDelete(string vesselId, string nodeId)
        {
            if (!_running || _commandClient == null)
            {
                throw new InvalidOperationException("Maneuver inventory link is not running.");
            }

            if (string.IsNullOrWhiteSpace(vesselId) ||
                string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException("Vessel and node identity are required.");
            }

            string message =
                DeleteProtocol + "|" +
                Uri.EscapeDataString(vesselId) + "|" +
                Uri.EscapeDataString(nodeId);

            byte[] data = System.Text.Encoding.UTF8.GetBytes(message);

            _commandClient.Send(
                data,
                data.Length,
                new System.Net.IPEndPoint(
                    System.Net.IPAddress.Loopback,
                    DeletePort));

            Debug.WriteLine(
                "KMC.MissionControl MANEUVER DELETE SENT" +
                " | VesselId=" + vesselId +
                " | NodeId=" + nodeId);
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    System.Net.IPEndPoint sender =
                        new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);

                    byte[] data = _inventoryClient.Receive(ref sender);
                    ManeuverInventorySnapshot snapshot;

                    if (!TryParseInventory(
                            System.Text.Encoding.UTF8.GetString(data),
                            out snapshot))
                    {
                        continue;
                    }

                    ManeuverInventoryStore.Publish(snapshot);

                    Action<ManeuverInventorySnapshot> handler = InventoryReceived;
                    if (handler != null) handler(snapshot);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    if (!_running) return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "KMC.MissionControl MANEUVER INVENTORY ERROR | " +
                        ex.Message);
                }
            }
        }

        private static bool TryParseInventory(
            string message,
            out ManeuverInventorySnapshot snapshot)
        {
            snapshot = null;

            if (string.IsNullOrWhiteSpace(message)) return false;
            string[] fields = message.Split('|');

            if (fields.Length < 6 ||
                !string.Equals(fields[0], InventoryProtocol, StringComparison.Ordinal))
            {
                return false;
            }

            double currentUt;
            int count;

            if (!double.TryParse(
                    fields[4],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out currentUt) ||
                !int.TryParse(fields[5], out count) ||
                count < 0 ||
                fields.Length != 6 + count)
            {
                return false;
            }

            ManeuverInventorySnapshot result =
                new ManeuverInventorySnapshot
                {
                    VesselId = Uri.UnescapeDataString(fields[2]),
                    VesselName = Uri.UnescapeDataString(fields[3]),
                    UniversalTimeSeconds = currentUt,
                    ReceivedUtc = DateTime.UtcNow
                };

            for (int index = 0; index < count; index++)
            {
                string[] nodeFields = fields[6 + index].Split('~');
                if (nodeFields.Length != 5) return false;

                double ut;
                double prograde;
                double normal;
                double radial;

                if (!TryDouble(nodeFields[1], out ut) ||
                    !TryDouble(nodeFields[2], out prograde) ||
                    !TryDouble(nodeFields[3], out normal) ||
                    !TryDouble(nodeFields[4], out radial))
                {
                    return false;
                }

                result.Nodes.Add(
                    new ManeuverInventoryNode
                    {
                        NodeId = Uri.UnescapeDataString(nodeFields[0]),
                        NodeUniversalTimeSeconds = ut,
                        ProgradeDeltaVMetersPerSecond = prograde,
                        NormalDeltaVMetersPerSecond = normal,
                        RadialDeltaVMetersPerSecond = radial
                    });
            }

            result.Nodes.Sort(
                delegate(ManeuverInventoryNode a, ManeuverInventoryNode b)
                {
                    return a.NodeUniversalTimeSeconds.CompareTo(
                        b.NodeUniversalTimeSeconds);
                });

            snapshot = result;
            return !string.IsNullOrWhiteSpace(result.VesselId);
        }

        private static bool TryDouble(string text, out double value)
        {
            return double.TryParse(
                       text,
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out value) &&
                   !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

        public void Stop()
        {
            _running = false;

            if (_inventoryClient != null)
            {
                _inventoryClient.Close();
                _inventoryClient = null;
            }

            if (_commandClient != null)
            {
                _commandClient.Close();
                _commandClient = null;
            }

            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(250);
            }

            _thread = null;
            ManeuverInventoryStore.Clear();
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class ElectricalControlReceiver :
        IDisposable
    {
        private const int Port = 5102;
        private const string ProtocolId = "KMC-ELEC1";

        private System.Net.Sockets.UdpClient _client;
        private System.Threading.Thread _thread;
        private volatile bool _running;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _client =
                new System.Net.Sockets.UdpClient(
                    new System.Net.IPEndPoint(
                        System.Net.IPAddress.Any,
                        Port));

            _running =
                true;

            _thread =
                new System.Threading.Thread(
                    ReceiveLoop);

            _thread.IsBackground =
                true;

            _thread.Name =
                "KMC Electrical Control";

            _thread.Start();

            Debug.WriteLine(
                "KMC.Transport BOUND | UDP " +
                Port);
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    System.Net.IPEndPoint sender =
                        new System.Net.IPEndPoint(
                            System.Net.IPAddress.Any,
                            0);

                    byte[] payload =
                        _client.Receive(
                            ref sender);

                    Handle(
                        System.Text.Encoding.UTF8.GetString(
                            payload));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
            }
        }

        private static void Handle(
            string message)
        {
            if (string.IsNullOrWhiteSpace(
                    message))
            {
                return;
            }

            string[] fields =
                message.Split('|');

            if (fields.Length < 4 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal))
            {
                return;
            }

            string vesselId =
                Uri.UnescapeDataString(
                    fields[1]);

            string controlId =
                Uri.UnescapeDataString(
                    fields[2]);

            if (string.IsNullOrWhiteSpace(
                    vesselId))
            {
                return;
            }

            if (string.Equals(
                    controlId,
                    "RESET",
                    StringComparison.Ordinal))
            {
                ElectricalControlCommandStore.Reset(
                    vesselId);

                Debug.WriteLine(
                    "KMC.MissionControl ELECTRICAL CONTROL" +
                    " | VesselId=" +
                    vesselId +
                    " | RESET NOMINAL");

                return;
            }

            bool commandedOn =
                fields[3] == "1";

            ElectricalControlCommandStore.Publish(
                vesselId,
                controlId,
                commandedOn);

            Debug.WriteLine(
                "KMC.MissionControl ELECTRICAL CONTROL" +
                " | VesselId=" +
                vesselId +
                " | Control=" +
                controlId +
                " | Command=" +
                (commandedOn
                    ? "ON/CLOSED"
                    : "OFF/OPEN"));
        }

        public void Dispose()
        {
            _running =
                false;

            System.Net.Sockets.UdpClient client =
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

            System.Threading.Thread thread =
                _thread;

            _thread =
                null;

            if (thread != null &&
                thread.IsAlive)
            {
                thread.Join(
                    250);
            }
        }
    }

}
