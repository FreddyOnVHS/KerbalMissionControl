using System.Drawing;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionLayoutNode
    {
        public PropulsionGraphNode Node { get; set; }

        public RectangleF Bounds { get; set; }

        public PointF Center
        {
            get
            {
                return new PointF(
                    Bounds.Left + Bounds.Width / 2.0f,
                    Bounds.Top + Bounds.Height / 2.0f);
            }
        }
    }
}
