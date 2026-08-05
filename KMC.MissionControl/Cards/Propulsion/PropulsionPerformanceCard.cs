using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;
namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class PropulsionPerformanceCard : MissionDisplayCard<PropulsionPageRenderModel>
    {
        public PropulsionPerformanceCard():base("prop.performance","PROPULSION PERFORMANCE"){}
        protected override void DrawContent(MissionRenderContext c, Rectangle b, PropulsionPageRenderModel m)
        { if(m?.Graph==null||m.Analysis==null)return; PropulsionDisplayRenderer.DrawPerformance(c.Graphics,b,m.Graph,m.Analysis.SystemModel,m.Telemetry,c.SmallFont,c.SmallFont,c.PhosphorColor,c.DimPhosphorColor); }
    }
}
