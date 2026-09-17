using System.Drawing;
using System.Windows.Forms;

namespace KMC.MissionControl.Pages
{
    public interface IMissionPagePointerInput
    {
        bool PointerDown(PointF virtualPoint, MouseButtons button);
        bool PointerMove(PointF virtualPoint, MouseButtons buttons);
        bool PointerUp(PointF virtualPoint, MouseButtons button);
        bool PointerWheel(PointF virtualPoint, int delta);
    }
}
