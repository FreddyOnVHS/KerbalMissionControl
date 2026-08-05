using System;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Describes why a card needs to be redrawn.
    ///
    /// Build 0.9.0.2 records these states but still draws cards into the
    /// existing full-page bitmap. A later milestone will use the states to
    /// decide which retained card bitmap must be rebuilt.
    /// </summary>
    [Flags]
    public enum CardDirtyState
    {
        None = 0,

        Layout = 1,

        Static = 2,

        Telemetry = 4,

        All =
            Layout |
            Static |
            Telemetry
    }
}
