using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KMC.MissionControl.Models;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Transport;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.OrbitMap;
using KMC.MissionControl.Telemetry;
using KMC.Shared;
using KMC.Engine.Navigation;
using KMC.MissionControl.Navigation;

namespace KMC.MissionControl.Pages
{
    public sealed class MapPage : IMissionPage, IMissionPageCanvasProvider, IMissionPagePointerInput
    {
        private readonly OrbitMapSceneCache _cache = new OrbitMapSceneCache();
        private readonly OrbitMapCamera _camera = new OrbitMapCamera();
        private readonly OrbitMapRenderer _renderer = new OrbitMapRenderer();
        private readonly List<Rectangle> _transferBodyButtons = new List<Rectangle>();
        private readonly List<string> _transferBodyNames = new List<string>();
        private Rectangle _viewport;
        private Rectangle _resetButton, _fitOrbitButton, _fitManeuverButton, _targetButton;
        private Rectangle _localTab, _transferTab;
        private Rectangle _createTransferNodeButton;
        private Rectangle _refineTransferNodeButton;
        private Rectangle _peMinus100Button;
        private Rectangle _peMinus10Button;
        private Rectangle _pePlus10Button;
        private Rectangle _pePlus100Button;
        private Rectangle _peAutoButton;
        private const int ButtonGap = 16;
        private const int ButtonHorizontalPadding = 14;
        private const int ButtonHeight = 24;
        private bool _dragging;
        private PointF _lastPointer;
        private bool _initialFitDone;
        private int _subpage;
        private string _selectedTransferBodyName = string.Empty;
        // NaN means AUTO. Manual values are altitude above the live target surface.
        private double _transferTargetPeriapsisAltitudeMeters = double.NaN;
        private ManeuverUplinkPacket _transferNodeCandidate;
        private string _lastTransferPlanId = string.Empty;
        private string _transferNodeActionText = "NO NODE REQUEST";
        private string _submittedTransferDestinationName = string.Empty;
        private double _submittedTransferNodeUt = double.NaN;
        private double _submittedTransferProgradeDv = double.NaN;
        private double _submittedTransferNormalDv = double.NaN;
        private double _submittedTransferRadialDv = double.NaN;
        private bool _submittedTransferUsedCoupledAuthority;
        private bool _submittedTransferUsedLambertAuthority;
        private string _currentTransferVesselId = string.Empty;
        private TransferSearchSolution _lambertPreview;
        private LambertParkingOrbitEjectionSolution _lambertEjectionPreview;
        private ParkingOrbitAwareTransferSolution _parkingAwareLambertPreview;
        private FiniteSoiDepartureCorrectionResult _finiteSoiCorrection;
        private TargetSoiShootingResult _targetSoiShooting;
        private string _targetSoiShootingFailureText = string.Empty;
        private CoupledFiniteSoiResult _coupledFiniteSoiResult;
        private string _coupledFiniteSoiFailureText = string.Empty;
        private bool _lambertPreviewAttempted;
        private string _lambertPreviewOriginName = string.Empty;
        private string _lambertPreviewDestinationName = string.Empty;
        private string _lambertPreviewMuSource = string.Empty;
        private double _lambertPreviewHohmannDepartureUt = double.NaN;

        public string Name { get { return "ORBIT MAP"; } }
        public Size PreferredVirtualCanvasSize { get { return Size.Empty; } }
        public MissionPageContentProfile ContentProfile { get { return MissionPageContentProfile.DenseEngineering; } }
        public long GeometryRebuildCount { get { return _cache.GeometryRebuildCount; } }
        public long SnapshotUpdateCount { get { return _cache.SnapshotUpdateCount; } }

        public void Draw(MissionRenderContext context, MissionTelemetry telemetry)
        {
            Rectangle b = context.ContentBounds;
            using (SolidBrush brush = new SolidBrush(context.PhosphorColor)) context.Graphics.DrawString("ORBIT MAP", context.LargeFont, brush, b.Left, b.Top);
            using (SolidBrush brush = new SolidBrush(context.DimPhosphorColor)) context.Graphics.DrawString("FDO / TRAJECTORY", context.SmallFont, brush, b.Right - 250, b.Top + 6);

            DrawSubpageTabs(context, b);

            OrbitMapPacket packet;
            DateTime received;
            bool hasPacket = OrbitMapSnapshotStore.TryGetLatest(out packet, out received);
            OrbitMapFreshness freshness = OrbitMapSnapshotStore.GetFreshness(DateTime.UtcNow);

            if (_subpage == 1)
            {
                DrawTransferPlanner(context, b, hasPacket ? packet : null, freshness);
                return;
            }

            int header = 82;
            int panelHeight = Math.Max(120, (int)(b.Height * 0.25));
            _viewport = new Rectangle(b.Left, b.Top + header, b.Width, Math.Max(180, b.Height - header - panelHeight - 8));
            int resetWidth = MeasureButtonWidth(context.Graphics, context.SmallFont, "RESET VIEW");
            int orbitWidth = MeasureButtonWidth(context.Graphics, context.SmallFont, "FIT ORBIT");
            int maneuverWidth = MeasureButtonWidth(context.Graphics, context.SmallFont, "FIT MANEUVER");
            int targetWidth = MeasureButtonWidth(context.Graphics, context.SmallFont, "TARGET");

            int availableWidth = Math.Max(0, _viewport.Width - 16);
            int totalWidth = resetWidth + orbitWidth + maneuverWidth + targetWidth + (ButtonGap * 3);
            int effectiveGap = ButtonGap;
            if (totalWidth > availableWidth)
            {
                effectiveGap = Math.Max(4, (availableWidth - resetWidth - orbitWidth - maneuverWidth - targetWidth) / 3);
            }

            int buttonTop = _viewport.Bottom - 32;
            int buttonLeft = _viewport.Left + 8;
            _resetButton = new Rectangle(buttonLeft, buttonTop, resetWidth, ButtonHeight);
            _fitOrbitButton = new Rectangle(_resetButton.Right + effectiveGap, buttonTop, orbitWidth, ButtonHeight);
            _fitManeuverButton = new Rectangle(_fitOrbitButton.Right + effectiveGap, buttonTop, maneuverWidth, ButtonHeight);
            _targetButton = new Rectangle(_fitManeuverButton.Right + effectiveGap, buttonTop, targetWidth, ButtonHeight);

            OrbitMapSceneSnapshot scene = null;
            if (hasPacket) scene = _cache.Update(packet);
            if (!_initialFitDone && scene != null)
            {
                _camera.Fit(CalculateSceneRadius(scene));
                _initialFitDone = true;
            }
            _renderer.Draw(context, _viewport, scene, _camera, freshness);
            DrawButton(context, _resetButton, "RESET VIEW");
            DrawButton(context, _fitOrbitButton, "FIT ORBIT");
            DrawButton(context, _fitManeuverButton, "FIT MANEUVER");
            DrawButton(context, _targetButton, "TARGET");
            using (SolidBrush status = new SolidBrush(context.DimPhosphorColor)) context.Graphics.DrawString("RX " + OrbitMapSnapshotStore.ReceivedCount + "  GEOMETRY REBUILD " + _cache.GeometryRebuildCount, context.SmallFont, status, _viewport.Right - 390, _viewport.Top + 8);
        }

        public bool PointerDown(PointF p, MouseButtons button)
        {
            if (button != MouseButtons.Left) return false;
            Point q = Point.Round(p);

            if (_localTab.Contains(q))
            {
                _subpage = 0;
                _dragging = false;
                return true;
            }
            if (_transferTab.Contains(q))
            {
                _subpage = 1;
                _dragging = false;
                return true;
            }

            if (_subpage == 1)
            {
                if (HasProductionNodeCandidate() &&
                    !_createTransferNodeButton.IsEmpty &&
                    _createTransferNodeButton.Contains(q))
                {
                    UploadTransferNode();
                    return true;
                }

                if (!_refineTransferNodeButton.IsEmpty &&
                    _refineTransferNodeButton.Contains(q))
                {
                    RefineTransferNode();
                    return true;
                }

                if (!_peMinus100Button.IsEmpty && _peMinus100Button.Contains(q))
                    return AdjustTargetPeriapsisAltitude(-100000.0);
                if (!_peMinus10Button.IsEmpty && _peMinus10Button.Contains(q))
                    return AdjustTargetPeriapsisAltitude(-10000.0);
                if (!_pePlus10Button.IsEmpty && _pePlus10Button.Contains(q))
                    return AdjustTargetPeriapsisAltitude(10000.0);
                if (!_pePlus100Button.IsEmpty && _pePlus100Button.Contains(q))
                    return AdjustTargetPeriapsisAltitude(100000.0);
                if (!_peAutoButton.IsEmpty && _peAutoButton.Contains(q))
                {
                    SetTargetPeriapsisAuto();
                    return true;
                }

                for (int i = 0; i < _transferBodyButtons.Count && i < _transferBodyNames.Count; i++)
                {
                    if (_transferBodyButtons[i].Contains(q))
                    {
                        _selectedTransferBodyName = _transferBodyNames[i];
                        _transferTargetPeriapsisAltitudeMeters = double.NaN;
                        _lastTransferPlanId = string.Empty;
                        _transferNodeActionText = "NO NODE REQUEST";
                        _submittedTransferDestinationName = string.Empty;
                        _submittedTransferNodeUt = double.NaN;
                        _submittedTransferProgradeDv = double.NaN;
                        _submittedTransferNormalDv = double.NaN;
                        _submittedTransferRadialDv = double.NaN;
                        _submittedTransferUsedCoupledAuthority = false;
                        _submittedTransferUsedLambertAuthority = false;
                        ResetLambertPreview();
                        return true;
                    }
                }
                return false;
            }

            if (_viewport.Contains(q))
            {
                if (HitButton(p)) return true;
                _dragging = true;
                _lastPointer = p;
                return true;
            }
            return false;
        }

        public bool PointerMove(PointF p, MouseButtons buttons)
        {
            if (_subpage != 0 || !_dragging || (buttons & MouseButtons.Left) == 0) return false;
            _camera.Rotate(p.X - _lastPointer.X, p.Y - _lastPointer.Y);
            _lastPointer = p;
            return true;
        }

        public bool PointerUp(PointF p, MouseButtons button)
        {
            bool was = _dragging;
            _dragging = false;
            return was;
        }

        public bool PointerWheel(PointF p, int delta)
        {
            if (_subpage != 0 || !_viewport.Contains(Point.Round(p))) return false;
            _camera.Zoom(delta);
            return true;
        }

        private bool HitButton(PointF p)
        {
            Point q = Point.Round(p);
            OrbitMapSceneSnapshot scene = _cache.Current;
            if (_resetButton.Contains(q)) { _camera.Reset(); _initialFitDone = false; return true; }
            if (_fitOrbitButton.Contains(q)) { if (scene != null) _camera.Fit(MaxMagnitude(scene.ActiveOrbitPoints, scene.BodyRadiusMeters)); return true; }
            if (_fitManeuverButton.Contains(q)) { if (scene != null) _camera.Fit(CalculateSceneRadius(scene)); return true; }
            if (_targetButton.Contains(q)) { if (scene != null && scene.Target != null && scene.Target.Present) _camera.Fit(Math.Max(scene.BodyRadiusMeters, scene.TargetPosition.Magnitude) * 1.4); return true; }
            return false;
        }

        private void DrawSubpageTabs(MissionRenderContext context, Rectangle bounds)
        {
            const int tabWidth = 110;
            const int tabHeight = 22;
            const int gap = 8;
            int y = bounds.Top + 42;
            _localTab = new Rectangle(bounds.Left, y, tabWidth, tabHeight);
            _transferTab = new Rectangle(_localTab.Right + gap, y, tabWidth, tabHeight);
            DrawTab(context, _localTab, "LOCAL", _subpage == 0);
            DrawTab(context, _transferTab, "TRANSFER", _subpage == 1);
        }

        private void DrawTransferPlanner(MissionRenderContext context, Rectangle bounds, OrbitMapPacket packet, OrbitMapFreshness freshness)
        {
            _transferBodyButtons.Clear();
            _transferBodyNames.Clear();
            _transferNodeCandidate = null;
            _currentTransferVesselId = packet != null
                ? (packet.VesselId ?? string.Empty)
                : string.Empty;
            _createTransferNodeButton = Rectangle.Empty;
            _refineTransferNodeButton = Rectangle.Empty;
            _peMinus100Button = Rectangle.Empty;
            _peMinus10Button = Rectangle.Empty;
            _pePlus10Button = Rectangle.Empty;
            _pePlus100Button = Rectangle.Empty;
            _peAutoButton = Rectangle.Empty;

            int top = bounds.Top + 84;
            Rectangle systemPanel = new Rectangle(bounds.Left, top, Math.Max(420, (int)(bounds.Width * 0.58)), bounds.Bottom - top);
            Rectangle plannerPanel = new Rectangle(systemPanel.Right + 8, top, Math.Max(1, bounds.Right - systemPanel.Right - 8), bounds.Bottom - top);
            DrawPanelFrame(context, systemPanel, "SYSTEM BODIES");
            DrawPanelFrame(context, plannerPanel, "TRANSFER PLANNER");

            if (packet == null || freshness == OrbitMapFreshness.Unavailable)
            {
                using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
                    context.Graphics.DrawString("SYSTEM DATA UNAVAILABLE", context.SmallFont, brush, systemPanel.Left + 10, systemPanel.Top + 36);
                return;
            }

            string origin = packet.ReferenceBodyName ?? string.Empty;
            EnsureTransferSelection(packet, origin);
            OrbitMapBody destination = FindBody(packet.Bodies, _selectedTransferBodyName);
            OrbitMapBody originBody = FindBody(packet.Bodies, origin);

            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.DrawString("KSP CATALOG " + packet.Bodies.Count + " / LIMIT " + OrbitMapPacket.MaxBodies, context.SmallFont, dim, systemPanel.Left + 10, systemPanel.Top + 34);
                context.Graphics.DrawString("ORIGIN  " + (string.IsNullOrWhiteSpace(origin) ? "---" : origin), context.SmallFont, bright, systemPanel.Left + 10, systemPanel.Top + 54);
                context.Graphics.DrawString("SELECT DESTINATION", context.SmallFont, dim, systemPanel.Left + 10, systemPanel.Top + 78);
            }

            int columns = 3;
            int gap = 8;
            int left = systemPanel.Left + 10;
            int gridTop = systemPanel.Top + 102;
            int usableWidth = systemPanel.Width - 20;
            int cellWidth = Math.Max(90, (usableWidth - gap * (columns - 1)) / columns);
            int cellHeight = 28;
            int slot = 0;

            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];
                if (body == null || string.IsNullOrWhiteSpace(body.Name)) continue;
                if (string.Equals(body.Name, origin, StringComparison.OrdinalIgnoreCase)) continue;

                int col = slot % columns;
                int row = slot / columns;
                Rectangle r = new Rectangle(left + col * (cellWidth + gap), gridTop + row * (cellHeight + gap), cellWidth, cellHeight);
                bool selected = string.Equals(body.Name, _selectedTransferBodyName, StringComparison.OrdinalIgnoreCase);
                DrawTransferBodyButton(context, r, body.Name, selected);
                _transferBodyButtons.Add(r);
                _transferBodyNames.Add(body.Name);
                slot++;
            }

            int x = plannerPanel.Left + 12;
            int y = plannerPanel.Top + 36;

            int transferFontHeight =
                (int)Math.Ceiling(
                    context.SmallFont.GetHeight(
                        context.Graphics));

            int transferLineHeight =
                Math.Max(
                    24,
                    transferFontHeight + 5);

            int transferSectionGap =
                Math.Max(
                    4,
                    transferFontHeight / 4);

            /*
             * NODE STATUS is bottom anchored and can occupy roughly 220 px
             * with a full KSP assessment. Keep all planner diagnostics above
             * this hard boundary so the two regions can never collide.
             */
            int transferContentBottom =
                plannerPanel.Bottom - 245;

            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    "ORIGIN  " +
                    (string.IsNullOrWhiteSpace(origin)
                        ? "---"
                        : origin),
                    context.SmallFont,
                    bright,
                    x,
                    y);
                y += transferLineHeight;

                context.Graphics.DrawString(
                    "DESTINATION  " +
                    (destination != null
                        ? destination.Name
                        : "SELECT BODY"),
                    context.SmallFont,
                    bright,
                    x,
                    y);
                y += transferLineHeight + transferSectionGap;

                if (destination != null)
                {
                    if (destination.Orbit != null)
                    {
                        context.Graphics.DrawString(
                            "PARENT " +
                            (string.IsNullOrWhiteSpace(destination.ParentName)
                                ? "---"
                                : destination.ParentName) +
                            "  SMA " +
                            FormatSystemDistance(destination.Orbit.SemiMajorAxisMeters) +
                            "  ECC " +
                            destination.Orbit.Eccentricity.ToString("0.0000"),
                            context.SmallFont,
                            dim,
                            x,
                            y);
                        y += transferLineHeight;

                        context.Graphics.DrawString(
                            "INC " +
                            destination.Orbit.InclinationDegrees.ToString("0.00") +
                            " deg  PERIOD " +
                            FormatDuration(destination.Orbit.PeriodSeconds),
                            context.SmallFont,
                            dim,
                            x,
                            y);
                        y += transferLineHeight + transferSectionGap;
                    }

                    string relation =
                        originBody != null &&
                        string.Equals(
                            originBody.ParentName,
                            destination.ParentName,
                            StringComparison.OrdinalIgnoreCase)
                            ? "SAME PARENT"
                            : "HIERARCHY CHANGE";

                    context.Graphics.DrawString(
                        "ROUTE  " + (relation == "SAME PARENT"
                            ? "DIRECT PLANET TRANSFER"
                            : "HIERARCHY CHANGE"),
                        context.SmallFont,
                        dim,
                        x,
                        y);
                    y += transferLineHeight;

                    DrawTargetPeriapsisControls(
                        context,
                        destination,
                        plannerPanel,
                        x,
                        ref y,
                        transferLineHeight,
                        transferSectionGap);

                    TransferWindowSolution solution;

                    if (OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                            originBody,
                            destination,
                            packet.UniversalTimeSeconds,
                            out solution))
                    {
                        context.Graphics.DrawString(
                            "TRANSFER WINDOW",
                            context.SmallFont,
                            bright,
                            x,
                            y);
                        y += transferLineHeight;

                        context.Graphics.DrawString(
                            "DEPART IN " +
                            FormatTransferInterval(solution.WaitSeconds) +
                            "  EST FLIGHT " +
                            FormatTransferInterval(solution.TransferTimeSeconds),
                            context.SmallFont,
                            dim,
                            x,
                            y);
                        y += transferLineHeight + transferSectionGap;

                        EnsureLambertPreview(
                            packet,
                            originBody,
                            destination,
                            solution);

                        bool showFallbackDiagnostics =
                            _coupledFiniteSoiResult == null ||
                            !IsCoupledProductionReady(_coupledFiniteSoiResult);

                        if (showFallbackDiagnostics &&
                            y + transferLineHeight * 3 <
                            transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                "LAMBERT SEARCH / FALLBACK",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            if (_lambertPreview != null)
                            {
                                context.Graphics.DrawString(
                                    "PATH " +
                                    _lambertPreview.Path.ToString().ToUpperInvariant() +
                                    "  DEP UT " +
                                    FormatMissionTime(_lambertPreview.DepartureUniversalTimeSeconds) +
                                    "  TOF " +
                                    FormatTransferInterval(_lambertPreview.TimeOfFlightSeconds),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;

                                context.Graphics.DrawString(
                                    "VINF DEP " +
                                    _lambertPreview.DepartureExcessSpeedMetersPerSecond.ToString("0.0") +
                                    "  ARR " +
                                    _lambertPreview.ArrivalExcessSpeedMetersPerSecond.ToString("0.0") +
                                    "  SCORE " +
                                    _lambertPreview.CombinedExcessSpeedMetersPerSecond.ToString("0.0") +
                                    " m/s",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight + transferSectionGap;
                            }
                            else
                            {
                                context.Graphics.DrawString(
                                    "NO SUPPORTED SAME-PARENT LAMBERT SAMPLE",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight + transferSectionGap;
                            }
                        }

                        if (showFallbackDiagnostics &&
                            _lambertEjectionPreview != null &&
                            y + transferLineHeight * 4 <
                                transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                "LAMBERT EJECTION / PRODUCTION",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            if (_parkingAwareLambertPreview != null)
                            {
                                context.Graphics.DrawString(
                                    "SELECT " +
                                    _parkingAwareLambertPreview.Transfer.Path.ToString().ToUpperInvariant() +
                                    "  DEP UT " +
                                    FormatMissionTime(_parkingAwareLambertPreview.Transfer.DepartureUniversalTimeSeconds) +
                                    "  ARR VINF " +
                                    _parkingAwareLambertPreview.ArrivalExcessSpeedMetersPerSecond.ToString("0.0"),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            context.Graphics.DrawString(
                                "BURN UT " +
                                FormatMissionTime(_lambertEjectionPreview.BurnUniversalTimeSeconds) +
                                "  OFF " +
                                FormatSignedMinutes(_lambertEjectionPreview.WindowOffsetSeconds),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "DV P/N/R " +
                                _lambertEjectionPreview.ProgradeDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                " / " +
                                _lambertEjectionPreview.NormalDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                " / " +
                                _lambertEjectionPreview.RadialDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                "  TOTAL " +
                                _lambertEjectionPreview.TotalDeltaVMetersPerSecond.ToString("0.0"),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }

                        FiniteSoiDepartureAssessment finiteSoi =
                            _finiteSoiCorrection != null
                                ? _finiteSoiCorrection.CorrectedAssessment
                                : (_parkingAwareLambertPreview != null
                                    ? _parkingAwareLambertPreview.FiniteSoiAssessment
                                    : null);

                        // Keep the normal operator view compact. Detailed numerical
                        // diagnostics remain visible only when the coupled solution has
                        // not reached production authority, where they are useful for
                        // understanding why fallback may be required.
                        if (_coupledFiniteSoiResult != null &&
                            IsCoupledProductionReady(_coupledFiniteSoiResult) &&
                            y + transferLineHeight * 5 <= transferContentBottom)
                        {
                            CoupledFiniteSoiResult coupledResult =
                                _coupledFiniteSoiResult;

                            context.Graphics.DrawString(
                                "TRANSFER SOLUTION READY",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            if (coupledResult.FinalEjection != null)
                            {
                                double burnInSeconds =
                                    coupledResult.FinalEjection.BurnUniversalTimeSeconds -
                                    packet.UniversalTimeSeconds;

                                context.Graphics.DrawString(
                                    "BURN IN " +
                                    FormatTransferInterval(Math.Max(0.0, burnInSeconds)) +
                                    "  TOTAL DV " +
                                    coupledResult.FinalDeltaVMetersPerSecond.ToString("0.0") +
                                    " m/s",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;

                                context.Graphics.DrawString(
                                    "PROGRADE " +
                                    coupledResult.FinalEjection.ProgradeDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                    "  NORMAL " +
                                    coupledResult.FinalEjection.NormalDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                    "  RADIAL " +
                                    coupledResult.FinalEjection.RadialDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                    " m/s",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            if (coupledResult.TargetAssessment != null &&
                                IsFinitePositive(coupledResult.TargetAssessment.DesiredPeriapsisRadiusMeters) &&
                                IsFinitePositive(coupledResult.TargetAssessment.TargetPeriapsisRadiusMeters))
                            {
                                double desiredAltitude =
                                    coupledResult.TargetAssessment.DesiredPeriapsisRadiusMeters -
                                    destination.RadiusMeters;
                                double predictedAltitude =
                                    coupledResult.TargetAssessment.TargetPeriapsisRadiusMeters -
                                    destination.RadiusMeters;

                                context.Graphics.DrawString(
                                    "TARGET ALT " +
                                    FormatSystemDistance(desiredAltitude) +
                                    "  PRED " +
                                    FormatSystemDistance(predictedAltitude) +
                                    "  ERR " +
                                    FormatSystemDistance(coupledResult.TargetPeriapsisErrorMeters),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            context.Graphics.DrawString(
                                "STATUS READY TO CREATE NODE  AUTH COUPLED",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }
                        else if (_coupledFiniteSoiResult != null &&
                            y + transferLineHeight * 7 <= transferContentBottom)
                        {
                            CoupledFiniteSoiResult coupledResult =
                                _coupledFiniteSoiResult;

                            context.Graphics.DrawString(
                                "COUPLED FINITE-SOI OPTIMIZER / CANDIDATE",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "DV " +
                                coupledResult.BootstrapDeltaVMetersPerSecond.ToString("0.0") +
                                " -> " +
                                coupledResult.FinalDeltaVMetersPerSecond.ToString("0.0") +
                                " m/s  FEAS " +
                                (coupledResult.FeasibilityPassSucceeded ? "PASS" : "FAIL") +
                                "  " + coupledResult.Stage,
                                context.SmallFont,
                                coupledResult.FeasibilityPassSucceeded ? bright : dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "SOURCE IF " +
                                FormatSystemDistance(coupledResult.SourceInterfacePositionErrorMeters) +
                                " / " +
                                coupledResult.SourceInterfaceVelocityErrorMetersPerSecond.ToString("0.0") +
                                " m/s  SPLIT VERR " +
                                coupledResult.SplitVelocityMismatchMetersPerSecond.ToString("0.0") +
                                "  " +
                                (coupledResult.SourceOutbound ? "OUT" : "NOT OUT"),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "TARGET IF " +
                                FormatSystemDistance(coupledResult.TargetInterfaceErrorMeters) +
                                "  PE ERR " +
                                FormatSystemDistance(coupledResult.TargetPeriapsisErrorMeters) +
                                "  " +
                                (coupledResult.TargetInbound ? "IN" : "NOT IN"),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "B MAG ERR " +
                                FormatSystemDistance(coupledResult.TargetBPlaneMagnitudeErrorMeters) +
                                "  B.T " +
                                FormatSystemDistance(coupledResult.TargetBPlaneTErrorMeters) +
                                "  B.R " +
                                FormatSystemDistance(coupledResult.TargetBPlaneRErrorMeters),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "ITER " + coupledResult.Iterations.ToString() +
                                "  EVAL " + coupledResult.Evaluations.ToString() +
                                "  JAC " + coupledResult.TerminalJacobianColumns.ToString() + "/8" +
                                "  REJ " + coupledResult.TerminalRejectedSteps.ToString() +
                                "  TR " + coupledResult.TerminalTrustRadius.ToString("0.000") +
                                "  AUTH FALLBACK",
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }
                        else if (_coupledFiniteSoiResult == null &&
                            !string.IsNullOrWhiteSpace(
                                _coupledFiniteSoiFailureText) &&
                            y + transferLineHeight * 2 <=
                                transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                "COUPLED SOLVER REJECTED",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;
                            context.Graphics.DrawString(
                                _coupledFiniteSoiFailureText,
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }

                        if (showFallbackDiagnostics &&
                            _targetSoiShooting == null &&
                            finiteSoi != null &&
                            y + transferLineHeight * 4 <
                                transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                _finiteSoiCorrection != null &&
                                _finiteSoiCorrection.Applied
                                    ? "FINITE-SOI LOCAL CORRECTION"
                                    : "FINITE-SOI MATCH",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            if (_finiteSoiCorrection != null)
                            {
                                context.Graphics.DrawString(
                                    "SCORE " +
                                    _finiteSoiCorrection.InitialAssessment.NormalizedStateError.ToString("0.000000") +
                                    " -> " +
                                    finiteSoi.NormalizedStateError.ToString("0.000000") +
                                    "  ITER " +
                                    _finiteSoiCorrection.Iterations.ToString() +
                                    "  EVAL " +
                                    _finiteSoiCorrection.Evaluations.ToString(),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            context.Graphics.DrawString(
                                "POS ERR " +
                                FormatSystemDistance(
                                    finiteSoi.PositionErrorMeters) +
                                "  VEL ERR " +
                                finiteSoi.VelocityErrorMetersPerSecond.ToString("0.0") +
                                " m/s",
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "EXIT UT " +
                                FormatMissionTime(finiteSoi.ExitUniversalTimeSeconds) +
                                "  TO EXIT " +
                                FormatTransferInterval(
                                    finiteSoi.TimeFromBurnToExitSeconds),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }

                        if (showFallbackDiagnostics &&
                            _targetSoiShooting == null &&
                            !string.IsNullOrWhiteSpace(
                                _targetSoiShootingFailureText) &&
                            y + transferLineHeight * 2 <=
                                transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                "TARGET-SOI SHOOTING REJECTED",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                _targetSoiShootingFailureText,
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }

                        if (showFallbackDiagnostics &&
                            _targetSoiShooting != null &&
                            _targetSoiShooting.CorrectedAssessment != null &&
                            y + transferLineHeight * 8 <=
                                transferContentBottom)
                        {
                            TargetSoiShootingAssessment shot =
                                _targetSoiShooting.CorrectedAssessment;

                            context.Graphics.DrawString(
                                _parkingAwareLambertPreview != null
                                    ? "TARGET-SOI SHOOTING"
                                    : "TARGET-SOI SHOOTING / 3D BOOTSTRAP",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            double seedShootingDv =
                                _targetSoiShooting.InitialEjection != null
                                    ? _targetSoiShooting.InitialEjection.TotalDeltaVMetersPerSecond
                                    : double.NaN;

                            double finalShootingDv =
                                _targetSoiShooting.CorrectedEjection != null
                                    ? _targetSoiShooting.CorrectedEjection.TotalDeltaVMetersPerSecond
                                    : double.NaN;

                            double shootingDvCap =
                                TargetSoiOptimizationPolicy.
                                    ComputeMaximumDepartureDeltaV(
                                        seedShootingDv);

                            context.Graphics.DrawString(
                                "DV SEED " +
                                seedShootingDv.ToString("0.0") +
                                " -> " +
                                finalShootingDv.ToString("0.0") +
                                " m/s  CAP " +
                                shootingDvCap.ToString("0.0"),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            if (_parkingAwareLambertPreview != null &&
                                _parkingAwareLambertPreview.FiniteSoiAssessment != null)
                            {
                                FiniteSoiDepartureAssessment seedSoi =
                                    _parkingAwareLambertPreview.FiniteSoiAssessment;

                                context.Graphics.DrawString(
                                    "SOI SEED POS " +
                                    (seedSoi.PositionErrorFractionOfSoi * 100.0).ToString("0.0") +
                                    "%  VEL " +
                                    (seedSoi.VelocityErrorFractionOfDepartureExcess * 100.0).ToString("0.0") +
                                    "%  FEASIBLE / MIN-DV" +
                                    (_targetSoiShooting.BPlaneBootstrapApplied
                                        ? "  BPLANE SEARCH"
                                        : string.Empty),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            context.Graphics.DrawString(
                                "MISS " +
                                FormatSystemDistance(
                                    _targetSoiShooting.InitialAssessment.MissDistanceMeters) +
                                " -> " +
                                FormatSystemDistance(
                                    shot.MissDistanceMeters) +
                                "  / SOI " +
                                FormatSystemDistance(
                                    destination.SoiRadiusMeters),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "ARR UT " +
                                FormatMissionTime(shot.ArrivalUniversalTimeSeconds) +
                                "  VERR " +
                                shot.SourceLambertVelocityMismatchMetersPerSecond.ToString("0.0") +
                                " m/s  " +
                                (shot.PredictedEncounter
                                    ? "PREDICT ENCOUNTER"
                                    : "PREDICT MISS"),
                                context.SmallFont,
                                shot.PredictedEncounter
                                    ? bright
                                    : dim,
                                x,
                                y);
                            y += transferLineHeight;

                            if (shot.PredictedEncounter &&
                                IsFinitePositive(
                                    shot.TargetPeriapsisRadiusMeters))
                            {
                                context.Graphics.DrawString(
                                    "TARGET PE R " +
                                    FormatSystemDistance(
                                        shot.DesiredPeriapsisRadiusMeters) +
                                    "  PRED PE R " +
                                    FormatSystemDistance(
                                        shot.TargetPeriapsisRadiusMeters) +
                                    (IsFinitePositive(
                                         shot.TargetBPlaneErrorMeters)
                                        ? "  BERR " +
                                          FormatSystemDistance(
                                              shot.TargetBPlaneErrorMeters)
                                        : string.Empty),
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;

                                context.Graphics.DrawString(
                                    "PRED PE ALT " +
                                    FormatSystemDistance(
                                        shot.TargetPeriapsisAltitudeMeters) +
                                    "  " +
                                    (shot.PredictedCollision
                                        ? "COLLISION RISK"
                                        : "SAFE FLYBY"),
                                    context.SmallFont,
                                    shot.PredictedCollision
                                        ? bright
                                        : dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }

                            context.Graphics.DrawString(
                                "ITER " +
                                _targetSoiShooting.Iterations.ToString() +
                                "  EVAL " +
                                _targetSoiShooting.Evaluations.ToString(),
                                context.SmallFont,
                                dim,
                                x,
                                y);
                            y += transferLineHeight + transferSectionGap;
                        }

                        /*
                         * Always calculate the legacy candidate because the
                         * production CREATE KSP NODE path still depends on it.
                         * Its text is optional if vertical space is exhausted.
                         */
                        ParkingOrbitEjectionSolution ejection;

                        if (OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(
                                packet,
                                originBody,
                                solution,
                                out ejection))
                        {
                            if (ejection.BurnUniversalTimeSeconds >
                                    packet.UniversalTimeSeconds + 0.25 &&
                                !string.IsNullOrWhiteSpace(
                                    packet.VesselId))
                            {
                                _transferNodeCandidate =
                                    new ManeuverUplinkPacket
                                    {
                                        VesselId =
                                            packet.VesselId,
                                        PlanId =
                                            string.Empty,
                                        NodeUniversalTimeSeconds =
                                            ejection.BurnUniversalTimeSeconds,
                                        ProgradeDeltaVMetersPerSecond =
                                            ejection.EjectionDeltaVMetersPerSecond,
                                        NormalDeltaVMetersPerSecond =
                                            0.0,
                                        RadialDeltaVMetersPerSecond =
                                            0.0,
                                        TargetBodyName =
                                            destination.Name,
                                        Operation =
                                            "CREATE"
                                    };
                            }

                            if ((_parkingAwareLambertPreview == null ||
                                 _parkingAwareLambertPreview.Ejection == null) &&
                                y + transferLineHeight * 3 <
                                    transferContentBottom)
                            {
                                context.Graphics.DrawString(
                                    "LEGACY FALLBACK",
                                    context.SmallFont,
                                    bright,
                                    x,
                                    y);
                                y += transferLineHeight;

                                context.Graphics.DrawString(
                                    "ALT " +
                                    FormatSystemDistance(
                                        ejection.ParkingAltitudeMeters) +
                                    "  VINF " +
                                    ejection.HyperbolicExcessSpeedMetersPerSecond.ToString("0.0") +
                                    "  DV +" +
                                    ejection.EjectionDeltaVMetersPerSecond.ToString("0.0") +
                                    " m/s",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;

                                context.Graphics.DrawString(
                                    "BURN UT " +
                                    FormatMissionTime(ejection.BurnUniversalTimeSeconds) +
                                    "  " +
                                    FormatSignedMinutes(
                                        ejection.WindowOffsetSeconds) +
                                    " WINDOW",
                                    context.SmallFont,
                                    dim,
                                    x,
                                    y);
                                y += transferLineHeight;
                            }
                        }
                        else if (y + transferLineHeight * 2 <
                            transferContentBottom)
                        {
                            context.Graphics.DrawString(
                                "PARKING EJECTION REQUIRES NEAR-CIRCULAR PROGRADE ORBIT",
                                context.SmallFont,
                                bright,
                                x,
                                y);
                            y += transferLineHeight;

                            context.Graphics.DrawString(
                                "WINDOW ONLY - NO MANEUVER NODE CREATED",
                                context.SmallFont,
                                dim,
                                x,
                                y);
                        }
                    }
                    else
                    {
                        context.Graphics.DrawString(
                            "WINDOW SOLUTION NOT AVAILABLE",
                            context.SmallFont,
                            bright,
                            x,
                            y);
                        y += transferLineHeight;

                        context.Graphics.DrawString(
                            relation == "HIERARCHY CHANGE"
                                ? "REQUIRES HIERARCHY-CHANGE PLANNING"
                                : "REQUIRES VALID ELLIPTIC BODY ORBITS",
                            context.SmallFont,
                            dim,
                            x,
                            y);
                    }
                }

                DrawTransferNodeControls(
                    context,
                    plannerPanel);
            }
        }

        private void ResetLambertPreview()
        {
            _lambertPreview = null;
            _lambertEjectionPreview = null;
            _parkingAwareLambertPreview = null;
            _finiteSoiCorrection = null;
            _targetSoiShooting = null;
            _targetSoiShootingFailureText = string.Empty;
            _coupledFiniteSoiResult = null;
            _coupledFiniteSoiFailureText = string.Empty;
            _lambertPreviewAttempted = false;
            _lambertPreviewOriginName = string.Empty;
            _lambertPreviewDestinationName = string.Empty;
            _lambertPreviewMuSource = string.Empty;
            _lambertPreviewHohmannDepartureUt = double.NaN;
        }

        private void EnsureLambertPreview(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferWindowSolution hohmann)
        {
            if (packet == null || origin == null ||
                destination == null || hohmann == null)
            {
                ResetLambertPreview();
                return;
            }

            bool sameIdentity =
                _lambertPreviewAttempted &&
                string.Equals(
                    _lambertPreviewOriginName,
                    origin.Name,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    _lambertPreviewDestinationName,
                    destination.Name,
                    StringComparison.OrdinalIgnoreCase);

            bool sameAnchor =
                sameIdentity &&
                !double.IsNaN(_lambertPreviewHohmannDepartureUt) &&
                Math.Abs(
                    _lambertPreviewHohmannDepartureUt -
                    hohmann.DepartureUniversalTimeSeconds) <= 60.0;

            bool previewStillFuture =
                _lambertPreview == null ||
                _lambertPreview.DepartureUniversalTimeSeconds >
                    packet.UniversalTimeSeconds + 1.0;

            if (sameAnchor && previewStillFuture)
                return;

            _lambertPreview = null;
            _lambertEjectionPreview = null;
            _parkingAwareLambertPreview = null;
            _finiteSoiCorrection = null;
            _targetSoiShooting = null;
            _targetSoiShootingFailureText = string.Empty;
            _coupledFiniteSoiResult = null;
            _coupledFiniteSoiFailureText = string.Empty;
            _lambertPreviewAttempted = true;
            _lambertPreviewOriginName = origin.Name ?? string.Empty;
            _lambertPreviewDestinationName =
                destination.Name ?? string.Empty;
            _lambertPreviewHohmannDepartureUt =
                hohmann.DepartureUniversalTimeSeconds;
            _lambertPreviewMuSource = string.Empty;

            TransferSearchSolution preview;
            string muSource;

            if (OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                    packet,
                    origin,
                    destination,
                    hohmann,
                    out preview,
                    out muSource))
            {
                _lambertPreview = preview;
                _lambertPreviewMuSource = muSource ?? string.Empty;

                ParkingOrbitAwareTransferSolution parkingAware;
                string parkingAwareMuSource;
                if (OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                        packet,
                        origin,
                        destination,
                        hohmann,
                        out parkingAware,
                        out parkingAwareMuSource))
                {
                    _parkingAwareLambertPreview = parkingAware;
                    _lambertEjectionPreview = parkingAware.Ejection;

                    FiniteSoiDepartureCorrectionResult correction;

                    if (MapFiniteSoiCorrectionAdapter.TryOptimize(
                            packet,
                            origin,
                            parkingAware,
                            out correction))
                    {
                        _finiteSoiCorrection = correction;

                        if (correction.CorrectedEjection != null)
                            _lambertEjectionPreview =
                                correction.CorrectedEjection;
                    }

                    CoupledFiniteSoiResult coupledResult;
                    string coupledFailure;

                    if (MapCoupledFiniteSoiAdapter.TrySolve(
                            packet,
                            origin,
                            destination,
                            parkingAware.Transfer,
                            parkingAware.Ejection,
                            ResolveRequestedPeriapsisRadius(destination),
                            out coupledResult,
                            out coupledFailure))
                    {
                        _coupledFiniteSoiResult = coupledResult;
                        _coupledFiniteSoiFailureText = string.Empty;
                    }
                    else
                    {
                        _coupledFiniteSoiFailureText =
                            coupledFailure ?? string.Empty;
                    }

                    TargetSoiShootingResult shooting;
                    string shootingFailure;

                    if (MapTargetSoiShootingAdapter.TrySolve(
                            packet,
                            origin,
                            destination,
                            parkingAware,
                            _lambertEjectionPreview,
                            out shooting,
                            out shootingFailure))
                    {
                        _targetSoiShooting = shooting;
                        _targetSoiShootingFailureText = string.Empty;

                        if (shooting.CorrectedEjection != null)
                            _lambertEjectionPreview =
                                shooting.CorrectedEjection;
                    }
                    else
                    {
                        _targetSoiShootingFailureText =
                            shootingFailure ?? string.Empty;
                    }
                }
                else
                {
                    LambertParkingOrbitEjectionSolution ejectionPreview;
                    if (OrbitMapNavigationAdapter.TryCalculateLambertParkingOrbitEjectionPreview(
                            packet,
                            origin,
                            preview,
                            out ejectionPreview))
                    {
                        _lambertEjectionPreview = ejectionPreview;

                        /*
                         * General 3D bootstrap fallback.
                         *
                         * A high-inclination/eccentric destination can produce a
                         * perfectly valid Lambert + local 3D ejection while the
                         * parking-aware finite-SOI ranking rejects every coarse
                         * candidate. Do not drop back to the legacy prograde-only
                         * path in that case. Let the target-SOI shooting solver
                         * repair the full P/N/R burn directly from the valid
                         * Lambert ejection seed.
                         */
                        CoupledFiniteSoiResult directCoupledResult;
                        string directCoupledFailure;

                        if (MapCoupledFiniteSoiAdapter.TrySolve(
                                packet,
                                origin,
                                destination,
                                preview,
                                ejectionPreview,
                                ResolveRequestedPeriapsisRadius(destination),
                                out directCoupledResult,
                                out directCoupledFailure))
                        {
                            _coupledFiniteSoiResult = directCoupledResult;
                            _coupledFiniteSoiFailureText = string.Empty;
                        }
                        else
                        {
                            _coupledFiniteSoiFailureText =
                                directCoupledFailure ?? string.Empty;
                        }

                        TargetSoiShootingResult directShooting;
                        string directShootingFailure;

                        if (MapTargetSoiShootingAdapter.TrySolve(
                                packet,
                                origin,
                                destination,
                                preview,
                                ejectionPreview,
                                out directShooting,
                                out directShootingFailure))
                        {
                            _targetSoiShooting = directShooting;
                            _targetSoiShootingFailureText = string.Empty;

                            if (directShooting.CorrectedEjection != null)
                                _lambertEjectionPreview =
                                    directShooting.CorrectedEjection;
                        }
                        else
                        {
                            _targetSoiShootingFailureText =
                                directShootingFailure ?? string.Empty;
                        }
                    }
                }
            }
        }

        private void DrawTransferNodeControls(
            MissionRenderContext context,
            Rectangle plannerPanel)
        {
            int left = plannerPanel.Left + 12;
            int width = Math.Max(1, plannerPanel.Width - 24);
            int buttonHeight = 30;
            int buttonTop = plannerPanel.Bottom - 42;

            string state = _transferNodeActionText;
            string detail = string.Empty;
            bool sameSubmittedCandidate =
                IsSameAsSubmittedTransferCandidate();

            ManeuverUplinkStatusSnapshot status = null;

            if (sameSubmittedCandidate &&
                !string.IsNullOrWhiteSpace(_lastTransferPlanId))
            {
                status =
                    ManeuverUplinkStatusStore.GetForPlan(
                        _lastTransferPlanId);

                if (status != null &&
                    status.UpdatedUtc != DateTime.MinValue)
                {
                    state = status.State ?? string.Empty;
                    detail = status.Detail ?? string.Empty;
                }
            }
            else if (!string.IsNullOrWhiteSpace(_lastTransferPlanId) &&
                     HasProductionNodeCandidate())
            {
                state = "NEW SOLUTION READY";
                detail = "PREVIOUS NODE DOES NOT MATCH CURRENT CANDIDATE";
            }

            bool rejected =
                string.Equals(state, "REJECTED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "NOT SENT", StringComparison.OrdinalIgnoreCase) ||
                state.StartsWith("UPLINK FAILED", StringComparison.OrdinalIgnoreCase);

            bool candidateLocked =
                sameSubmittedCandidate &&
                !string.IsNullOrWhiteSpace(_lastTransferPlanId) &&
                !rejected;

            if (HasProductionNodeCandidate())
            {
                const int controlGap = 12;
                int singleButtonWidth =
                    Math.Min(
                        width,
                        Math.Max(
                            250,
                            MeasureButtonWidth(
                                context.Graphics,
                                context.SmallFont,
                                "CREATE KSP NODE")));

                int primaryRequiredWidth =
                    Math.Max(
                        MeasureButtonWidth(
                            context.Graphics,
                            context.SmallFont,
                            "NODE CREATED"),
                        MeasureButtonWidth(
                            context.Graphics,
                            context.SmallFont,
                            "NODE UPLINKED"));

                int secondaryRequiredWidth =
                    Math.Max(
                        MeasureButtonWidth(
                            context.Graphics,
                            context.SmallFont,
                            "REFINE KSP NODE"),
                        MeasureButtonWidth(
                            context.Graphics,
                            context.SmallFont,
                            "ENCOUNTER ACHIEVED"));

                int pairedAvailableWidth =
                    Math.Max(
                        2,
                        width - controlGap);

                int primaryButtonWidth =
                    primaryRequiredWidth;
                int secondaryButtonWidth =
                    secondaryRequiredWidth;

                if (primaryButtonWidth + secondaryButtonWidth > pairedAvailableWidth)
                {
                    double requestedTotal =
                        Math.Max(
                            1.0,
                            primaryButtonWidth + secondaryButtonWidth);

                    primaryButtonWidth =
                        Math.Max(
                            1,
                            (int)Math.Floor(
                                pairedAvailableWidth *
                                (primaryButtonWidth / requestedTotal)));

                    secondaryButtonWidth =
                        Math.Max(
                            1,
                            pairedAvailableWidth -
                            primaryButtonWidth);
                }

                Rectangle primaryButton =
                    new Rectangle(
                        left,
                        buttonTop,
                        candidateLocked ? primaryButtonWidth : singleButtonWidth,
                        buttonHeight);

                if (candidateLocked)
                {
                    _createTransferNodeButton = Rectangle.Empty;

                    string label =
                        string.Equals(state, "NODE VERIFIED", StringComparison.OrdinalIgnoreCase)
                            ? "NODE CREATED"
                            : "NODE UPLINKED";

                    DrawInactiveButton(
                        context,
                        primaryButton,
                        label);

                    bool assessmentReady =
                        status != null &&
                        status.NodeExists &&
                        status.TransferAssessmentAvailable;

                    if (assessmentReady)
                    {
                        Rectangle secondaryButton =
                            new Rectangle(
                                primaryButton.Right + controlGap,
                                buttonTop,
                                secondaryButtonWidth,
                                buttonHeight);

                        if (status.TargetEncounter)
                        {
                            _refineTransferNodeButton = Rectangle.Empty;
                            DrawInactiveButton(
                                context,
                                secondaryButton,
                                "ENCOUNTER ACHIEVED");
                        }
                        else if (_submittedTransferUsedCoupledAuthority)
                        {
                            _refineTransferNodeButton = Rectangle.Empty;
                            DrawInactiveButton(
                                context,
                                secondaryButton,
                                "COUPLED AUTHORITY - NO REFINE");
                        }
                        else if (_submittedTransferUsedLambertAuthority)
                        {
                            _refineTransferNodeButton = Rectangle.Empty;
                            DrawInactiveButton(
                                context,
                                secondaryButton,
                                "LAMBERT AUTHORITY - NO REFINE");
                        }
                        else
                        {
                            _refineTransferNodeButton = secondaryButton;
                            DrawButton(
                                context,
                                _refineTransferNodeButton,
                                "REFINE KSP NODE");
                        }
                    }
                }
                else
                {
                    _createTransferNodeButton = primaryButton;

                    DrawButton(
                        context,
                        _createTransferNodeButton,
                        "CREATE KSP NODE");
                }
            }

            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            {
                int statusFontHeight =
                    (int)Math.Ceiling(
                        context.SmallFont.GetHeight(
                            context.Graphics));
                int statusLineSpacing =
                    statusFontHeight + 6;
                const int statusButtonGap = 10;

                int detailLineCount =
                    string.IsNullOrWhiteSpace(detail)
                        ? 0
                        : 1;
                int assessmentLineCount = 0;

                if (status != null &&
                    status.TransferAssessmentAvailable)
                {
                    assessmentLineCount =
                        status.TargetEncounter
                            ? 4
                            : 5;
                }

                int totalLineCount =
                    1 +
                    detailLineCount +
                    assessmentLineCount;

                int statusBlockBottom =
                    buttonTop -
                    statusButtonGap;
                int statusBlockHeight =
                    (totalLineCount - 1) * statusLineSpacing +
                    statusFontHeight;
                int statusY =
                    statusBlockBottom -
                    statusBlockHeight;
                int nextLineY = statusY;

                using (Pen separatorPen =
                    new Pen(context.DimPhosphorColor))
                {
                    int separatorY =
                        statusY - 10;

                    context.Graphics.DrawLine(
                        separatorPen,
                        left,
                        separatorY,
                        left + width,
                        separatorY);
                }

                context.Graphics.DrawString(
                    "NODE STATUS  " + (string.IsNullOrWhiteSpace(state) ? "---" : state),
                    context.SmallFont,
                    bright,
                    left,
                    nextLineY);
                nextLineY += statusLineSpacing;

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    context.Graphics.DrawString(
                        detail,
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);
                    nextLineY += statusLineSpacing;
                }

                if (status != null &&
                    status.TransferAssessmentAvailable)
                {
                    string targetName =
                        string.IsNullOrWhiteSpace(status.TargetBodyName)
                            ? _selectedTransferBodyName
                            : status.TargetBodyName;

                    context.Graphics.DrawString(
                        "KSP TRANSFER ASSESSMENT  " + targetName,
                        context.SmallFont,
                        bright,
                        left,
                        nextLineY);
                    nextLineY += statusLineSpacing;

                    const string encounterLabel = "ENCOUNTER       ";
                    context.Graphics.DrawString(
                        encounterLabel,
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);

                    SizeF encounterLabelSize =
                        context.Graphics.MeasureString(
                            encounterLabel,
                            context.SmallFont);

                    using (SolidBrush encounterValueBrush =
                        new SolidBrush(
                            status.TargetEncounter
                                ? Color.LimeGreen
                                : Color.Red))
                    {
                        context.Graphics.DrawString(
                            status.TargetEncounter ? "YES" : "NO",
                            context.SmallFont,
                            encounterValueBrush,
                            left + encounterLabelSize.Width,
                            nextLineY);
                    }
                    nextLineY += statusLineSpacing;

                    context.Graphics.DrawString(
                        "CLOSEST APPROACH " +
                        FormatSystemDistance(status.ClosestApproachMeters) +
                        "  @ UT " +
                        FormatMissionTime(status.ClosestApproachUniversalTimeSeconds),
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);
                    nextLineY += statusLineSpacing;

                    if (_submittedTransferUsedCoupledAuthority &&
                        _coupledFiniteSoiResult != null &&
                        _coupledFiniteSoiResult.TargetAssessment != null &&
                        IsFinitePositive(
                            _coupledFiniteSoiResult.TargetAssessment.DesiredPeriapsisRadiusMeters) &&
                        IsFinitePositive(status.ClosestApproachMeters))
                    {
                        double desiredPe =
                            _coupledFiniteSoiResult.TargetAssessment.DesiredPeriapsisRadiusMeters;
                        double kspPeError =
                            Math.Abs(status.ClosestApproachMeters - desiredPe);

                        context.Graphics.DrawString(
                            "TARGET PE        " +
                            FormatSystemDistance(desiredPe) +
                            "  ERR " +
                            FormatSystemDistance(kspPeError),
                            context.SmallFont,
                            kspPeError <= Math.Max(1000.0, desiredPe * 0.002)
                                ? dim
                                : bright,
                            left,
                            nextLineY);
                    }
                    else
                    {
                        context.Graphics.DrawString(
                            "TARGET SOI       " +
                            FormatSystemDistance(status.TargetSoiRadiusMeters),
                            context.SmallFont,
                            dim,
                            left,
                            nextLineY);
                    }
                    nextLineY += statusLineSpacing;

                    if (!status.TargetEncounter &&
                        IsFinitePositive(status.ClosestApproachMeters) &&
                        IsFinitePositive(status.TargetSoiRadiusMeters))
                    {
                        double outside =
                            Math.Max(
                                0.0,
                                status.ClosestApproachMeters -
                                status.TargetSoiRadiusMeters);

                        context.Graphics.DrawString(
                            "OUTSIDE SOI      " +
                            FormatSystemDistance(outside),
                            context.SmallFont,
                            bright,
                            left,
                            nextLineY);
                    }
                }
            }
        }

        private LambertParkingOrbitEjectionSolution GetProductionCoupledEjection()
        {
            return IsCoupledProductionReady(_coupledFiniteSoiResult)
                ? _coupledFiniteSoiResult.FinalEjection
                : null;
        }

        private bool HasProductionNodeCandidate()
        {
            if (GetProductionCoupledEjection() != null)
                return true;

            // A manual periapsis is an operator constraint. Never silently
            // fall back to a path that was solved for the automatic target.
            if (IsManualTargetPeriapsis())
                return false;

            return GetProductionLambertEjection() != null ||
                _transferNodeCandidate != null;
        }

        private static bool IsCoupledProductionReady(
            CoupledFiniteSoiResult result)
        {
            if (result == null ||
                !result.FeasibilityPassSucceeded ||
                !result.BPlaneInitializationApplied ||
                !result.SourceOutbound ||
                !result.TargetInbound ||
                !string.Equals(
                    result.Stage,
                    "B-PLANE / PE SOLVED",
                    StringComparison.Ordinal) ||
                result.FinalEjection == null ||
                result.TargetAssessment == null ||
                !result.TargetAssessment.PredictedEncounter ||
                result.TargetAssessment.PredictedCollision)
            {
                return false;
            }

            LambertParkingOrbitEjectionSolution ejection =
                result.FinalEjection;

            if (!IsFinitePositive(ejection.BurnUniversalTimeSeconds) ||
                !IsFinite(ejection.ProgradeDeltaVMetersPerSecond) ||
                !IsFinite(ejection.NormalDeltaVMetersPerSecond) ||
                !IsFinite(ejection.RadialDeltaVMetersPerSecond) ||
                !IsFinitePositive(ejection.TotalDeltaVMetersPerSecond) ||
                !IsFinite(result.SourceInterfacePositionErrorMeters) ||
                !IsFinite(result.SourceInterfaceVelocityErrorMetersPerSecond) ||
                !IsFinite(result.SplitVelocityMismatchMetersPerSecond) ||
                !IsFinite(result.TargetInterfaceErrorMeters) ||
                !IsFinite(result.TargetPeriapsisErrorMeters) ||
                !IsFinite(result.TargetBPlaneMagnitudeErrorMeters))
            {
                return false;
            }

            if (result.SourceInterfacePositionErrorMeters > 1000.0 ||
                result.SourceInterfaceVelocityErrorMetersPerSecond > 5.0 ||
                result.SplitVelocityMismatchMetersPerSecond > 5.0 ||
                result.TargetInterfaceErrorMeters > 1000.0)
            {
                return false;
            }

            double desiredPe =
                result.TargetAssessment.DesiredPeriapsisRadiusMeters;
            double desiredB =
                result.TargetAssessment.DesiredBPlaneRadiusMeters;

            if (!IsFinitePositive(desiredPe) ||
                !IsFinitePositive(desiredB))
            {
                return false;
            }

            double peTolerance = Math.Max(1000.0, desiredPe * 0.002);
            double bTolerance = Math.Max(1000.0, desiredB * 0.002);

            return result.TargetPeriapsisErrorMeters <= peTolerance &&
                result.TargetBPlaneMagnitudeErrorMeters <= bTolerance;
        }

        private LambertParkingOrbitEjectionSolution GetProductionLambertEjection()
        {
            if (_targetSoiShooting != null &&
                _targetSoiShooting.CorrectedEjection != null)
            {
                return
                    _targetSoiShooting.CorrectedEjection;
            }

            if (_finiteSoiCorrection != null &&
                _finiteSoiCorrection.CorrectedEjection != null)
            {
                return
                    _finiteSoiCorrection.CorrectedEjection;
            }

            return
                _parkingAwareLambertPreview != null
                    ? _parkingAwareLambertPreview.Ejection
                    : null;
        }

        private bool IsSameAsSubmittedTransferCandidate()
        {
            if (string.IsNullOrWhiteSpace(_submittedTransferDestinationName) ||
                !string.Equals(
                    _submittedTransferDestinationName,
                    _selectedTransferBodyName,
                    StringComparison.OrdinalIgnoreCase) ||
                double.IsNaN(_submittedTransferNodeUt) ||
                double.IsNaN(_submittedTransferProgradeDv) ||
                double.IsNaN(_submittedTransferNormalDv) ||
                double.IsNaN(_submittedTransferRadialDv))
            {
                return false;
            }

            if (_submittedTransferUsedCoupledAuthority)
            {
                LambertParkingOrbitEjectionSolution ejection =
                    GetProductionCoupledEjection();

                if (ejection == null)
                    return false;

                return
                    Math.Abs(
                        ejection.BurnUniversalTimeSeconds -
                        _submittedTransferNodeUt) <= 120.0 &&
                    Math.Abs(
                        ejection.ProgradeDeltaVMetersPerSecond -
                        _submittedTransferProgradeDv) <= 5.0 &&
                    Math.Abs(
                        ejection.NormalDeltaVMetersPerSecond -
                        _submittedTransferNormalDv) <= 5.0 &&
                    Math.Abs(
                        ejection.RadialDeltaVMetersPerSecond -
                        _submittedTransferRadialDv) <= 5.0;
            }

            if (_submittedTransferUsedLambertAuthority)
            {
                LambertParkingOrbitEjectionSolution ejection =
                    GetProductionLambertEjection();

                if (ejection == null)
                    return false;

                return
                    Math.Abs(
                        ejection.BurnUniversalTimeSeconds -
                        _submittedTransferNodeUt) <= 120.0 &&
                    Math.Abs(
                        ejection.ProgradeDeltaVMetersPerSecond -
                        _submittedTransferProgradeDv) <= 5.0 &&
                    Math.Abs(
                        ejection.NormalDeltaVMetersPerSecond -
                        _submittedTransferNormalDv) <= 5.0 &&
                    Math.Abs(
                        ejection.RadialDeltaVMetersPerSecond -
                        _submittedTransferRadialDv) <= 5.0;
            }

            if (_transferNodeCandidate == null)
                return false;

            /*
             * Keep the already-created legacy node locked across tiny
             * live-planner drift while still allowing a materially changed
             * solution to expose CREATE KSP NODE again.
             */
            return
                Math.Abs(
                    _transferNodeCandidate.NodeUniversalTimeSeconds -
                    _submittedTransferNodeUt) <= 120.0 &&
                Math.Abs(
                    _transferNodeCandidate.ProgradeDeltaVMetersPerSecond -
                    _submittedTransferProgradeDv) <= 5.0 &&
                Math.Abs(_submittedTransferNormalDv) <= 5.0 &&
                Math.Abs(_submittedTransferRadialDv) <= 5.0;
        }

        private static void DrawInactiveButton(
            MissionRenderContext context,
            Rectangle r,
            string text)
        {
            using (Pen pen = new Pen(context.DimPhosphorColor))
                context.Graphics.DrawRectangle(pen, r);

            using (SolidBrush brush = new SolidBrush(context.DimPhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    r,
                    format);
            }
        }

        private static ManeuverUplinkPacket BuildProductionNodePacket(
            string vesselId,
            string destinationBodyName,
            LambertParkingOrbitEjectionSolution coupledEjection,
            LambertParkingOrbitEjectionSolution lambertEjection,
            ManeuverUplinkPacket legacyCandidate,
            string planId,
            out bool usedCoupledAuthority,
            out bool usedLambertAuthority)
        {
            usedCoupledAuthority = false;
            usedLambertAuthority = false;

            LambertParkingOrbitEjectionSolution authoritativeEjection =
                coupledEjection ?? lambertEjection;

            if (authoritativeEjection != null)
            {
                usedCoupledAuthority = coupledEjection != null;
                usedLambertAuthority = !usedCoupledAuthority;

                return
                    new ManeuverUplinkPacket
                    {
                        VesselId = vesselId ?? string.Empty,
                        PlanId = planId ?? string.Empty,
                        NodeUniversalTimeSeconds =
                            authoritativeEjection.BurnUniversalTimeSeconds,
                        ProgradeDeltaVMetersPerSecond =
                            authoritativeEjection.ProgradeDeltaVMetersPerSecond,
                        NormalDeltaVMetersPerSecond =
                            authoritativeEjection.NormalDeltaVMetersPerSecond,
                        RadialDeltaVMetersPerSecond =
                            authoritativeEjection.RadialDeltaVMetersPerSecond,
                        TargetBodyName =
                            destinationBodyName ?? string.Empty,
                        Operation = "CREATE"
                    };
            }

            if (legacyCandidate == null)
                return null;

            return
                new ManeuverUplinkPacket
                {
                    VesselId =
                        string.IsNullOrWhiteSpace(vesselId)
                            ? legacyCandidate.VesselId
                            : vesselId,
                    PlanId = planId ?? string.Empty,
                    NodeUniversalTimeSeconds =
                        legacyCandidate.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        legacyCandidate.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond = 0.0,
                    RadialDeltaVMetersPerSecond = 0.0,
                    TargetBodyName =
                        string.IsNullOrWhiteSpace(destinationBodyName)
                            ? legacyCandidate.TargetBodyName
                            : destinationBodyName,
                    Operation = "CREATE"
                };
        }

        private void UploadTransferNode()
        {
            LambertParkingOrbitEjectionSolution coupledEjection =
                GetProductionCoupledEjection();

            if (IsManualTargetPeriapsis() && coupledEjection == null)
            {
                _transferNodeActionText =
                    "CUSTOM PE REQUIRES COUPLED SOLUTION";
                return;
            }

            LambertParkingOrbitEjectionSolution lambertEjection =
                GetProductionLambertEjection();

            if (coupledEjection == null &&
                lambertEjection == null &&
                _transferNodeCandidate == null)
            {
                _transferNodeActionText =
                    "NO VALID NODE CANDIDATE";
                return;
            }

            string planId =
                "MAP-XFER-" +
                SanitizePlanToken(_selectedTransferBodyName) +
                "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            string vesselId =
                !string.IsNullOrWhiteSpace(_currentTransferVesselId)
                    ? _currentTransferVesselId
                    : (_transferNodeCandidate != null
                        ? _transferNodeCandidate.VesselId
                        : string.Empty);

            bool usedCoupledAuthority;
            bool usedLambertAuthority;

            ManeuverUplinkPacket packet =
                BuildProductionNodePacket(
                    vesselId,
                    _selectedTransferBodyName,
                    coupledEjection,
                    lambertEjection,
                    _transferNodeCandidate,
                    planId,
                    out usedCoupledAuthority,
                    out usedLambertAuthority);

            if (packet == null)
            {
                _transferNodeActionText =
                    "NO VALID NODE CANDIDATE";
                return;
            }

            if (usedCoupledAuthority &&
                _coupledFiniteSoiResult != null &&
                _coupledFiniteSoiResult.TargetAssessment != null &&
                IsFinitePositive(
                    _coupledFiniteSoiResult.TargetAssessment.DesiredPeriapsisRadiusMeters))
            {
                packet.DesiredPeriapsisRadiusMeters =
                    _coupledFiniteSoiResult.TargetAssessment.DesiredPeriapsisRadiusMeters;
            }

            string resultText;
            bool sent =
                TransferPlannerManeuverUplink.Send(
                    packet,
                    out resultText);

            _lastTransferPlanId =
                packet.PlanId;

            if (sent)
            {
                _submittedTransferDestinationName =
                    _selectedTransferBodyName ?? string.Empty;
                _submittedTransferNodeUt =
                    packet.NodeUniversalTimeSeconds;
                _submittedTransferProgradeDv =
                    packet.ProgradeDeltaVMetersPerSecond;
                _submittedTransferNormalDv =
                    packet.NormalDeltaVMetersPerSecond;
                _submittedTransferRadialDv =
                    packet.RadialDeltaVMetersPerSecond;
                _submittedTransferUsedCoupledAuthority =
                    usedCoupledAuthority;
                _submittedTransferUsedLambertAuthority =
                    usedLambertAuthority;
            }

            _transferNodeActionText =
                string.IsNullOrWhiteSpace(resultText)
                    ? (sent
                        ? (usedCoupledAuthority
                            ? "COUPLED AUTHORITY UPLINK SENT"
                            : (usedLambertAuthority
                                ? "LAMBERT AUTHORITY UPLINK SENT"
                                : "LEGACY FALLBACK UPLINK SENT"))
                        : "UPLINK FAILED")
                    : resultText;
        }

        private void RefineTransferNode()
        {
            if (_submittedTransferUsedCoupledAuthority)
            {
                _transferNodeActionText =
                    "COUPLED NODE REFINEMENT DISABLED";
                return;
            }

            if (_submittedTransferUsedLambertAuthority)
            {
                _transferNodeActionText =
                    "LAMBERT NODE REFINEMENT DISABLED";
                return;
            }

            if (string.IsNullOrWhiteSpace(_lastTransferPlanId))
            {
                _transferNodeActionText = "NO TRACKED NODE TO REFINE";
                return;
            }

            ManeuverUplinkStatusSnapshot status =
                ManeuverUplinkStatusStore.GetForPlan(
                    _lastTransferPlanId);

            if (status == null ||
                !status.NodeExists ||
                !status.TransferAssessmentAvailable)
            {
                _transferNodeActionText = "KSP ASSESSMENT NOT READY";
                return;
            }

            if (status.TargetEncounter)
            {
                _transferNodeActionText = "TARGET ENCOUNTER ALREADY ACHIEVED";
                return;
            }

            if (!IsFinitePositive(status.NodeUniversalTimeSeconds) ||
                double.IsNaN(status.ProgradeDeltaVMetersPerSecond) ||
                double.IsInfinity(status.ProgradeDeltaVMetersPerSecond))
            {
                _transferNodeActionText = "LIVE KSP NODE STATE INVALID";
                return;
            }

            ManeuverUplinkPacket packet =
                new ManeuverUplinkPacket
                {
                    VesselId =
                        _transferNodeCandidate != null
                            ? _transferNodeCandidate.VesselId
                            : string.Empty,
                    PlanId = _lastTransferPlanId,
                    NodeUniversalTimeSeconds = status.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond = status.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond = status.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond = status.RadialDeltaVMetersPerSecond,
                    TargetBodyName =
                        string.IsNullOrWhiteSpace(status.TargetBodyName)
                            ? _selectedTransferBodyName
                            : status.TargetBodyName,
                    Operation = "REFINE"
                };

            string resultText;
            bool sent =
                TransferPlannerManeuverUplink.Send(
                    packet,
                    out resultText);

            _transferNodeActionText =
                string.IsNullOrWhiteSpace(resultText)
                    ? (sent ? "REFINEMENT SENT" : "REFINEMENT FAILED")
                    : resultText;
        }

        private bool IsManualTargetPeriapsis()
        {
            return IsFinitePositive(_transferTargetPeriapsisAltitudeMeters);
        }

        private double ResolveRequestedPeriapsisRadius(OrbitMapBody destination)
        {
            if (!IsManualTargetPeriapsis() || destination == null ||
                !IsFinitePositive(destination.RadiusMeters) ||
                !IsFinitePositive(destination.SoiRadiusMeters))
                return double.NaN;

            double radius =
                destination.RadiusMeters +
                _transferTargetPeriapsisAltitudeMeters;

            return radius > destination.RadiusMeters &&
                   radius < destination.SoiRadiusMeters
                ? radius
                : double.NaN;
        }

        private static double ComputeAutoTargetPeriapsisRadius(OrbitMapBody destination)
        {
            if (destination == null ||
                !IsFinitePositive(destination.SoiRadiusMeters))
                return double.NaN;

            double desired = destination.SoiRadiusMeters * 0.01;
            if (IsFinitePositive(destination.RadiusMeters))
                desired = Math.Max(desired, destination.RadiusMeters * 2.0);

            double maximum = destination.SoiRadiusMeters * 0.25;
            desired = Math.Min(desired, maximum);

            if (IsFinitePositive(destination.RadiusMeters) &&
                desired <= destination.RadiusMeters)
            {
                double fallback = destination.RadiusMeters * 1.10;
                if (fallback >= destination.SoiRadiusMeters)
                    return double.NaN;
                desired = Math.Min(fallback, maximum);
            }

            return IsFinitePositive(desired) ? desired : double.NaN;
        }

        private bool AdjustTargetPeriapsisAltitude(double deltaMeters)
        {
            OrbitMapPacket packet;
            DateTime received;
            if (!OrbitMapSnapshotStore.TryGetLatest(out packet, out received) ||
                packet == null)
                return false;

            OrbitMapBody destination =
                FindBody(packet.Bodies, _selectedTransferBodyName);
            if (destination == null ||
                !IsFinitePositive(destination.RadiusMeters) ||
                !IsFinitePositive(destination.SoiRadiusMeters))
                return false;

            double currentAltitude;
            if (IsManualTargetPeriapsis())
            {
                currentAltitude = _transferTargetPeriapsisAltitudeMeters;
            }
            else
            {
                double automaticRadius =
                    ComputeAutoTargetPeriapsisRadius(destination);
                if (!IsFinitePositive(automaticRadius))
                    return false;
                currentAltitude =
                    automaticRadius - destination.RadiusMeters;
            }

            double minimumAltitude =
                Math.Max(1000.0, destination.RadiusMeters * 0.01);
            double maximumAltitude =
                destination.SoiRadiusMeters * 0.90 -
                destination.RadiusMeters;

            if (!IsFinitePositive(maximumAltitude) ||
                maximumAltitude <= minimumAltitude)
                return false;

            _transferTargetPeriapsisAltitudeMeters =
                Math.Max(
                    minimumAltitude,
                    Math.Min(
                        maximumAltitude,
                        currentAltitude + deltaMeters));

            InvalidateTransferSolutionForPeriapsisChange();
            return true;
        }

        private void SetTargetPeriapsisAuto()
        {
            if (!IsManualTargetPeriapsis())
                return;

            _transferTargetPeriapsisAltitudeMeters = double.NaN;
            InvalidateTransferSolutionForPeriapsisChange();
        }

        private void InvalidateTransferSolutionForPeriapsisChange()
        {
            _lastTransferPlanId = string.Empty;
            _transferNodeActionText = "NO NODE REQUEST";
            _submittedTransferDestinationName = string.Empty;
            _submittedTransferNodeUt = double.NaN;
            _submittedTransferProgradeDv = double.NaN;
            _submittedTransferNormalDv = double.NaN;
            _submittedTransferRadialDv = double.NaN;
            _submittedTransferUsedCoupledAuthority = false;
            _submittedTransferUsedLambertAuthority = false;
            ResetLambertPreview();
        }

        private void DrawTargetPeriapsisControls(
            MissionRenderContext context,
            OrbitMapBody destination,
            Rectangle plannerPanel,
            int x,
            ref int y,
            int lineHeight,
            int sectionGap)
        {
            double autoRadius = ComputeAutoTargetPeriapsisRadius(destination);
            double displayedAltitude = IsManualTargetPeriapsis()
                ? _transferTargetPeriapsisAltitudeMeters
                : (IsFinitePositive(autoRadius) && destination != null &&
                   IsFinitePositive(destination.RadiusMeters)
                    ? autoRadius - destination.RadiusMeters
                    : double.NaN);

            const int gap = 8;
            const int buttonHeight = 28;
            string[] labels =
            {
                "LOWER 100",
                "LOWER 10",
                "RAISE 10",
                "RAISE 100",
                "AUTO"
            };
            Rectangle[] buttons = new Rectangle[5];

            string text =
                "TARGET PERIAPSIS ALTITUDE  " +
                (IsFinitePositive(displayedAltitude)
                    ? (displayedAltitude / 1000.0).ToString("0.0") + " km"
                    : "---") +
                (IsManualTargetPeriapsis() ? "  MANUAL" : "  AUTO");

            using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    x,
                    y + 3);
            }

            int buttonTop = y + Math.Max(lineHeight, buttonHeight);
            int buttonLeft = x;

            for (int i = 0; i < labels.Length; i++)
            {
                int width = Math.Max(82, MeasureButtonWidth(
                    context.Graphics, context.SmallFont, labels[i]));

                if (buttonLeft + width > plannerPanel.Right - 12)
                    width = Math.Max(1, plannerPanel.Right - 12 - buttonLeft);

                buttons[i] = new Rectangle(
                    buttonLeft, buttonTop, width, buttonHeight);

                bool selected =
                    i == 4 && !IsManualTargetPeriapsis();

                DrawPeriapsisControlButton(
                    context,
                    buttons[i],
                    labels[i],
                    selected);

                buttonLeft = buttons[i].Right + gap;
            }

            _peMinus100Button = buttons[0];
            _peMinus10Button = buttons[1];
            _pePlus10Button = buttons[2];
            _pePlus100Button = buttons[3];
            _peAutoButton = buttons[4];

            y = buttonTop + buttonHeight + sectionGap;
        }

        private static void DrawPeriapsisControlButton(
            MissionRenderContext context,
            Rectangle rectangle,
            string text,
            bool selected)
        {
            Color outline = context.PhosphorColor;
            Color fill = Color.FromArgb(
                selected ? 52 : 24,
                context.PhosphorColor);

            using (SolidBrush background = new SolidBrush(fill))
                context.Graphics.FillRectangle(background, rectangle);

            using (Pen pen = new Pen(outline, selected ? 1.8f : 1.2f))
                context.Graphics.DrawRectangle(pen, rectangle);

            using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    rectangle,
                    format);
            }
        }

        private static string SanitizePlanToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "BODY";
            char[] chars = value.Trim().ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c))
                    chars[i] = '-';
            }
            return new string(chars);
        }


        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0.0;
        }

        private static string FormatSignedMinutes(double seconds)
        {
            if (!IsFinite(seconds)) return "---";
            string sign = seconds >= 0.0 ? "+" : "-";
            return sign + FormatTransferInterval(Math.Abs(seconds));
        }

        private static string FormatTransferInterval(double seconds)
        {
            if (!IsFinite(seconds) || seconds < 0.0) return "---";
            long totalSeconds = (long)Math.Floor(seconds + 0.5);
            long days = totalSeconds / 86400;
            long hours = (totalSeconds % 86400) / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long secs = totalSeconds % 60;
            if (days > 0) return string.Format("{0}d {1:00}h {2:00}m {3:00}s", days, hours, minutes, secs);
            if (hours > 0) return string.Format("{0}h {1:00}m {2:00}s", hours, minutes, secs);
            if (minutes > 0) return string.Format("{0}m {1:00}s", minutes, secs);
            return secs.ToString("0") + "s";
        }

        private void EnsureTransferSelection(OrbitMapPacket packet, string origin)
        {
            OrbitMapBody selected = FindBody(packet.Bodies, _selectedTransferBodyName);
            if (selected != null && !string.Equals(selected.Name, origin, StringComparison.OrdinalIgnoreCase)) return;

            _selectedTransferBodyName = string.Empty;
            _transferTargetPeriapsisAltitudeMeters = double.NaN;
            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];
                if (body == null || string.IsNullOrWhiteSpace(body.Name)) continue;
                if (string.Equals(body.Name, origin, StringComparison.OrdinalIgnoreCase)) continue;
                _selectedTransferBodyName = body.Name;
                break;
            }
        }

        private static OrbitMapBody FindBody(List<OrbitMapBody> bodies, string name)
        {
            if (bodies == null || string.IsNullOrWhiteSpace(name)) return null;
            for (int i = 0; i < bodies.Count; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body != null && string.Equals(body.Name, name, StringComparison.OrdinalIgnoreCase)) return body;
            }
            return null;
        }

        private static void DrawPanelFrame(MissionRenderContext context, Rectangle r, string title)
        {
            using (Pen pen = new Pen(context.DimPhosphorColor, 1.0f)) context.Graphics.DrawRectangle(pen, r);
            using (SolidBrush brush = new SolidBrush(context.PhosphorColor)) context.Graphics.DrawString(title, context.SmallFont, brush, r.Left + 8, r.Top + 8);
        }

        private static void DrawTransferBodyButton(MissionRenderContext context, Rectangle r, string text, bool selected)
        {
            Color color = selected ? context.PhosphorColor : context.DimPhosphorColor;
            using (Pen pen = new Pen(color, selected ? 1.6f : 1.0f)) context.Graphics.DrawRectangle(pen, r);
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                context.Graphics.DrawString(text, context.SmallFont, brush, r, format);
            }
        }

        private static void DrawTab(MissionRenderContext context, Rectangle r, string text, bool active)
        {
            Color color = active ? context.PhosphorColor : context.DimPhosphorColor;
            using (Pen pen = new Pen(color, active ? 1.6f : 1.0f)) context.Graphics.DrawRectangle(pen, r);
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                context.Graphics.DrawString(text, context.SmallFont, brush, r, format);
            }
        }

        private static string FormatSystemDistance(double meters)
        {
            double magnitude = Math.Abs(meters);
            if (magnitude >= 1000000000.0) return (meters / 1000000000.0).ToString("0.00") + " Gm";
            if (magnitude >= 1000000.0) return (meters / 1000000.0).ToString("0.00") + " Mm";
            return (meters / 1000.0).ToString("0.0") + " km";
        }

        private static string FormatMissionTime(double seconds)
        {
            if (!IsFinite(seconds) || seconds < 0.0) return "---";
            return FormatTransferInterval(seconds);
        }

        private static string FormatDuration(double seconds)
        {
            return FormatTransferInterval(seconds);
        }

        private static int MeasureButtonWidth(Graphics graphics, Font font, string text)
        {
            SizeF measured = graphics.MeasureString(text, font);
            return Math.Max(1, (int)Math.Ceiling(measured.Width) + (ButtonHorizontalPadding * 2));
        }

        private static void DrawButton(MissionRenderContext c, Rectangle r, string text)
        {
            using (Pen p = new Pen(c.DimPhosphorColor)) c.Graphics.DrawRectangle(p, r);
            using (SolidBrush b = new SolidBrush(c.PhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                c.Graphics.DrawString(text, c.SmallFont, b, r, format);
            }
        }

        private static double CalculateSceneRadius(OrbitMapSceneSnapshot s)
        {
            double r = MaxMagnitude(s.ActiveOrbitPoints, s.BodyRadiusMeters);
            r = Math.Max(r, MaxMagnitude(s.TargetOrbitPoints, 0));
            for (int i = 0; i < s.PatchPoints.Count; i++) r = Math.Max(r, MaxMagnitude(s.PatchPoints[i], 0));
            return r;
        }

        private static double MaxMagnitude(OrbitMapVector3[] points, double seed)
        {
            double r = seed;
            if (points == null) return r;
            for (int i = 0; i < points.Length; i++) r = Math.Max(r, points[i].Magnitude);
            return r;
        }
    }

    /// <summary>
    /// MissionControl-side bridge for finite-SOI local correction. Keeps packet
    /// translation outside KMC.Engine while the numerical optimizer remains
    /// KSP-independent.
    /// </summary>
    internal static class MapFiniteSoiCorrectionAdapter
    {
        public static bool TryOptimize(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            ParkingOrbitAwareTransferSolution parkingAware,
            out FiniteSoiDepartureCorrectionResult correction)
        {
            correction = null;

            if (packet == null ||
                origin == null ||
                parkingAware == null ||
                parkingAware.Transfer == null ||
                parkingAware.Ejection == null ||
                packet.ActiveOrbit == null)
                return false;

            if (!string.IsNullOrWhiteSpace(packet.ReferenceBodyName) &&
                !string.Equals(
                    packet.ReferenceBodyName,
                    origin.Name,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            double parentMu;

            if (!TryResolveParentMu(
                    packet,
                    origin,
                    out parentMu))
                return false;

            return
                FiniteSoiDepartureOptimizer.TryOptimize(
                    parkingAware.Transfer,
                    parkingAware.Ejection,
                    OrbitMapNavigationAdapter.ToElements(
                        packet.ActiveOrbit),
                    OrbitMapNavigationAdapter.ToBody(
                        origin),
                    parentMu,
                    out correction);
        }

        private static bool TryResolveParentMu(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            out double parentMu)
        {
            parentMu = double.NaN;

            if (packet == null ||
                origin == null ||
                string.IsNullOrWhiteSpace(origin.ParentName))
                return false;

            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];

                if (body == null)
                    continue;

                if (string.Equals(
                        body.Name,
                        origin.ParentName,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsFinitePositive(
                        body.GravParameter))
                {
                    parentMu =
                        body.GravParameter;
                    return true;
                }
            }

            if (origin.Orbit == null ||
                !IsFinitePositive(
                    origin.Orbit.SemiMajorAxisMeters) ||
                !IsFinitePositive(
                    origin.Orbit.PeriodSeconds))
                return false;

            double a =
                origin.Orbit.SemiMajorAxisMeters;

            double period =
                origin.Orbit.PeriodSeconds;

            double twoPi =
                2.0 * Math.PI;

            parentMu =
                twoPi * twoPi *
                a * a * a /
                (period * period);

            return
                IsFinitePositive(
                    parentMu);
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }


    /// <summary>
    /// Packet-to-engine bridge for the target-SOI shooting solver.
    /// </summary>
    internal static class MapTargetSoiShootingAdapter
    {
        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            ParkingOrbitAwareTransferSolution parkingAware,
            LambertParkingOrbitEjectionSolution seedEjection,
            out TargetSoiShootingResult result)
        {
            string ignoredFailure;

            return
                TrySolve(
                    packet,
                    origin,
                    destination,
                    parkingAware,
                    seedEjection,
                    out result,
                    out ignoredFailure);
        }

        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            ParkingOrbitAwareTransferSolution parkingAware,
            LambertParkingOrbitEjectionSolution seedEjection,
            out TargetSoiShootingResult result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;

            if (parkingAware == null ||
                parkingAware.Transfer == null)
            {
                failureReason = "NO PARKING-AWARE TRANSFER";
                return false;
            }

            return
                TrySolve(
                    packet,
                    origin,
                    destination,
                    parkingAware.Transfer,
                    seedEjection,
                    out result,
                    out failureReason);
        }

        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            out TargetSoiShootingResult result)
        {
            string ignoredFailure;

            return
                TrySolve(
                    packet,
                    origin,
                    destination,
                    transfer,
                    seedEjection,
                    out result,
                    out ignoredFailure);
        }

        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            out TargetSoiShootingResult result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;

            if (packet == null ||
                origin == null ||
                destination == null ||
                transfer == null ||
                seedEjection == null ||
                packet.ActiveOrbit == null ||
                destination.Orbit == null ||
                destination.SoiRadiusMeters <= 0.0)
            {
                failureReason = "MAP ADAPTER INPUT / BODY DATA";
                return false;
            }

            double parentMu;

            if (!TryResolveParentMu(
                    packet,
                    origin,
                    out parentMu))
            {
                failureReason = "PARENT MU UNAVAILABLE";
                return false;
            }

            return
                TargetSoiShootingSolver.TrySolve(
                    transfer,
                    seedEjection,
                    OrbitMapNavigationAdapter.ToElements(
                        packet.ActiveOrbit),
                    OrbitMapNavigationAdapter.ToBody(
                        origin),
                    OrbitMapNavigationAdapter.ToBody(
                        destination),
                    parentMu,
                    out result,
                    out failureReason);
        }

        private static bool TryResolveParentMu(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            out double parentMu)
        {
            parentMu = double.NaN;

            if (packet == null ||
                origin == null ||
                string.IsNullOrWhiteSpace(origin.ParentName))
                return false;

            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body =
                    packet.Bodies[i];

                if (body == null)
                    continue;

                if (string.Equals(
                        body.Name,
                        origin.ParentName,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsFinitePositive(
                        body.GravParameter))
                {
                    parentMu =
                        body.GravParameter;
                    return true;
                }
            }

            if (origin.Orbit == null ||
                !IsFinitePositive(
                    origin.Orbit.SemiMajorAxisMeters) ||
                !IsFinitePositive(
                    origin.Orbit.PeriodSeconds))
                return false;

            double a =
                origin.Orbit.SemiMajorAxisMeters;

            double period =
                origin.Orbit.PeriodSeconds;

            double twoPi =
                2.0 *
                Math.PI;

            parentMu =
                twoPi * twoPi *
                a * a * a /
                (period * period);

            return
                IsFinitePositive(
                    parentMu);
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }


    /// <summary>
    /// Packet bridge for the production coupled finite-SOI optimizer.
    /// Mission Control separately applies the maneuver-authority gate.
    /// </summary>
    internal static class MapCoupledFiniteSoiAdapter
    {
        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrapEjection,
            out CoupledFiniteSoiResult result,
            out string failureReason)
        {
            return TrySolve(
                packet, origin, destination, transfer, bootstrapEjection,
                double.NaN, out result, out failureReason);
        }

        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrapEjection,
            double desiredPeriapsisRadiusMeters,
            out CoupledFiniteSoiResult result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;

            if (packet == null || origin == null || destination == null ||
                transfer == null || bootstrapEjection == null ||
                packet.ActiveOrbit == null || destination.Orbit == null)
            {
                failureReason = "COUPLED MAP ADAPTER INPUT";
                return false;
            }

            double parentMu;
            if (!TryResolveParentMu(packet, origin, out parentMu))
            {
                failureReason = "COUPLED PARENT MU UNAVAILABLE";
                return false;
            }

            return CoupledFiniteSoiOptimizer.TrySolve(
                transfer,
                bootstrapEjection,
                OrbitMapNavigationAdapter.ToElements(packet.ActiveOrbit),
                OrbitMapNavigationAdapter.ToBody(origin),
                OrbitMapNavigationAdapter.ToBody(destination),
                parentMu,
                desiredPeriapsisRadiusMeters,
                out result,
                out failureReason);
        }

        private static bool TryResolveParentMu(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            out double parentMu)
        {
            parentMu = double.NaN;

            if (packet == null || origin == null ||
                string.IsNullOrWhiteSpace(origin.ParentName))
                return false;

            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];
                if (body == null)
                    continue;

                if (string.Equals(
                        body.Name,
                        origin.ParentName,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsFinitePositive(body.GravParameter))
                {
                    parentMu = body.GravParameter;
                    return true;
                }
            }

            if (origin.Orbit == null ||
                !IsFinitePositive(origin.Orbit.SemiMajorAxisMeters) ||
                !IsFinitePositive(origin.Orbit.PeriodSeconds))
                return false;

            double a = origin.Orbit.SemiMajorAxisMeters;
            double period = origin.Orbit.PeriodSeconds;
            double twoPi = 2.0 * Math.PI;
            parentMu = twoPi * twoPi * a * a * a / (period * period);
            return IsFinitePositive(parentMu);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0.0;
        }
    }


    /// <summary>
    /// Compatibility bridge retained for pre-production callers.
    /// New code should use MapCoupledFiniteSoiAdapter.
    /// </summary>
    internal static class MapCoupledFiniteSoiShadowAdapter
    {
        public static bool TrySolve(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrapEjection,
            out CoupledFiniteSoiShadowResult result,
            out string failureReason)
        {
            result = null;
            CoupledFiniteSoiResult productionResult;
            bool solved = MapCoupledFiniteSoiAdapter.TrySolve(
                packet,
                origin,
                destination,
                transfer,
                bootstrapEjection,
                out productionResult,
                out failureReason);

            if (!solved)
                return false;

            result = productionResult as CoupledFiniteSoiShadowResult;
            return result != null;
        }
    }

}
