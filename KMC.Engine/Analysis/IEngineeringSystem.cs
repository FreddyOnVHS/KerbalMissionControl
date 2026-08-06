namespace KMC.Engine.Analysis
{
    public interface IEngineeringSystem
    {
        string Name { get; }
        int Order { get; }
        void Analyze(AnalysisContext context);
    }
}
