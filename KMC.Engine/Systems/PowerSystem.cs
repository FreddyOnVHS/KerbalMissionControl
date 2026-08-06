using KMC.Engine.Analysis;

namespace KMC.Engine.Systems
{
    public sealed class PowerSystem : IEngineeringSystem
    {
        public string Name => "Power";
        public int Order => 200;

        public void Analyze(AnalysisContext context)
        {
            context.Power.IsAvailable = false;
            context.AddDiagnostic("Power model initialized; telemetry mapping is not enabled in Milestone 7.0.");
        }
    }
}
