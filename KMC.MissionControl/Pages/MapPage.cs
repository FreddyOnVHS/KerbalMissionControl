using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.OrbitMap;
using KMC.MissionControl.Telemetry;
using KMC.Shared;

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
        private const int ButtonGap = 16;
        private const int ButtonHorizontalPadding = 14;
        private const int ButtonHeight = 24;
        private bool _dragging;
        private PointF _lastPointer;
        private bool _initialFitDone;
        private int _subpage;
        private string _selectedTransferBodyName = string.Empty;

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
                for (int i = 0; i < _transferBodyButtons.Count && i < _transferBodyNames.Count; i++)
                {
                    if (_transferBodyButtons[i].Contains(q))
                    {
                        _selectedTransferBodyName = _transferBodyNames[i];
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
                    y += 34;
                }

                context.Graphics.DrawString("CALCULATION NOT ENABLED IN 14.22.27", context.SmallFont, bright, x, y);
                y += 22;
                context.Graphics.DrawString("FOUNDATION ONLY: BODY CATALOG + DESTINATION SELECTION", context.SmallFont, dim, x, y);
                y += 20;
                context.Graphics.DrawString("NEXT: TRANSFER WINDOW / MANEUVER SOLUTION", context.SmallFont, dim, x, y);
            }
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
