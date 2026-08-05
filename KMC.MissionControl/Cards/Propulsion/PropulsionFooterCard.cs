using System;
using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;
namespace KMC.MissionControl.Cards.Propulsion
{
 public sealed class PropulsionFooterCard:IMissionDisplayCard<PropulsionPageRenderModel>
 {
  public PropulsionFooterCard(){Id="prop.footer";Visible=true;} public string Id{get;private set;} public Rectangle Bounds{get;set;} public bool Visible{get;set;}
  public void Draw(MissionRenderContext c,PropulsionPageRenderModel m)
  { if(!Visible||c==null||m?.Telemetry==null)return; using(var f=new SolidBrush(Color.FromArgb(65,2,14,20)))using(var p=new Pen(Color.FromArgb(125,c.DimPhosphorColor),1.3f)){c.Graphics.FillRectangle(f,Bounds);c.Graphics.DrawRectangle(p,Bounds);}
   string[] l={"STAGE","THROTTLE","THRUST","TWR","ISP","ENGINES","ACTIVE LF","ACTIVE OX","GRAPH REV"}; var t=m.Telemetry; string[] v={t.CurrentStage.ToString("00"),Pct(t.Throttle),Num(t.CurrentThrust,"0.0"," kN"),Num(t.ThrustToWeightRatio,"0.00",""),Num(t.AverageSpecificImpulse,"0"," s"),t.EngineCount.ToString("00"),Pct(Frac(t.StageLiquidFuelAmount,t.StageLiquidFuelCapacity)),Pct(Frac(t.StageOxidizerAmount,t.StageOxidizerCapacity)),m.Graph!=null?m.Graph.TopologyRevision.ToString():"--"}; int w=Math.Max(1,Bounds.Width/l.Length);
   for(int i=0;i<l.Length;i++){var b=new Rectangle(Bounds.Left+i*w,Bounds.Top,i==l.Length-1?Bounds.Right-(Bounds.Left+i*w):w,Bounds.Height); Cell(c,b,l[i],v[i],i>0);}
  }
  static void Cell(MissionRenderContext c,Rectangle b,string l,string v,bool d){using(var lb=new SolidBrush(c.DimPhosphorColor))using(var vb=new SolidBrush(c.PhosphorColor))using(var p=new Pen(Color.FromArgb(80,c.DimPhosphorColor)))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){if(d)c.Graphics.DrawLine(p,b.Left,b.Top+8,b.Left,b.Bottom-8);c.Graphics.DrawString(l,c.SmallFont,lb,new Rectangle(b.Left,b.Top+5,b.Width,b.Height/2-2),sf);c.Graphics.DrawString(v,c.SmallFont,vb,new Rectangle(b.Left,b.Top+b.Height/2,b.Width,b.Height/2-3),sf);}}
  static double Frac(double a,double c)=>c<=0?0:Math.Max(0,Math.Min(1,a/c)); static string Pct(double f)=>(Math.Max(0,Math.Min(1,f))*100).ToString("0")+"%"; static string Num(double v,string f,string s)=>double.IsNaN(v)||double.IsInfinity(v)?"---":Math.Max(0,v).ToString(f)+s;
 }
}
