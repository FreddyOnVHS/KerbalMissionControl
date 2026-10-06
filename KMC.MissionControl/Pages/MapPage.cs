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
        private const int ButtonGap = 16;
        private const int ButtonHorizontalPadding = 14;
        private const int ButtonHeight = 24;
        private bool _dragging;
        private PointF _lastPointer;
        private bool _initialFitDone;
        private int _subpage;
        private string _selectedTransferBodyName = string.Empty;
        private ManeuverUplinkPacket _transferNodeCandidate;
        private string _lastTransferPlanId = string.Empty;
        private string _transferNodeActionText = "NO NODE REQUEST";
        private string _submittedTransferDestinationName = string.Empty;
        private double _submittedTransferNodeUt = double.NaN;
        private double _submittedTransferProgradeDv = double.NaN;
        private TransferSearchSolution _lambertPreview;
        private LambertParkingOrbitEjectionSolution _lambertEjectionPreview;
        private ParkingOrbitAwareTransferSolution _parkingAwareLambertPreview;
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
                if (_transferNodeCandidate != null &&
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

                for (int i = 0; i < _transferBodyButtons.Count && i < _transferBodyNames.Count; i++)
                {
                    if (_transferBodyButtons[i].Contains(q))
                    {
                        _selectedTransferBodyName = _transferBodyNames[i];
                        _lastTransferPlanId = string.Empty;
                        _transferNodeActionText = "NO NODE REQUEST";
                        _submittedTransferDestinationName = string.Empty;
                        _submittedTransferNodeUt = double.NaN;
                        _submittedTransferProgradeDv = double.NaN;
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
            _createTransferNodeButton = Rectangle.Empty;
            _refineTransferNodeButton = Rectangle.Empty;

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
            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString("ORIGIN", context.SmallFont, dim, x, y);
                context.Graphics.DrawString(string.IsNullOrWhiteSpace(origin) ? "---" : origin, context.LargeFont, bright, x, y + 18);
                y += 60;
                context.Graphics.DrawString("DESTINATION", context.SmallFont, dim, x, y);
                context.Graphics.DrawString(destination != null ? destination.Name : "SELECT BODY", context.LargeFont, bright, x, y + 18);
                y += 64;

                if (destination != null)
                {
                    context.Graphics.DrawString("PARENT  " + (string.IsNullOrWhiteSpace(destination.ParentName) ? "---" : destination.ParentName), context.SmallFont, dim, x, y);
                    y += 20;
                    if (destination.Orbit != null)
                    {
                        context.Graphics.DrawString("SMA     " + FormatSystemDistance(destination.Orbit.SemiMajorAxisMeters), context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("ECC     " + destination.Orbit.Eccentricity.ToString("0.0000"), context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("INC     " + destination.Orbit.InclinationDegrees.ToString("0.00") + " deg", context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("PERIOD  " + FormatDuration(destination.Orbit.PeriodSeconds), context.SmallFont, dim, x, y);
                        y += 28;
                    }

                    string relation = originBody != null &&
                                      string.Equals(originBody.ParentName, destination.ParentName, StringComparison.OrdinalIgnoreCase)
                        ? "SAME PARENT"
                        : "HIERARCHY CHANGE";
                    context.Graphics.DrawString("ROUTE CLASS  " + relation, context.SmallFont, dim, x, y);
                    y += 30;

                    TransferWindowSolution solution;
                    if (OrbitMapNavigationAdapter.TryCalculateTransferWindow(originBody, destination, packet.UniversalTimeSeconds, out solution))
                    {
                        context.Graphics.DrawString("HOHMANN WINDOW  CIRCULAR / COPLANAR APPROX", context.SmallFont, bright, x, y);
                        y += 22;
                        context.Graphics.DrawString("PARENT         " + solution.ParentName, context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("CURRENT PHASE  " + solution.CurrentPhaseDegrees.ToString("0.00") + " deg", context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("REQ PHASE      " + solution.RequiredPhaseDegrees.ToString("0.00") + " deg", context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("WINDOW IN      " + FormatTransferInterval(solution.WaitSeconds), context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("DEPARTURE UT   " + solution.DepartureUniversalTimeSeconds.ToString("0"), context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("TRANSFER TIME  " + FormatTransferInterval(solution.TransferTimeSeconds), context.SmallFont, dim, x, y);
                        y += 20;
                        context.Graphics.DrawString("PARENT DV      " + solution.ParentFrameDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") + " m/s", context.SmallFont, dim, x, y);
                        y += 26;

                        EnsureLambertPreview(packet, originBody, destination, solution);

                        context.Graphics.DrawString(
                            "LAMBERT PREVIEW  COARSE 9x9 / NO NODE AUTHORITY",
                            context.SmallFont, bright, x, y);
                        y += 22;

                        if (_lambertPreview != null)
                        {
                            context.Graphics.DrawString(
                                "PATH           " + _lambertPreview.Path.ToString().ToUpperInvariant(),
                                context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString(
                                "DEPARTURE UT   " + _lambertPreview.DepartureUniversalTimeSeconds.ToString("0"),
                                context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString(
                                "FLIGHT TIME    " + FormatTransferInterval(_lambertPreview.TimeOfFlightSeconds),
                                context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString(
                                "DEP VINF       " + _lambertPreview.DepartureExcessSpeedMetersPerSecond.ToString("0.0") + " m/s",
                                context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString(
                                "ARR VINF       " + _lambertPreview.ArrivalExcessSpeedMetersPerSecond.ToString("0.0") + " m/s",
                                context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString(
                                "SCORE          " + _lambertPreview.CombinedExcessSpeedMetersPerSecond.ToString("0.0") +
                                " m/s  MU " + _lambertPreviewMuSource,
                                context.SmallFont, dim, x, y);
                            y += 24;

                            context.Graphics.DrawString(
                                "PARKING-AWARE LAMBERT / NO NODE AUTHORITY",
                                context.SmallFont, bright, x, y);
                            y += 18;

                            if (_lambertEjectionPreview != null)
                            {
                                if (_parkingAwareLambertPreview != null)
                                {
                                    context.Graphics.DrawString(
                                        "SELECT " + _parkingAwareLambertPreview.Transfer.Path.ToString().ToUpperInvariant() +
                                        "  DEP UT " + _parkingAwareLambertPreview.Transfer.DepartureUniversalTimeSeconds.ToString("0") +
                                        "  ARR VINF " + _parkingAwareLambertPreview.ArrivalExcessSpeedMetersPerSecond.ToString("0.0"),
                                        context.SmallFont, dim, x, y);
                                    y += 18;
                                }

                                context.Graphics.DrawString(
                                    "BURN UT " + _lambertEjectionPreview.BurnUniversalTimeSeconds.ToString("0") +
                                    "  OFF " + FormatSignedMinutes(_lambertEjectionPreview.WindowOffsetSeconds),
                                    context.SmallFont, dim, x, y);
                                y += 18;
                                context.Graphics.DrawString(
                                    "DV P " + _lambertEjectionPreview.ProgradeDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                    "  N " + _lambertEjectionPreview.NormalDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") +
                                    "  R " + _lambertEjectionPreview.RadialDeltaVMetersPerSecond.ToString("+0.0;-0.0;0.0") + " m/s",
                                    context.SmallFont, dim, x, y);
                                y += 18;
                                context.Graphics.DrawString(
                                    "EJECT SCORE " + _lambertEjectionPreview.TotalDeltaVMetersPerSecond.ToString("0.0") +
                                    " m/s  GEOM RES " + _lambertEjectionPreview.GeometryResidualDegrees.ToString("0.0000") + " deg",
                                    context.SmallFont, dim, x, y);
                                y += 24;
                            }
                            else
                            {
                                context.Graphics.DrawString(
                                    "EJECTION PREVIEW UNAVAILABLE FOR CURRENT PARKING GEOMETRY",
                                    context.SmallFont, dim, x, y);
                                y += 24;
                            }
                        }
                        else
                        {
                            context.Graphics.DrawString(
                                "NO SUPPORTED SAME-PARENT LAMBERT SAMPLE",
                                context.SmallFont, dim, x, y);
                            y += 26;
                        }

                        ParkingOrbitEjectionSolution ejection;
                        if (OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, originBody, solution, out ejection))
                        {
                            context.Graphics.DrawString("PARKING EJECTION  CIRCULAR / PROGRADE APPROX", context.SmallFont, bright, x, y);
                            y += 22;
                            context.Graphics.DrawString("PARK ALT       " + FormatSystemDistance(ejection.ParkingAltitudeMeters), context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("VINF           " + ejection.HyperbolicExcessSpeedMetersPerSecond.ToString("0.0") + " m/s  " + ejection.ExcessDirection, context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("PARK SPEED     " + ejection.ParkingSpeedMetersPerSecond.ToString("0.0") + " m/s", context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("EJECT SPEED    " + ejection.HyperbolicPeriapsisSpeedMetersPerSecond.ToString("0.0") + " m/s", context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("VESSEL DV      +" + ejection.EjectionDeltaVMetersPerSecond.ToString("0.0") + " m/s", context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("BURN RADIUS    " + ejection.AsymptoteAngleDegrees.ToString("0.00") + " deg BEHIND VINF", context.SmallFont, dim, x, y);
                            y += 20;
                            context.Graphics.DrawString("BURN UT        " + ejection.BurnUniversalTimeSeconds.ToString("0") + "  (" + FormatSignedMinutes(ejection.WindowOffsetSeconds) + " WINDOW)", context.SmallFont, dim, x, y);
                            y += 26;
                            if (ejection.BurnUniversalTimeSeconds > packet.UniversalTimeSeconds + 0.25 &&
                                !string.IsNullOrWhiteSpace(packet.VesselId))
                            {
                                _transferNodeCandidate =
                                    new ManeuverUplinkPacket
                                    {
                                        VesselId = packet.VesselId,
                                        PlanId = string.Empty,
                                        NodeUniversalTimeSeconds = ejection.BurnUniversalTimeSeconds,
                                        ProgradeDeltaVMetersPerSecond = ejection.EjectionDeltaVMetersPerSecond,
                                        NormalDeltaVMetersPerSecond = 0.0,
                                        RadialDeltaVMetersPerSecond = 0.0,
                                        TargetBodyName = destination.Name,
                                        Operation = "CREATE"
                                    };
                            }

                            context.Graphics.DrawString("NODE CANDIDATE READY - KSP WILL BE AUTHORITATIVE", context.SmallFont, bright, x, y);
                        }
                        else
                        {
                            context.Graphics.DrawString("PARKING EJECTION REQUIRES NEAR-CIRCULAR PROGRADE ORBIT", context.SmallFont, bright, x, y);
                            y += 22;
                            context.Graphics.DrawString("WINDOW ONLY - NO MANEUVER NODE CREATED", context.SmallFont, dim, x, y);
                        }
                    }
                    else
                    {
                        context.Graphics.DrawString("WINDOW SOLUTION NOT AVAILABLE IN 14.22.28", context.SmallFont, bright, x, y);
                        y += 22;
                        context.Graphics.DrawString(
                            relation == "HIERARCHY CHANGE"
                                ? "REQUIRES HIERARCHY-CHANGE PLANNING"
                                : "REQUIRES VALID ELLIPTIC BODY ORBITS",
                            context.SmallFont, dim, x, y);
                    }
                }

                DrawTransferNodeControls(context, plannerPanel);
            }
        }

        private void ResetLambertPreview()
        {
            _lambertPreview = null;
            _lambertEjectionPreview = null;
            _parkingAwareLambertPreview = null;
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
                     _transferNodeCandidate != null)
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

            if (_transferNodeCandidate != null)
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

                    context.Graphics.DrawString(
                        "ENCOUNTER       " +
                        (status.TargetEncounter ? "YES" : "NO"),
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);
                    nextLineY += statusLineSpacing;

                    context.Graphics.DrawString(
                        "CLOSEST APPROACH " +
                        FormatSystemDistance(status.ClosestApproachMeters) +
                        "  @ UT " +
                        status.ClosestApproachUniversalTimeSeconds.ToString("0"),
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);
                    nextLineY += statusLineSpacing;

                    context.Graphics.DrawString(
                        "TARGET SOI       " +
                        FormatSystemDistance(status.TargetSoiRadiusMeters),
                        context.SmallFont,
                        dim,
                        left,
                        nextLineY);
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

        private bool IsSameAsSubmittedTransferCandidate()
        {
            if (_transferNodeCandidate == null ||
                string.IsNullOrWhiteSpace(_submittedTransferDestinationName) ||
                !string.Equals(
                    _submittedTransferDestinationName,
                    _selectedTransferBodyName,
                    StringComparison.OrdinalIgnoreCase) ||
                double.IsNaN(_submittedTransferNodeUt) ||
                double.IsNaN(_submittedTransferProgradeDv))
            {
                return false;
            }

            /*
             * Keep the already-created node locked across tiny live-planner
             * drift while still allowing a materially changed solution to
             * expose CREATE KSP NODE again.
             */
            return
                Math.Abs(
                    _transferNodeCandidate.NodeUniversalTimeSeconds -
                    _submittedTransferNodeUt) <= 120.0 &&
                Math.Abs(
                    _transferNodeCandidate.ProgradeDeltaVMetersPerSecond -
                    _submittedTransferProgradeDv) <= 5.0;
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

        private void UploadTransferNode()
        {
            if (_transferNodeCandidate == null)
            {
                _transferNodeActionText = "NO VALID NODE CANDIDATE";
                return;
            }

            ManeuverUplinkPacket packet =
                new ManeuverUplinkPacket
                {
                    VesselId = _transferNodeCandidate.VesselId,
                    PlanId =
                        "MAP-XFER-" +
                        SanitizePlanToken(_selectedTransferBodyName) +
                        "-" +
                        Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                    NodeUniversalTimeSeconds =
                        _transferNodeCandidate.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        _transferNodeCandidate.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond = 0.0,
                    RadialDeltaVMetersPerSecond = 0.0,
                    TargetBodyName =
                        _transferNodeCandidate.TargetBodyName,
                    Operation = "CREATE"
                };

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
            }

            _transferNodeActionText =
                string.IsNullOrWhiteSpace(resultText)
                    ? (sent ? "UPLINK SENT" : "UPLINK FAILED")
                    : resultText;
        }

        private void RefineTransferNode()
        {
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


        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }

        private static string FormatSignedMinutes(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "---";
            string sign = seconds >= 0.0 ? "+" : "-";
            long totalSeconds = (long)Math.Floor(Math.Abs(seconds) + 0.5);
            long minutes = totalSeconds / 60;
            long secs = totalSeconds % 60;
            return string.Format("{0}{1}m {2:00}s", sign, minutes, secs);
        }

        private static string FormatTransferInterval(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0) return "---";
            long totalMinutes = (long)Math.Floor((seconds / 60.0) + 0.5);
            long hours = totalMinutes / 60;
            long minutes = totalMinutes % 60;
            return string.Format("{0}h {1:00}m", hours, minutes);
        }

        private void EnsureTransferSelection(OrbitMapPacket packet, string origin)
        {
            OrbitMapBody selected = FindBody(packet.Bodies, _selectedTransferBodyName);
            if (selected != null && !string.Equals(selected.Name, origin, StringComparison.OrdinalIgnoreCase)) return;

            _selectedTransferBodyName = string.Empty;
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

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0.0) return "---";
            double days = seconds / 86400.0;
            return days >= 1.0 ? days.ToString("0.00") + " d" : (seconds / 3600.0).ToString("0.00") + " h";
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
}
