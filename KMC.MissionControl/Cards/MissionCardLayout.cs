using System;
using System.Drawing;
namespace KMC.MissionControl.Cards
{
    public sealed class MissionCardLayout
    {
        public Rectangle EngineCluster {get;set;} public Rectangle Performance {get;set;} public Rectangle PropellantFlow {get;set;} public Rectangle Footer {get;set;}
    }
    public static class MissionCardLayoutEngine
    {
        public static MissionCardLayout CalculatePropulsion(Rectangle w)
        {
            const int gap=14, footerGap=12; int footerHeight=Math.Max(58,w.Height/11);
            var display=new Rectangle(w.Left,w.Top,w.Width,Math.Max(1,w.Height-footerHeight-footerGap));
            int upper=Math.Max(190,display.Height*36/100);
            var left=new Rectangle(display.Left,display.Top,display.Width*48/100,upper);
            var right=new Rectangle(left.Right+gap,display.Top,Math.Max(1,display.Right-left.Right-gap),upper);
            var flow=new Rectangle(display.Left,left.Bottom+gap,display.Width,Math.Max(1,display.Bottom-left.Bottom-gap));
            var footer=new Rectangle(w.Left,display.Bottom+footerGap,w.Width,footerHeight);
            return new MissionCardLayout{EngineCluster=left,Performance=right,PropellantFlow=flow,Footer=footer};
        }
    }
}
