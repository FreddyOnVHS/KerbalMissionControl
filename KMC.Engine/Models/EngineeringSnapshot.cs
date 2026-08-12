using System;
using System.Collections.Generic;
using KMC.Engine.Ascent;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;
using KMC.Engine.Guidance;
using KMC.Engine.SpacecraftSystems;

namespace KMC.Engine.Models
{
    public sealed class EngineeringSnapshot
    {
        public EngineeringSnapshot(
            long sequence,
            DateTime generatedUtc,
            VesselModel vessel,
            CapabilityModel capabilities,
            PowerModel power,
            PropulsionModel propulsion,
            IReadOnlyList<string> diagnostics)
        {
            Sequence = sequence;
            GeneratedUtc = generatedUtc;
            Vessel = vessel;
            Capabilities = capabilities;
            Power = power;
            Propulsion = propulsion;
            Ascent = new AscentModel();
            Orbit = new OrbitModel();
            ManeuverPlan = new ManeuverPlanModel();
            Guidance = new GuidanceSolutionModel();
            SpacecraftSystems = new SpacecraftSystemsModel();
            Diagnostics = diagnostics;
        }

        public long Sequence { get; private set; }
        public DateTime GeneratedUtc { get; private set; }
        public VesselModel Vessel { get; private set; }
        public CapabilityModel Capabilities { get; private set; }
        public PowerModel Power { get; private set; }
        public PropulsionModel Propulsion { get; private set; }

        /// <summary>
        /// Engine-owned ASCENT state from the same flight-analysis cycle as
        /// vessel, power, and propulsion engineering results.
        /// </summary>
        public AscentModel Ascent { get; internal set; }

        /// <summary>
        /// Engine-owned ORBIT state from the same flight-analysis cycle.
        /// This exposes the existing OrbitFoundationSystem result to
        /// MissionControl without duplicating ORBIT calculations in the UI.
        /// </summary>
        public OrbitModel Orbit { get; internal set; }

        /// <summary>
        /// Engine-owned maneuver plan derived from the ORBIT state in this
        /// analysis cycle. Build 11.0 supports circularization at apoapsis only.
        /// </summary>
        public ManeuverPlanModel ManeuverPlan { get; internal set; }

        public GuidanceSolutionModel Guidance { get; internal set; }

        /// <summary>
        /// Build 14.0 Engine-owned synthetic spacecraft systems graph.
        /// This contains modeled components and dependencies only; it does not
        /// mutate KSP or claim stock KSP models electrical buses.
        /// </summary>
        public SpacecraftSystemsModel SpacecraftSystems { get; internal set; }

        public IReadOnlyList<string> Diagnostics { get; private set; }
    }
}
