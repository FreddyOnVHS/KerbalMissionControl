using System;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Maintains a cached topology snapshot and rebuilds it only when the
    /// active vessel structure or staging state appears to have changed.
    /// </summary>
    internal sealed class VesselTopologyService
    {
        private readonly VesselTopologyBuilder _builder =
            new VesselTopologyBuilder();

        private string _vesselId =
            string.Empty;

        private int _partCount =
            -1;

        private uint _rootPartId;

        private int _currentStage =
            -1;

        private long _revision;

        private VesselTopology _current =
            new VesselTopology();

        public VesselTopology Current
        {
            get { return _current; }
        }

        public bool Update(
            Vessel vessel)
        {
            if (vessel == null)
            {
                return false;
            }

            string vesselId =
                vessel.id.ToString();

            int partCount =
                vessel.parts != null
                    ? vessel.parts.Count
                    : 0;

            uint rootPartId =
                vessel.rootPart != null
                    ? vessel.rootPart.flightID
                    : 0U;

            int currentStage =
                vessel.currentStage;

            bool changed =
                !string.Equals(
                    vesselId,
                    _vesselId,
                    StringComparison.Ordinal) ||
                partCount !=
                    _partCount ||
                rootPartId !=
                    _rootPartId ||
                currentStage !=
                    _currentStage;

            if (!changed)
            {
                return false;
            }

            _vesselId =
                vesselId;

            _partCount =
                partCount;

            _rootPartId =
                rootPartId;

            _currentStage =
                currentStage;

            _revision++;

            _current =
                _builder.Build(
                    vessel,
                    _revision);

            return true;
        }

        public void ForceRebuild(
            Vessel vessel)
        {
            _partCount =
                -1;

            Update(
                vessel);
        }

        public void Reset()
        {
            _vesselId =
                string.Empty;

            _partCount =
                -1;

            _rootPartId =
                0U;

            _currentStage =
                -1;

            _revision =
                0;

            _current =
                new VesselTopology();
        }
    }
}
