using System.Drawing;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Optional mission-page capability for dense engineering displays that
    /// require a larger logical drawing surface than the standard pages.
    ///
    /// Pages that do not implement this interface retain the existing
    /// automatic virtual-canvas behavior.
    /// </summary>
    public interface IMissionPageCanvasProvider
    {
        Size PreferredVirtualCanvasSize { get; }

        MissionPageContentProfile ContentProfile { get; }
    }
}
