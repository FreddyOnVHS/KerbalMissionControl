using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;
namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class PropellantFlowCard : MissionDisplayCard<PropulsionPageRenderModel>
    {
        public PropellantFlowCard():base("prop.flow","PROPELLANT FLOW / STAGE SYSTEM"){}
        protected override void DrawContent(MissionRenderContext c, Rectangle b, PropulsionPageRenderModel m)
        { if(m?.Analysis==null)return; PropulsionDisplayRenderer.DrawSystemFlow(c.Graphics,b,m.Analysis.SystemModel,m.Telemetry,c.SmallFont,c.SmallFont,c.PhosphorColor,c.DimPhosphorColor); }
    }
}
