using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;
namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class EngineClusterCard : MissionDisplayCard<PropulsionPageRenderModel>
    {
        public EngineClusterCard():base("prop.engine-cluster","PHYSICAL ENGINE CLUSTER"){}
        protected override void DrawContent(MissionRenderContext c, Rectangle b, PropulsionPageRenderModel m)
        { PropulsionDisplayRenderer.DrawEngineCluster(c.Graphics,b,m?.Analysis?.EngineCluster,m?.Telemetry,c.SmallFont,c.SmallFont,c.PhosphorColor,c.DimPhosphorColor); }
    }
}
