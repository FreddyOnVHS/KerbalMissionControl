using System.Drawing;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Cards
{
    public interface IMissionDisplayCard<TModel>
    {
        string Id { get; }

        Rectangle Bounds { get; set; }

        bool Visible { get; set; }

        CardDirtyState DirtyState { get; }

        long DrawCount { get; }

        double LastDrawMilliseconds { get; }

        double AverageDrawMilliseconds { get; }

        void MarkDirty(
            CardDirtyState dirtyState);

        void Draw(
            MissionRenderContext context,
            TModel model);
    }
}
