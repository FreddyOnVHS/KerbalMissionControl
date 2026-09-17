using System;
using System.Collections.Generic;
using System.Drawing;
using KMC.MissionControl.Telemetry;
using KMC.Shared;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public sealed class OrbitMapRenderer
    {
        public void Draw(MissionRenderContext context, Rectangle viewport, OrbitMapSceneSnapshot scene, OrbitMapCamera camera, OrbitMapFreshness freshness)
        {
            Graphics g = context.Graphics;
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(7, 15, 20))) g.FillRectangle(bg, viewport);
            using (Pen frame = new Pen(context.DimPhosphorColor, 1.0f)) g.DrawRectangle(frame, viewport);

            if (freshness == OrbitMapFreshness.Unavailable || scene == null)
            {
                DrawCentered(context, viewport, "ORBIT DATA UNAVAILABLE", context.PhosphorColor);
                DrawPanels(context, viewport, null, freshness);
                return;
            }

            Color activeColor = freshness == OrbitMapFreshness.Stale ? context.DimPhosphorColor : context.PhosphorColor;
            Color targetColor = Color.FromArgb(freshness == OrbitMapFreshness.Stale ? 110 : 190, 150, 220, 255);
            Color patchColor = Color.FromArgb(freshness == OrbitMapFreshness.Stale ? 100 : 210, 255, 190, 90);

            DrawBody(context, viewport, scene, camera, activeColor);
            using (Pen targetPen = new Pen(targetColor, 1.2f)) DrawPolyline(g, viewport, scene.TargetOrbitPoints, camera, targetPen, scene.BodyRadiusMeters);
            using (Pen activePen = new Pen(activeColor, 2.0f)) DrawPolyline(g, viewport, scene.ActiveOrbitPoints, camera, activePen, scene.BodyRadiusMeters);
            using (Pen patchPen = new Pen(patchColor, 1.6f))
                for (int i = 0; i < scene.PatchPoints.Count; i++) DrawPolyline(g, viewport, scene.PatchPoints[i], camera, patchPen, scene.BodyRadiusMeters);

            DrawMarker(context, viewport, camera, scene.VesselPosition, "VSL", activeColor, 5, scene.BodyRadiusMeters);
            DrawMarker(context, viewport, camera, scene.ApoapsisPosition, "AP", activeColor, 4, scene.BodyRadiusMeters);
            DrawMarker(context, viewport, camera, scene.PeriapsisPosition, "PE", activeColor, 4, scene.BodyRadiusMeters);
            if (scene.Target != null && scene.Target.Present) DrawMarker(context, viewport, camera, scene.TargetPosition, "TGT", targetColor, 5, scene.BodyRadiusMeters);
            for (int i = 0; i < scene.ManeuverNodes.Count; i++)
            {
                OrbitMapManeuverNode n = scene.ManeuverNodes[i];
                DrawMarker(context, viewport, camera, new OrbitMapVector3(n.PositionX, n.PositionY, n.PositionZ), "MNV" + (i + 1).ToString(), patchColor, 5, scene.BodyRadiusMeters);
            }

            DrawViewIndicator(context, viewport, camera);
            if (freshness == OrbitMapFreshness.Stale) DrawStatus(context, viewport, "ORBIT DATA STALE", Color.Orange);
            DrawPanels(context, viewport, scene, freshness);
        }

        private static void DrawBody(MissionRenderContext context, Rectangle viewport, OrbitMapSceneSnapshot scene, OrbitMapCamera camera, Color color)
        {
            PointF center; double depth;
            if (!camera.TryProject(new OrbitMapVector3(0, 0, 0), viewport, out center, out depth)) return;
            double focal = (viewport.Height * 0.5) / Math.Tan(45.0 * Math.PI / 360.0);
            float radius = (float)Math.Max(4.0, scene.BodyRadiusMeters * focal / depth);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(70, color))) context.Graphics.FillEllipse(fill, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (Pen pen = new Pen(color, 1.4f)) context.Graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (SolidBrush text = new SolidBrush(color)) context.Graphics.DrawString(scene.BodyName ?? string.Empty, context.SmallFont, text, center.X + radius + 5, center.Y - 8);
        }

        private static void DrawPolyline(Graphics g, Rectangle viewport, OrbitMapVector3[] points, OrbitMapCamera camera, Pen pen, double bodyRadiusMeters)
        {
            if (points == null || points.Length < 2) return;

            for (int i = 1; i < points.Length; i++)
            {
                OrbitMapVector3 a = points[i - 1];
                OrbitMapVector3 b = points[i];
                OrbitMapVector3 midpoint = new OrbitMapVector3(
                    (a.X + b.X) * 0.5,
                    (a.Y + b.Y) * 0.5,
                    (a.Z + b.Z) * 0.5);

                if (camera.IsOccludedBySphere(midpoint, bodyRadiusMeters)) continue;

                PointF pa, pb;
                double da, db;
                if (!camera.TryProject(a, viewport, out pa, out da)) continue;
                if (!camera.TryProject(b, viewport, out pb, out db)) continue;
                g.DrawLine(pen, pa, pb);
            }
        }

        private static void DrawMarker(MissionRenderContext context, Rectangle viewport, OrbitMapCamera camera, OrbitMapVector3 world, string label, Color color, int radius, double bodyRadiusMeters)
        {
            if (camera.IsOccludedBySphere(world, bodyRadiusMeters)) return;
            PointF p; double depth; if (!camera.TryProject(world, viewport, out p, out depth)) return;
            using (Pen pen = new Pen(color, 1.5f)) context.Graphics.DrawEllipse(pen, p.X - radius, p.Y - radius, radius * 2, radius * 2);
            using (SolidBrush brush = new SolidBrush(color)) context.Graphics.DrawString(label, context.SmallFont, brush, p.X + radius + 3, p.Y - 8);
        }

        private static void DrawViewIndicator(MissionRenderContext context, Rectangle viewport, OrbitMapCamera camera)
        {
            int left = viewport.Left + 12;
            int top = viewport.Top + 30;
            string text = string.Format("VIEW AZ {0:000}  EL {1:+00;-00;00}", camera.YawDegrees, camera.PitchDegrees);
            using (SolidBrush brush = new SolidBrush(context.DimPhosphorColor))
                context.Graphics.DrawString(text, context.SmallFont, brush, left, top);

            PointF origin = new PointF(left + 24, top + 42);
            DrawAxis(context, camera.ProjectDirection(new OrbitMapVector3(1, 0, 0)), origin, "X");
            DrawAxis(context, camera.ProjectDirection(new OrbitMapVector3(0, 1, 0)), origin, "Y");
            DrawAxis(context, camera.ProjectDirection(new OrbitMapVector3(0, 0, 1)), origin, "N");
        }

        private static void DrawAxis(MissionRenderContext context, PointF direction, PointF origin, string label)
        {
            if (Math.Abs(direction.X) < 1e-6f && Math.Abs(direction.Y) < 1e-6f) return;
            float length = 22.0f;
            PointF end = new PointF(origin.X + direction.X * length, origin.Y + direction.Y * length);
            using (Pen pen = new Pen(context.DimPhosphorColor, 1.0f))
                context.Graphics.DrawLine(pen, origin, end);
            using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
                context.Graphics.DrawString(label, context.SmallFont, brush, end.X + 2, end.Y - 6);
        }

        private static void DrawCentered(MissionRenderContext context, Rectangle viewport, string text, Color color)
        {
            SizeF size = context.Graphics.MeasureString(text, context.LargeFont);
            using (SolidBrush brush = new SolidBrush(color)) context.Graphics.DrawString(text, context.LargeFont, brush, viewport.Left + (viewport.Width - size.Width) / 2.0f, viewport.Top + (viewport.Height - size.Height) / 2.0f);
        }

        private static void DrawStatus(MissionRenderContext context, Rectangle viewport, string text, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color)) context.Graphics.DrawString(text, context.SmallFont, brush, viewport.Left + 10, viewport.Top + 8);
        }

        private static void DrawPanels(MissionRenderContext context, Rectangle viewport, OrbitMapSceneSnapshot scene, OrbitMapFreshness freshness)
        {
            Rectangle bounds = context.ContentBounds;
            int top = viewport.Bottom + 8;
            int h = Math.Max(80, bounds.Bottom - top);
            int w = bounds.Width / 4;
            DrawPanel(context, new Rectangle(bounds.Left, top, w - 4, h), "CURRENT ORBIT", BuildOrbitText(scene, freshness));
            DrawPanel(context, new Rectangle(bounds.Left + w, top, w - 4, h), "TARGET", BuildTargetText(scene, freshness));
            DrawPanel(context, new Rectangle(bounds.Left + w * 2, top, w - 4, h), "MANEUVER", BuildManeuverText(scene, freshness));
            DrawPanel(context, new Rectangle(bounds.Left + w * 3, top, bounds.Right - (bounds.Left + w * 3), h), "ENCOUNTER", BuildEncounterText(scene, freshness));
        }

        private static void DrawPanel(MissionRenderContext context, Rectangle r, string title, string body)
        {
            using (Pen pen = new Pen(context.DimPhosphorColor, 1.0f)) context.Graphics.DrawRectangle(pen, r);
            using (SolidBrush titleBrush = new SolidBrush(context.PhosphorColor)) context.Graphics.DrawString(title, context.SmallFont, titleBrush, r.Left + 6, r.Top + 5);
            using (SolidBrush bodyBrush = new SolidBrush(context.DimPhosphorColor)) context.Graphics.DrawString(body, context.SmallFont, bodyBrush, r.Left + 6, r.Top + 30);
        }

        private static string BuildOrbitText(OrbitMapSceneSnapshot s, OrbitMapFreshness f)
        {
            if (s == null || f == OrbitMapFreshness.Unavailable || s.ActiveOrbit == null) return "UNAVAILABLE";
            return string.Format("{0}\nAP {1}\nPE {2}\nINC {3:0.00} deg", s.VesselName, D(s.ActiveOrbit.ApoapsisMeters), D(s.ActiveOrbit.PeriapsisMeters), s.ActiveOrbit.InclinationDegrees);
        }
        private static string BuildTargetText(OrbitMapSceneSnapshot s, OrbitMapFreshness f)
        {
            if (s == null || f == OrbitMapFreshness.Unavailable) return "UNAVAILABLE";
            if (s.Target == null || !s.Target.Present) return "NONE";
            return (s.Target.TargetName ?? "TARGET") + "\n" + (s.Target.TargetType ?? string.Empty);
        }
        private static string BuildManeuverText(OrbitMapSceneSnapshot s, OrbitMapFreshness f)
        {
            if (s == null || f == OrbitMapFreshness.Unavailable) return "UNAVAILABLE";
            if (s.ManeuverNodes.Count == 0) return "NONE";
            OrbitMapManeuverNode n = s.ManeuverNodes[0];
            return string.Format("T-NODE {0:0}s\nDV {1:0.0} m/s\nP {2:0.0} N {3:0.0} R {4:0.0}", n.UniversalTimeSeconds - s.UniversalTimeSeconds, n.TotalDeltaVMetersPerSecond, n.ProgradeDeltaVMetersPerSecond, n.NormalDeltaVMetersPerSecond, n.RadialDeltaVMetersPerSecond);
        }
        private static string BuildEncounterText(OrbitMapSceneSnapshot s, OrbitMapFreshness f)
        {
            if (s == null || f == OrbitMapFreshness.Unavailable) return "UNAVAILABLE";
            for (int i = 0; i < s.Patches.Count; i++) if (!string.IsNullOrWhiteSpace(s.Patches[i].NextBodyName)) return "SOI: " + s.Patches[i].NextBodyName + "\n" + s.Patches[i].TransitionType;
            return "NO ENCOUNTER";
        }
        private static string D(double meters) { double a = Math.Abs(meters); return a >= 1000000 ? (meters / 1000000.0).ToString("0.00") + " Mm" : (meters / 1000.0).ToString("0.0") + " km"; }
    }
}
