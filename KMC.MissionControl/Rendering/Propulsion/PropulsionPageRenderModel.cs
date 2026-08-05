using KMC.MissionControl.Models;
namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionPageRenderModel
    {
        public PropulsionRenderGraph Graph {get;set;} public PropulsionAnalysis Analysis {get;set;} public MissionTelemetry Telemetry {get;set;}
    }
}
