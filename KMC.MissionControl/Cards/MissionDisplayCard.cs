using System;
using System.Drawing;
using KMC.MissionControl.Rendering;
namespace KMC.MissionControl.Cards
{
    public abstract class MissionDisplayCard<TModel> : IMissionDisplayCard<TModel>
    {
        protected MissionDisplayCard(string id, string title) { Id=id; Title=title; Visible=true; }
        public string Id { get; private set; }
        public string Title { get; protected set; }
        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }
        public void Draw(MissionRenderContext context, TModel model)
        {
            if(!Visible || context==null || Bounds.Width<=0 || Bounds.Height<=0) return;
            using(var fill=new SolidBrush(Color.FromArgb(70,2,14,20)))
            using(var border=new Pen(Color.FromArgb(130,context.DimPhosphorColor),1.4f))
            using(var brush=new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.FillRectangle(fill,Bounds); context.Graphics.DrawRectangle(border,Bounds);
                context.Graphics.DrawString(Title,context.SmallFont,brush,Bounds.Left+14,Bounds.Top+12);
                context.Graphics.DrawLine(border,Bounds.Left+14,Bounds.Top+39,Bounds.Right-14,Bounds.Top+39);
            }
            var content=new Rectangle(Bounds.Left+18,Bounds.Top+48,Math.Max(1,Bounds.Width-36),Math.Max(1,Bounds.Height-58));
            var state=context.Graphics.Save();
            try { context.Graphics.SetClip(content); DrawContent(context,content,model); }
            finally { context.Graphics.Restore(state); }
        }
        protected abstract void DrawContent(MissionRenderContext context, Rectangle contentBounds, TModel model);
    }
}
