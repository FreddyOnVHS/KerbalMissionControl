using System;
using System.Drawing;
using KMC.MissionControl.Cards;
using KMC.MissionControl.Cards.Propulsion;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;
namespace KMC.MissionControl.Pages
{
 public sealed class PropulsionPage:IMissionPage,IMissionPageCanvasProvider
 {
  readonly EngineClusterCard a=new EngineClusterCard(); readonly PropulsionPerformanceCard b=new PropulsionPerformanceCard(); readonly PropellantFlowCard c=new PropellantFlowCard(); readonly PropulsionFooterCard d=new PropulsionFooterCard();
  public string Name=>"PROPULSION"; public Size PreferredVirtualCanvasSize=>new Size(2400,1350); public MissionPageContentProfile ContentProfile=>MissionPageContentProfile.DenseEngineering;
  public void Draw(MissionRenderContext x,MissionTelemetry t){if(x==null)throw new ArgumentNullException(nameof(x));if(t==null)return;new MissionPageLayout(x).DrawHeader(Name,"CH 04");var w=new Rectangle(x.ContentBounds.Left+18,x.ContentBounds.Top+78,x.ContentBounds.Width-36,x.ContentBounds.Height-98);var l=MissionCardLayoutEngine.CalculatePropulsion(w);var g=PropulsionGraphStore.GetCurrent();var an=g!=null?PropulsionAnalysisCache.GetOrBuild(g):null;var m=new PropulsionPageRenderModel{Graph=g,Analysis=an,Telemetry=t};a.Bounds=l.EngineCluster;b.Bounds=l.Performance;c.Bounds=l.PropellantFlow;d.Bounds=l.Footer;a.Draw(x,m);b.Draw(x,m);c.Draw(x,m);d.Draw(x,m);}
 }
}
