using System.Collections.Generic;

namespace KMC.Plugin.Simulation
{
    internal sealed class SimulatedEngine
    {
        public SimulatedEngine()
        {
            Propellants =
                new List<SimulatedPropellant>();
        }

        public uint PartPersistentId { get; set; }

        public string PartName { get; set; }

        public int ActivationStage { get; set; }

        public double SeaLevelThrustKilonewtons
        {
            get;
            set;
        }

        public double VacuumThrustKilonewtons
        {
            get;
            set;
        }

        public double SeaLevelSpecificImpulse
        {
            get;
            set;
        }

        public double VacuumSpecificImpulse
        {
            get;
            set;
        }

        public bool ThrottleLocked { get; set; }

        public IList<SimulatedPropellant> Propellants
        {
            get;
            private set;
        }
    }
}
