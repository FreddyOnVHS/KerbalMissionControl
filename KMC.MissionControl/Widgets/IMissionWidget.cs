using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System.Drawing;

namespace KMC.MissionControl.Widgets
{
    /// <summary>
    /// Common contract for reusable mission-display widgets.
    /// Widgets draw inside the virtual mission canvas and may read
    /// the current telemetry snapshot.
    /// </summary>
    public interface IMissionWidget
    {
        void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry);
    }
}
