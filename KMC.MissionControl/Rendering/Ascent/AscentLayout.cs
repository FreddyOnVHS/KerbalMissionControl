using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Centralized geometry for the Ascent Guidance page.
    ///
    /// Build 9.5.2 uses the 1920x1080 DenseEngineering canvas requested by
    /// AscentPage. The FDAI is intentionally promoted to a primary instrument
    /// while the existing graph/guidance/prediction stack remains available
    /// for foundation validation.
    ///
    /// Build 9.6 will complete the Engine-owned ASCENT integration.
    /// </summary>
    public sealed class AscentLayout
    {
        public Rectangle Graph { get; private set; }

        public Rectangle Navball { get; private set; }

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
                        0.012f,
                        0.070f,
                        0.468f,
                        0.748f),

                Navball =
                    context.GetRelativeRectangle(
                        0.496f,
                        0.070f,
                        0.286f,
                        0.474f),

                OrbitTrend =
                    context.GetRelativeRectangle(
                        0.797f,
                        0.070f,
                        0.191f,
                        0.220f),

                FlightDirector =
                    context.GetRelativeRectangle(
                        0.797f,
                        0.305f,
                        0.191f,
                        0.239f),

                Prediction =
                    context.GetRelativeRectangle(
                        0.496f,
                        0.562f,
                        0.492f,
                        0.256f),

                Footer =
                    context.GetRelativeRectangle(
                        0.012f,
                        0.836f,
                        0.976f,
                        0.148f)
            };
        }
    }
}
