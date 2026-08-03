using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Centralized geometry for the Ascent Guidance page.
    ///
    /// Future panel resizing should be made here instead of directly in
    /// AscentPage or the individual renderers.
    /// </summary>
    public sealed class AscentLayout
    {
        public Rectangle Graph { get; private set; }

        public Rectangle OrbitTrend { get; private set; }

        public Rectangle FlightDirector { get; private set; }

        public Rectangle Prediction { get; private set; }

        public Rectangle Footer { get; private set; }

        public static AscentLayout Create(
            MissionRenderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            return new AscentLayout
            {
                Graph =
                    context.GetRelativeRectangle(
                        0.008f,
                        0.070f,
                        0.555f,
                        0.765f),

                OrbitTrend =
                    context.GetRelativeRectangle(
                        0.575f,
                        0.070f,
                        0.417f,
                        0.220f),

                FlightDirector =
                    context.GetRelativeRectangle(
                        0.575f,
                        0.302f,
                        0.417f,
                        0.325f),

                Prediction =
                    context.GetRelativeRectangle(
                        0.575f,
                        0.639f,
                        0.417f,
                        0.196f),

                Footer =
                    context.GetRelativeRectangle(
                        0.008f,
                        0.850f,
                        0.984f,
                        0.140f)
            };
        }
    }
}
