using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public interface IMissionPage
    {
        string Name { get; }

        void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry);
    }
}