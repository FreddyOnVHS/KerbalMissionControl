using KMC.Engine.Analysis;

namespace KMC.Engine.Systems
{
    public sealed class PropulsionSystem : IEngineeringSystem
    {
        public string Name => "Propulsion";
        public int Order => 300;

        public void Analyze(AnalysisContext context)
        {
            context.Propulsion.IsAvailable = false;
            context.AddDiagnostic("Propulsion model initialized; existing Mission Control analysis is not migrated in Milestone 7.0.");
        }
    }
}
