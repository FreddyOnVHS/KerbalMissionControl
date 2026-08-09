using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Build 9.6.2 ASCENT layout.
    ///
    /// Uses the existing 1920x1080 DenseEngineering canvas but reallocates
    /// vertical space so the Engine-owned Flight Director and both prediction
    /// columns have enough room for full engineering text without overflow.
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
                        0.430f,
                        0.730f),

                Navball =
                    context.GetRelativeRectangle(
                        0.458f,
                        0.070f,
                        0.295f,
                        0.345f),

                OrbitTrend =
                    context.GetRelativeRectangle(
                        0.768f,
                        0.070f,
                        0.220f,
                        0.180f),

                FlightDirector =
                    context.GetRelativeRectangle(
                        0.458f,
                        0.430f,
                        0.530f,
                        0.185f),

                Prediction =
                    context.GetRelativeRectangle(
                        0.458f,
                        0.630f,
                        0.530f,
                        0.205f),

                Footer =
                    context.GetRelativeRectangle(
                        0.012f,
                        0.850f,
                        0.976f,
                        0.125f)
            };
        }
    }
}
