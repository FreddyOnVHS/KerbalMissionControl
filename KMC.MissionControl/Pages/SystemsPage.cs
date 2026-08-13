using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class SystemsPage : IMissionPage, IMissionPageCanvasProvider
    {
        public string Name { get { return "SYS"; } }
        public Size PreferredVirtualCanvasSize { get { return Size.Empty; } }
        public MissionPageContentProfile ContentProfile
        { get { return MissionPageContentProfile.DenseEngineering; } }

        public void Draw(MissionRenderContext context, MissionTelemetry telemetry)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            AnalysisPipelineResult result;
            EngineeringSnapshotStore.TryGetLatest(out result);
            SpacecraftSystemsModel systems = result != null && result.Snapshot != null
                ? result.Snapshot.SpacecraftSystems : null;

            Graphics g = context.Graphics;
            Rectangle b = context.ContentBounds;
            using (Pen p = new Pen(context.DimPhosphorColor, 1f))
            using (Brush bright = new SolidBrush(context.PhosphorColor))
            using (Brush dim = new SolidBrush(context.DimPhosphorColor))
            {
                g.DrawString("SPACECRAFT SYSTEMS OVERVIEW", context.LargeFont, bright,
                    b.Left + 18, b.Top + 14);

                if (systems == null)
                {
                    g.DrawString("NO ENGINEERING SYSTEMS SNAPSHOT", context.LargeFont, bright,
                        b.Left + 18, b.Top + 80);
                    return;
                }

                FailureSimulationSnapshot failures = systems.FailureSimulation;
                g.DrawString("VESSEL  " + (systems.VesselName ?? string.Empty) +
                    "     FAILURE MODE  " + (failures != null ? failures.Mode.ToString().ToUpperInvariant() : "UNKNOWN") +
                    "     ACTIVE  " + (failures != null ? failures.ActiveFailureCount.ToString() : "0"),
                    context.SmallFont, dim, b.Left + 18, b.Top + 50);

                string[] ids = new[] {
                    "BUS_MAIN_A","BUS_MAIN_B","BUS_ESS",
                    "GUID_A","GUID_B","FLIGHT_COMPUTER",
                    "COMM_A","COMM_B","PUMP_A","PUMP_B" };

                int columns = 2;
                int gap = 14;
                int top = b.Top + 86;
                int rowH = Math.Max(54, (b.Height - 150) / 5);
                int width = (b.Width - 54) / columns;

                for (int i = 0; i < ids.Length; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    Rectangle r = new Rectangle(
                        b.Left + 18 + col * (width + gap),
                        top + row * rowH,
                        width,
                        rowH - 8);
                    DrawComponent(g, p, bright, dim, context, r,
                        systems.FindComponent(ids[i]), ids[i]);
                }

                g.DrawString("STATE IS ENGINE-OWNED SYNTHETIC TRUTH; LIVE KSP EFFECTS ARE SHOWN ONLY WHERE EXPLICITLY INTEGRATED.",
                    context.SmallFont, dim, b.Left + 18, b.Bottom - 42);
            }
        }

        private static void DrawComponent(Graphics g, Pen p, Brush bright,
            Brush dim, MissionRenderContext context, Rectangle r,
            SpacecraftSystemComponent component, string fallbackId)
        {
            g.DrawRectangle(p, r);
            string name = component != null && !string.IsNullOrWhiteSpace(component.DisplayName)
                ? component.DisplayName : fallbackId;
            string state = component != null ? component.State.ToString().ToUpperInvariant() : "UNAVAILABLE";
            string health = component != null ? component.Health.ToString().ToUpperInvariant() : "UNKNOWN";
            g.DrawString(name, context.SmallFont, dim, r.Left + 10, r.Top + 7);
            g.DrawString(state, context.LargeFont, bright, r.Left + 10, r.Top + 25);
            g.DrawString("HEALTH " + health, context.SmallFont, dim,
                Math.Max(r.Left + 10, r.Right - 190), r.Top + 9);
        }
    }
}
