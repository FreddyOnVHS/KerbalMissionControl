using System;
using System.Drawing;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Debugging.Electrical;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Power;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Pages
{
    public sealed class PowerPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private long _lastTopologyRevision =
            long.MinValue;

        private ElectricalTopologyModel _cachedModel =
            new ElectricalTopologyModel();

        public string Name
        {
            get { return "POWER"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                return new Size(
                    1600,
                    900);
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile.Standard;
            }
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

            VesselTopology topology =
                PropulsionDebugSnapshotStore
                    .GetTopology();

            if (topology == null)
            {
                _cachedModel =
                    new ElectricalTopologyModel();

                _lastTopologyRevision =
                    long.MinValue;
            }
            else if (topology.Revision !=
                     _lastTopologyRevision)
            {
                _cachedModel =
                    ElectricalTopologyBuilder.Build(
                        topology);

                _lastTopologyRevision =
                    topology.Revision;
            }

            PowerPageRenderer.Draw(
                context,
                telemetry,
                _cachedModel);
        }
    }
}
