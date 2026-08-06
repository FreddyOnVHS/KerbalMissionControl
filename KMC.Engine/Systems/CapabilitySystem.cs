using KMC.Engine.Analysis;

namespace KMC.Engine.Systems
{
    public sealed class CapabilitySystem : IEngineeringSystem
    {
        public string Name => "Capabilities";
        public int Order => 100;

        public void Analyze(AnalysisContext context)
        {
            if (context.Vessel.PartCount > 0)
            {
                context.Capabilities.Add("VesselTopology");
            }

            context.AddDiagnostic("Capability framework initialized.");
        }
    }
}
