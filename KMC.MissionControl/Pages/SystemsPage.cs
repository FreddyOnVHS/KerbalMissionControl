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
                    "SPACECRAFT SYSTEMS / FAULT ISOLATION",
                    context.LargeFont,
                    bright,
                    b.Left + 18,
                    b.Top + 14);

                if (systems == null)
                {
                    g.DrawString(
                        "NO ENGINEERING SYSTEMS SNAPSHOT",
                        context.LargeFont,
                        bright,
                        b.Left + 18,
                        b.Top + 80);
                    return;
                }

                FailureSimulationSnapshot failures =
                    systems.FailureSimulation;

                FaultIsolationSnapshot isolation =
                    FaultIsolationAnalyzer.Build(systems);

                g.DrawString(
                    "VESSEL  " +
                    (systems.VesselName ?? string.Empty) +
                    "     FAILURE MODE  " +
                    (failures != null
                        ? failures.Mode.ToString().ToUpperInvariant()
                        : "UNKNOWN") +
                    "     ACTIVE FAILURES  " +
                    (failures != null
                        ? failures.ActiveFailureCount.ToString()
                        : "0") +
                    "     ISOLATION CASES  " +
                    isolation.ActiveCaseCount.ToString(),
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Top + 50);

                string[] ids = new[] {
                    "BUS_MAIN_A","BUS_MAIN_B","BUS_ESS",
                    "GUID_A","GUID_B","FLIGHT_COMPUTER",
                    "COMM_A","COMM_B","PUMP_A","PUMP_B" };

                int gap = 10;
                int top = b.Top + 82;
                int componentAreaHeight =
                    Math.Max(
                        250,
                        (int)(b.Height * 0.48));

                int rowH =
                    componentAreaHeight / 5;

                int width =
                    (b.Width - 54) / 2;

                for (int i = 0; i < ids.Length; i++)
                {
                    int col = i % 2;
                    int row = i / 2;

                    Rectangle r =
                        new Rectangle(
                            b.Left + 18 +
                            col * (width + gap),
                            top + row * rowH,
                            width,
                            rowH - 6);

                    DrawComponent(
                        g,
                        p,
                        bright,
                        dim,
                        context,
                        r,
                        systems.FindComponent(ids[i]),
                        ids[i]);
                }

                int procedureTop =
                    top + componentAreaHeight + 8;

                Rectangle procedure =
                    new Rectangle(
                        b.Left + 18,
                        procedureTop,
                        b.Width - 36,
                        Math.Max(
                            170,
                            b.Bottom -
                            procedureTop -
                            38));

                DrawIsolation(
                    g,
                    p,
                    bright,
                    dim,
                    context,
                    procedure,
                    isolation);

                g.DrawString(
                    "PROCEDURES ARE ADVISORY. CREW RECONFIGURATION DOES NOT CLEAR FAILURE TRUTH.",
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Bottom - 28);
            }
        }

        private static void DrawComponent(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle r,
            SpacecraftSystemComponent component,
            string fallbackId)
        {
            g.DrawRectangle(p, r);

            string name =
                component != null &&
                !string.IsNullOrWhiteSpace(component.DisplayName)
                    ? component.DisplayName
                    : fallbackId;

            string state =
                component != null
                    ? component.State.ToString().ToUpperInvariant()
                    : "UNAVAILABLE";

            string health =
                component != null
                    ? component.Health.ToString().ToUpperInvariant()
                    : "UNKNOWN";

            g.DrawString(
                name,
                context.SmallFont,
                dim,
                r.Left + 8,
                r.Top + 5);

            g.DrawString(
                state,
                context.LargeFont,
                bright,
                r.Left + 8,
                r.Top + 22);

            g.DrawString(
                "HEALTH " + health,
                context.SmallFont,
                dim,
                Math.Max(
                    r.Left + 8,
                    r.Right - 175),
                r.Top + 7);
        }

        private static void DrawIsolation(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            FaultIsolationSnapshot snapshot)
        {
            g.DrawRectangle(p, box);

            g.DrawString(
                "FAULT ISOLATION / CREW PROCEDURE",
                context.LargeFont,
                bright,
                box.Left + 12,
                box.Top + 10);

            g.DrawString(
                snapshot != null
                    ? snapshot.Summary
                    : "UNAVAILABLE",
                context.SmallFont,
                dim,
                box.Left + 12,
                box.Top + 42);

            if (snapshot == null ||
                snapshot.Cases.Count == 0)
            {
                g.DrawString(
                    "MONITOR SYSTEMS. NO CREW CORRECTIVE ACTION REQUIRED.",
                    context.LargeFont,
                    bright,
                    box.Left + 12,
                    box.Top + 78);
                return;
            }

            FaultIsolationCase primary =
                snapshot.Cases[0];

            int y = box.Top + 72;

            DrawLine(
                g, bright, context,
                "PRIMARY  " +
                primary.Severity.ToString().ToUpperInvariant() +
                " / " +
                primary.Subsystem +
                " / " +
                primary.Condition,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "ISOLATE  " +
                primary.Isolation,
                box.Left + 12,
                ref y);

            DrawLine(
                g, bright, context,
                "ACTION   " +
                primary.ImmediateAction,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "VERIFY   " +
                primary.Verification,
                box.Left + 12,
                ref y);

            DrawLine(
                g, dim, context,
                "OBJECTIVE " +
                primary.RecoveryObjective,
                box.Left + 12,
                ref y);

            if (snapshot.Cases.Count > 1)
            {
                string additional =
                    "ADDITIONAL CASES  ";

                int max =
                    Math.Min(
                        3,
                        snapshot.Cases.Count);

                for (int index = 1;
                     index < max;
                     index++)
                {
                    if (index > 1)
                    {
                        additional += "  |  ";
                    }

                    additional +=
                        snapshot.Cases[index].Subsystem +
                        ": " +
                        snapshot.Cases[index].Condition;
                }

                DrawLine(
                    g, dim, context,
                    additional,
                    box.Left + 12,
                    ref y);
            }
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

            y += 28;
        }
    }
}
