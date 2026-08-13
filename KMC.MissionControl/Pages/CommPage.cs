using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class CommPage : IMissionPage, IMissionPageCanvasProvider
    {
        public string Name { get { return "COMM"; } }
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
                g.DrawString("COMMUNICATIONS / REDUNDANCY STATUS", context.LargeFont, bright,
                    b.Left + 18, b.Top + 14);
                g.DrawString("KMC SYNTHETIC SYSTEM STATE — STOCK KSP RF RANGE/SIGNAL IS NOT CLAIMED",
                    context.SmallFont, dim, b.Left + 18, b.Top + 48);

                if (systems == null)
                {
                    g.DrawString("NO ENGINEERING SYSTEMS SNAPSHOT", context.LargeFont, bright,
                        b.Left + 18, b.Top + 105);
                    return;
                }

                SpacecraftSystemComponent a = systems.FindComponent("COMM_A");
                SpacecraftSystemComponent c = systems.FindComponent("COMM_B");
                Rectangle left = new Rectangle(b.Left + 18, b.Top + 90, (b.Width - 54)/2, 170);
                Rectangle right = new Rectangle(left.Right + 18, b.Top + 90, left.Width, 170);
                DrawChannel(g, p, bright, dim, context, left, "COMM A", a);
                DrawChannel(g, p, bright, dim, context, right, "COMM B", c);

                string overall = ResolveOverall(a, c);
                g.DrawString("OVERALL COMM CAPABILITY", context.SmallFont, dim,
                    b.Left + 18, b.Top + 300);
                g.DrawString(overall, context.LargeFont, bright,
                    b.Left + 18, b.Top + 330);

                FailureSimulationSnapshot failures = systems.FailureSimulation;
                int active = failures != null ? failures.ActiveFailureCount : 0;
                g.DrawString("FAILURE MODE  " + (failures != null ? failures.Mode.ToString().ToUpperInvariant() : "UNKNOWN") +
                    "     ACTIVE FAILURES  " + active.ToString(), context.SmallFont, dim,
                    b.Left + 18, b.Top + 390);
                g.DrawString("OPERATOR NOTE: COMM A/B electrical controls remain crew reconfiguration, not repair.",
                    context.SmallFont, dim, b.Left + 18, b.Bottom - 55);
            }
        }

        private static void DrawChannel(Graphics g, Pen p, Brush bright, Brush dim,
            MissionRenderContext context, Rectangle box, string title,
            SpacecraftSystemComponent component)
        {
            g.DrawRectangle(p, box);
            g.DrawString(title, context.LargeFont, bright, box.Left + 14, box.Top + 12);
            string state = component != null ? component.State.ToString().ToUpperInvariant() : "UNAVAILABLE";
            string health = component != null ? component.Health.ToString().ToUpperInvariant() : "UNKNOWN";
            g.DrawString("STATE   " + state, context.LargeFont, bright, box.Left + 14, box.Top + 62);
            g.DrawString("HEALTH  " + health, context.SmallFont, dim, box.Left + 14, box.Top + 108);
        }

        private static string ResolveOverall(SpacecraftSystemComponent a,
            SpacecraftSystemComponent b)
        {
            bool aGood = IsUsable(a);
            bool bGood = IsUsable(b);
            if (aGood && bGood) return "NOMINAL — A/B AVAILABLE";
            if (aGood || bGood) return "DEGRADED — SINGLE CHANNEL AVAILABLE";
            return "COMMUNICATION CAPABILITY LOST / SYNTHETIC";
        }

        private static bool IsUsable(SpacecraftSystemComponent c)
        {
            return c != null &&
                (c.State == SpacecraftSystemState.Online ||
                 c.State == SpacecraftSystemState.Degraded);
        }
    }
}
