using System.Drawing;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Immutable diagnostic record for one display card.
    /// </summary>
    public sealed class CardDiagnosticSnapshot
    {
        public string Id { get; set; }

        public Rectangle Bounds { get; set; }

        public CardDirtyState DirtyStateBeforeDraw
        {
            get;
            set;
        }

        public long DrawCount { get; set; }

        public double LastDrawMilliseconds { get; set; }

        public double AverageDrawMilliseconds { get; set; }
    }
}
