namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Broad operational grouping for synthetic spacecraft equipment.
    /// This is intentionally independent of KSP PartModule types.
    /// </summary>
    public enum SpacecraftSystemCategory
    {
        General = 0,
        Electrical = 1,
        Guidance = 2,
        Communications = 3,
        Propulsion = 4,
        Environmental = 5,
        Thermal = 6,
        Payload = 7,
        Crew = 8
    }

    /// <summary>
    /// Intrinsic condition of a synthetic component before dependencies are
    /// considered. Build 14.0 creates only Nominal components; later builds
    /// will let the failure engine change this value.
    /// </summary>
    public enum SpacecraftSystemHealth
    {
        Nominal = 0,
        Degraded = 1,
        Failed = 2
    }

    /// <summary>
    /// Derived operational state visible to later KMC engineering systems.
    /// </summary>
    public enum SpacecraftSystemState
    {
        Offline = 0,
        Online = 1,
        Degraded = 2,
        Failed = 3,
        Unpowered = 4
    }

    /// <summary>
    /// Relationship between two synthetic systems.
    /// </summary>
    public enum SpacecraftDependencyKind
    {
        Power = 0,
        Functional = 1,
        Data = 2
    }
}
