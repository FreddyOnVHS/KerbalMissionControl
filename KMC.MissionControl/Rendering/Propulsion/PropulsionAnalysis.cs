namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Immutable result of the topology-dependent propulsion analysis.
    /// Dynamic telemetry is deliberately not stored here.
    /// </summary>
    public sealed class PropulsionAnalysis
    {
        public PropulsionAnalysis(
            PropulsionSystemModel systemModel,
            EngineClusterProjection engineCluster,
            long topologyRevision,
            int currentStage,
            string vesselName)
        {
            SystemModel =
                systemModel ??
                new PropulsionSystemModel();

            EngineCluster =
                engineCluster ??
                new EngineClusterProjection();

            TopologyRevision =
                topologyRevision;

            CurrentStage =
                currentStage;

            VesselName =
                vesselName ??
                string.Empty;
        }

        public PropulsionSystemModel SystemModel
        {
            get;
            private set;
        }

        public EngineClusterProjection EngineCluster
        {
            get;
            private set;
        }

        public long TopologyRevision
        {
            get;
            private set;
        }

        public int CurrentStage
        {
            get;
            private set;
        }

        public string VesselName
        {
            get;
            private set;
        }
    }
}
