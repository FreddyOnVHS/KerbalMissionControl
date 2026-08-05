using System.Drawing;
using KMC.MissionControl.Rendering;
namespace KMC.MissionControl.Cards
{
    public interface IMissionDisplayCard<TModel>
    {
        string Id { get; }
        Rectangle Bounds { get; set; }
        bool Visible { get; set; }
        void Draw(MissionRenderContext context, TModel model);
    }
}
