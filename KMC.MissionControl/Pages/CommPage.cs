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

            SpacecraftSystemsModel systems =
                result != null &&
                result.Snapshot != null
                    ? result.Snapshot.SpacecraftSystems
                    : null;

            Graphics g = context.Graphics;
            Rectangle b = context.ContentBounds;

            using (Pen p = new Pen(context.DimPhosphorColor, 1f))
            using (Brush bright = new SolidBrush(context.PhosphorColor))
            using (Brush dim = new SolidBrush(context.DimPhosphorColor))
            {
                g.DrawString(
                    "COMMUNICATIONS / REDUNDANCY STATUS",
                    context.LargeFont,
                    bright,
                    b.Left + 18,
                    b.Top + 14);

                g.DrawString(
                    "KMC SYNTHETIC SYSTEM STATE — STOCK KSP RF RANGE/SIGNAL IS NOT CLAIMED",
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Top + 48);

                if (systems == null)
                {
                    g.DrawString(
                        "NO ENGINEERING SYSTEMS SNAPSHOT",
                        context.LargeFont,
                        bright,
                        b.Left + 18,
                        b.Top + 105);
                    return;
                }

                SpacecraftSystemComponent a =
                    systems.FindComponent("COMM_A");

                SpacecraftSystemComponent c =
                    systems.FindComponent("COMM_B");

                Rectangle left =
                    new Rectangle(
                        b.Left + 18,
                        b.Top + 90,
                        (b.Width - 54) / 2,
                        150);

                Rectangle right =
                    new Rectangle(
                        left.Right + 18,
                        b.Top + 90,
                        left.Width,
                        150);

                DrawChannel(
                    g, p, bright, dim,
                    context, left, "COMM A", a);

                DrawChannel(
                    g, p, bright, dim,
                    context, right, "COMM B", c);

                string overall =
                    ResolveOverall(a, c);

                g.DrawString(
                    "OVERALL COMM CAPABILITY",
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Top + 270);

                g.DrawString(
                    overall,
                    context.LargeFont,
                    bright,
                    b.Left + 18,
                    b.Top + 300);

                FailureSimulationSnapshot failures =
                    systems.FailureSimulation;

                int active =
                    failures != null
                        ? failures.ActiveFailureCount
                        : 0;

                g.DrawString(
                    "FAILURE MODE  " +
                    (failures != null
                        ? failures.Mode.ToString().ToUpperInvariant()
                        : "UNKNOWN") +
                    "     ACTIVE FAILURES  " +
                    active.ToString(),
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Top + 350);

                FaultIsolationSnapshot isolation =
                    FaultIsolationAnalyzer.Build(systems);

                FaultIsolationCase commCase =
                    FindCommCase(isolation);

                Rectangle procedure =
                    new Rectangle(
                        b.Left + 18,
                        b.Top + 395,
                        b.Width - 36,
                        Math.Max(
                            190,
                            b.Bottom -
                            (b.Top + 395) -
                            60));

                DrawProcedure(
                    g,
                    p,
                    bright,
                    dim,
                    context,
                    procedure,
                    commCase);

                g.DrawString(
                    "OPERATOR NOTE: COMM A/B electrical controls remain crew reconfiguration, not repair.",
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Bottom - 40);
            }
        }

        private static void DrawChannel(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            string title,
            SpacecraftSystemComponent component)
        {
            g.DrawRectangle(p, box);

            g.DrawString(
                title,
                context.LargeFont,
                bright,
                box.Left + 14,
                box.Top + 12);

            string state =
                component != null
                    ? component.State.ToString().ToUpperInvariant()
                    : "UNAVAILABLE";

            string health =
                component != null
                    ? component.Health.ToString().ToUpperInvariant()
                    : "UNKNOWN";

            g.DrawString(
                "STATE   " + state,
                context.LargeFont,
                bright,
                box.Left + 14,
                box.Top + 58);

            g.DrawString(
                "HEALTH  " + health,
                context.SmallFont,
                dim,
                box.Left + 14,
                box.Top + 102);
        }

        private static void DrawProcedure(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            FaultIsolationCase item)
        {
            g.DrawRectangle(p, box);

            g.DrawString(
                "COMM FAULT ISOLATION / PROCEDURE",
                context.LargeFont,
                bright,
                box.Left + 12,
                box.Top + 10);

            if (item == null)
            {
                g.DrawString(
                    "NOMINAL — NO COMM CORRECTIVE ACTION REQUIRED",
                    context.LargeFont,
                    bright,
                    box.Left + 12,
                    box.Top + 58);

                g.DrawString(
                    "VERIFY BOTH CHANNELS REMAIN ONLINE AND MONITOR BUS STATUS.",
                    context.SmallFont,
                    dim,
                    box.Left + 12,
                    box.Top + 96);

                return;
            }

            int y = box.Top + 52;

            DrawLine(
                g, bright, context,
                "STATE    " +
                item.Severity.ToString().ToUpperInvariant() +
                " / " +
                item.Condition,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "ISOLATE  " +
                item.Isolation,
                box.Left + 12,
                ref y);

            DrawLine(
                g, bright, context,
                "ACTION   " +
                item.ImmediateAction,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "VERIFY   " +
                item.Verification,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "OBJECTIVE " +
                item.RecoveryObjective,
                box.Left + 12,
                ref y);
        }

        private static FaultIsolationCase FindCommCase(
            FaultIsolationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            for (int index = 0;
                 index < snapshot.Cases.Count;
                 index++)
            {
                FaultIsolationCase item =
                    snapshot.Cases[index];

                if (item != null &&
                    string.Equals(
                        item.Subsystem,
                        "COMM",
                        StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static string ResolveOverall(
            SpacecraftSystemComponent a,
            SpacecraftSystemComponent b)
        {
            bool aGood = IsUsable(a);
            bool bGood = IsUsable(b);

            if (aGood && bGood)
            {
                return
                    "NOMINAL — A/B AVAILABLE";
            }

            if (aGood || bGood)
            {
                return
                    "DEGRADED — SINGLE CHANNEL AVAILABLE";
            }

            return
                "COMMUNICATION CAPABILITY LOST / SYNTHETIC";
        }

        private static bool IsUsable(
            SpacecraftSystemComponent c)
        {
            return
                c != null &&
                (c.State ==
                     SpacecraftSystemState.Online ||
                 c.State ==
                     SpacecraftSystemState.Degraded);
        }

        private static void DrawLine(
            Graphics g,
            Brush brush,
            MissionRenderContext context,
            string text,
            int x,
            ref int y)
        {
            g.DrawString(
                text ?? string.Empty,
                context.SmallFont,
                brush,
                x,
                y);

            y += 30;
        }
    }
}
