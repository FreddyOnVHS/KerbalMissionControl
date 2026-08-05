using System.Collections.Generic;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Operator-facing propulsion system summary derived from the complete
    /// part graph. It intentionally hides individual plumbing parts.
    /// </summary>
    public sealed class PropulsionSystemModel
    {
        public PropulsionSystemModel()
        {
            VesselName = string.Empty;
            EngineGroups = new List<PropulsionEngineGroup>();
            SeparationStages = new List<int>();
            LiquidEnginePartIds = new List<uint>();
        }

        public string VesselName { get; set; }

        public long Revision { get; set; }

        public int CurrentStage { get; set; }

        public int CommandCount { get; set; }

        public int PayloadCount { get; set; }

        public int RcsThrusterCount { get; set; }

        public int BatteryCount { get; set; }

        public int PowerSourceCount { get; set; }

        public int DockingPortCount { get; set; }

        /// <summary>
        /// Number of engines whose propellant requirements contain both
        /// LiquidFuel and Oxidizer.
        /// </summary>
        public int LiquidEngineCount { get; set; }

        /// <summary>
        /// Stable KSP PartIds for engines that consume both LiquidFuel and
        /// Oxidizer. Runtime rendering uses these IDs to determine actual
        /// liquid-system operating state without being affected by SRBs.
        /// </summary>
        public List<uint> LiquidEnginePartIds { get; private set; }

        public bool HasLiquidFuel { get; set; }

        public bool HasOxidizer { get; set; }

        public bool HasMonopropellant { get; set; }

        public bool HasSolidFuel { get; set; }

        public List<PropulsionEngineGroup>
            EngineGroups { get; private set; }

        public List<int>
            SeparationStages { get; private set; }

        public int TotalEngineCount
        {
            get
            {
                int total = 0;

                for (int index = 0;
                     index < EngineGroups.Count;
                     index++)
                {
                    total +=
                        EngineGroups[index].Count;
                }

                return total;
            }
        }
    }
}
