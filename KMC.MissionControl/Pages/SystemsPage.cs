using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// KMC Build 14.18.7
    ///
    /// SYS remains a provisional spacecraft-systems home.
    ///
    /// The RCS section intentionally exposes only truth KMC already owns:
    /// - discovered ReactionControl-capable part count from the engineering
    ///   capability model;
    /// - vessel-wide monopropellant telemetry from MissionTelemetry.
    ///
    /// Build 14.18.7 adds vessel-wide KMC RCS authority truth and downstream
    /// enforcement command state. Electrical/control feed, manifold state,
    /// cluster health and axis-specific authority remain deliberately unmodeled.
    ///
    /// Existing fault-isolation content remains below the RCS foundation so
    /// potentially useful legacy behavior is not deleted during this
    /// placeholder milestone.
    /// </summary>
    public sealed class SystemsPage : IMissionPage, IMissionPageCanvasProvider
    {
        public string Name { get { return "SYS"; } }

        public Size PreferredVirtualCanvasSize
        {
            get { return Size.Empty; }
        }

        public MissionPageContentProfile ContentProfile
        {
            get { return MissionPageContentProfile.DenseEngineering; }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            AnalysisPipelineResult result;
            EngineeringSnapshotStore.TryGetLatest(
                out result);

            SpacecraftSystemsModel systems =
                result != null &&
                result.Snapshot != null
                    ? result.Snapshot.SpacecraftSystems
                    : null;

            CapabilityModel capabilities =
                result != null &&
                result.Snapshot != null
                    ? result.Snapshot.Capabilities
                    : null;

            Graphics g = context.Graphics;
            Rectangle b = context.ContentBounds;

            using (Pen p =
                new Pen(
                    context.DimPhosphorColor,
                    1f))
            using (Brush bright =
                new SolidBrush(
                    context.PhosphorColor))
            using (Brush dim =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                DrawHeader(
                    g,
                    bright,
                    dim,
                    context,
                    b,
                    systems);

                int rcsTop =
                    b.Top + 78;

                int rcsHeight =
                    Math.Max(
                        245,
                        (int)(b.Height * 0.38));

                Rectangle rcsBox =
                    new Rectangle(
                        b.Left + 18,
                        rcsTop,
                        b.Width - 36,
                        rcsHeight);

                string vesselId =
                    result != null &&
                    result.Snapshot != null &&
                    result.Snapshot.Vessel != null
                        ? result.Snapshot.Vessel.VesselId
                        : string.Empty;

                DrawRcsFoundation(
                    g,
                    p,
                    bright,
                    dim,
                    context,
                    rcsBox,
                    telemetry,
                    capabilities,
                    vesselId);

                int isolationTop =
                    rcsBox.Bottom + 12;

                Rectangle isolationBox =
                    new Rectangle(
                        b.Left + 18,
                        isolationTop,
                        b.Width - 36,
                        Math.Max(
                            155,
                            b.Bottom -
                            isolationTop -
                            36));

                DrawLegacyIsolation(
                    g,
                    p,
                    bright,
                    dim,
                    context,
                    isolationBox,
                    systems);

                g.DrawString(
                    "RCS FOUNDATION IS PROVISIONAL. LOCATION / DETAIL MODEL WILL BE REVISITED.",
                    context.SmallFont,
                    dim,
                    b.Left + 18,
                    b.Bottom - 27);
            }
        }

        private static void DrawHeader(
            Graphics g,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle bounds,
            SpacecraftSystemsModel systems)
        {
            g.DrawString(
                "SPACECRAFT SYSTEMS / RCS FOUNDATION",
                context.LargeFont,
                bright,
                bounds.Left + 18,
                bounds.Top + 14);

            string vessel =
                systems != null
                    ? systems.VesselName
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(
                vessel))
            {
                vessel =
                    "NO ENGINEERING SNAPSHOT";
            }

            g.DrawString(
                "VESSEL  " + vessel +
                "     BUILD 14.18.7 RCS AUTHORITY",
                context.SmallFont,
                dim,
                bounds.Left + 18,
                bounds.Top + 50);
        }

        private static void DrawRcsFoundation(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            MissionTelemetry telemetry,
            CapabilityModel capabilities,
            string vesselId)
        {
            g.DrawRectangle(
                p,
                box);

            g.DrawString(
                "REACTION CONTROL SYSTEM",
                context.LargeFont,
                bright,
                box.Left + 12,
                box.Top + 10);

            int rcsPartCount =
                capabilities != null
                    ? capabilities.GetPartCount(
                        VesselCapabilityType.ReactionControl)
                    : 0;

            bool capabilityAvailable =
                capabilities != null;

            string installed =
                capabilityAvailable
                    ? (rcsPartCount > 0
                        ? "DETECTED"
                        : "NOT DETECTED")
                    : "UNKNOWN";

            double monoAmount =
                telemetry != null
                    ? Math.Max(
                        0.0,
                        telemetry.TotalMonopropellantAmount)
                    : 0.0;

            double monoCapacity =
                telemetry != null
                    ? Math.Max(
                        0.0,
                        telemetry.TotalMonopropellantCapacity)
                    : 0.0;

            string propellant =
                telemetry != null &&
                monoCapacity > 0.0
                    ? monoAmount.ToString("0.0") +
                      " / " +
                      monoCapacity.ToString("0.0") +
                      "  (" +
                      ((monoAmount /
                        monoCapacity) *
                       100.0)
                        .ToString("0.0") +
                      "%)"
                    : "UNAVAILABLE";

            int innerTop =
                box.Top + 48;

            int gap = 10;
            int halfWidth =
                (box.Width -
                 34 -
                 gap) /
                2;

            Rectangle left =
                new Rectangle(
                    box.Left + 12,
                    innerTop,
                    halfWidth,
                    box.Height - 60);

            Rectangle right =
                new Rectangle(
                    left.Right + gap,
                    innerTop,
                    halfWidth,
                    box.Height - 60);

            DrawRcsStatusColumn(
                g,
                p,
                bright,
                dim,
                context,
                left,
                installed,
                rcsPartCount,
                propellant);

            DrawRcsAuthorityColumn(
                g,
                p,
                bright,
                dim,
                context,
                right,
                vesselId,
                rcsPartCount);
        }

        private static void DrawRcsStatusColumn(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            string installed,
            int rcsPartCount,
            string propellant)
        {
            g.DrawRectangle(
                p,
                box);

            g.DrawString(
                "SYSTEM / HARDWARE",
                context.SmallFont,
                dim,
                box.Left + 10,
                box.Top + 8);

            int y =
                box.Top + 32;

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "RCS HARDWARE",
                installed,
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "RCS PARTS",
                rcsPartCount.ToString(),
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "MONOPROPELLANT",
                propellant,
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "CONTROL POWER",
                "NOT YET MODELED",
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "FEED / MANIFOLD",
                "NOT YET MODELED",
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "THRUSTER GROUPS",
                "NOT YET MODELED",
                box.Left + 10,
                box.Right - 10,
                ref y);
        }

        private static void DrawRcsAuthorityColumn(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            string vesselId,
            int rcsPartCount)
        {
            g.DrawRectangle(
                p,
                box);

            g.DrawString(
                "CONTROL AUTHORITY",
                context.SmallFont,
                dim,
                box.Left + 10,
                box.Top + 8);

            RcsAuthoritySnapshot authority =
                RcsAuthorityStore.GetSnapshot(
                    vesselId);

            string master;

            if (rcsPartCount <= 0)
            {
                master =
                    "NOT INSTALLED";
            }
            else if (!authority.Known)
            {
                master =
                    "UNKNOWN";
            }
            else
            {
                master =
                    authority.AuthorityAvailable
                        ? "AVAILABLE"
                        : "UNAVAILABLE";
            }

            string cause =
                rcsPartCount <= 0
                    ? "NO RCS HARDWARE"
                    : authority.Detail;

            string enforcement =
                rcsPartCount <= 0
                    ? "N/A"
                    : authority.AuthorityAvailable
                        ? "RELEASE COMMANDED"
                        : "INHIBIT COMMANDED";

            int y =
                box.Top + 32;

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "MASTER RCS",
                master,
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "AUTHORITY CAUSE",
                cause,
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "PITCH / YAW / ROLL",
                "GLOBAL ONLY",
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "TRANSLATION X/Y/Z",
                "GLOBAL ONLY",
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "KSP AUTHORITY",
                enforcement,
                box.Left + 10,
                box.Right - 10,
                ref y);

            DrawStatusRow(
                g,
                bright,
                dim,
                context,
                "AXIS / CLUSTERS",
                "NOT YET MODELED",
                box.Left + 10,
                box.Right - 10,
                ref y);
        }

        private static void DrawStatusRow(
            Graphics g,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            string label,
            string value,
            int left,
            int right,
            ref int y)
        {
            g.DrawString(
                label,
                context.SmallFont,
                dim,
                left,
                y);

            SizeF size =
                g.MeasureString(
                    value ?? string.Empty,
                    context.SmallFont);

            float valueX =
                Math.Max(
                    left + 150,
                    right - size.Width);

            g.DrawString(
                value ?? string.Empty,
                context.SmallFont,
                bright,
                valueX,
                y);

            y += 27;
        }

        private static void DrawLegacyIsolation(
            Graphics g,
            Pen p,
            Brush bright,
            Brush dim,
            MissionRenderContext context,
            Rectangle box,
            SpacecraftSystemsModel systems)
        {
            g.DrawRectangle(
                p,
                box);

            g.DrawString(
                "EXISTING SYS FAULT ISOLATION / LEGACY CONTENT",
                context.LargeFont,
                bright,
                box.Left + 12,
                box.Top + 10);

            if (systems == null)
            {
                g.DrawString(
                    "NO ENGINEERING SYSTEMS SNAPSHOT",
                    context.SmallFont,
                    dim,
                    box.Left + 12,
                    box.Top + 44);

                return;
            }

            FailureSimulationSnapshot failures =
                systems.FailureSimulation;

            FaultIsolationSnapshot isolation =
                FaultIsolationAnalyzer.Build(
                    systems);

            g.DrawString(
                "FAILURE MODE  " +
                (failures != null
                    ? failures.Mode
                        .ToString()
                        .ToUpperInvariant()
                    : "UNKNOWN") +
                "     ACTIVE FAILURES  " +
                (failures != null
                    ? failures.ActiveFailureCount
                        .ToString()
                    : "0") +
                "     ISOLATION CASES  " +
                isolation.ActiveCaseCount
                    .ToString(),
                context.SmallFont,
                dim,
                box.Left + 12,
                box.Top + 43);

            if (isolation.Cases.Count == 0)
            {
                g.DrawString(
                    "NO ACTIVE FAULT-ISOLATION CASE.",
                    context.SmallFont,
                    dim,
                    box.Left + 12,
                    box.Top + 72);

                return;
            }

            FaultIsolationCase primary =
                isolation.Cases[0];

            int y =
                box.Top + 72;

            DrawLine(
                g,
                bright,
                context,
                "PRIMARY  " +
                primary.Severity
                    .ToString()
                    .ToUpperInvariant() +
                " / " +
                primary.Subsystem +
                " / " +
                primary.Condition,
                box.Left + 12,
                ref y);

            DrawLine(
                g,
                dim,
                context,
                "ISOLATE  " +
                primary.Isolation,
                box.Left + 12,
                ref y);

            DrawLine(
                g,
                bright,
                context,
                "ACTION   " +
                primary.ImmediateAction,
                box.Left + 12,
                ref y);

            DrawLine(
                g,
                dim,
                context,
                "VERIFY   " +
                primary.Verification,
                box.Left + 12,
                ref y);
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

            y += 27;
        }
    }
}
