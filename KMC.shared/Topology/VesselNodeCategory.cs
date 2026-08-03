namespace KMC.Shared.Topology
{
    /// <summary>
    /// Primary schematic identity for a vessel part.
    /// A part may perform several functions; see VesselNodeRole.
    /// </summary>
    public enum VesselNodeCategory
    {
        Unknown = 0,
        Command = 1,
        Engine = 2,
        SolidBooster = 3,
        FuelTank = 4,
        Decoupler = 5,
        Fairing = 6,
        RcsThruster = 7,
        ReactionWheel = 8,
        Battery = 9,
        SolarPanel = 10,
        Generator = 11,
        DockingPort = 12,
        Antenna = 13,
        Structural = 14,
        Payload = 15
    }
}
