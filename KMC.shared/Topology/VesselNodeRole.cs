using System;

namespace KMC.Shared.Topology
{
    /// <summary>
    /// Functional capabilities of a vessel part.
    /// Flags allow one physical part to perform several roles.
    /// </summary>
    [Flags]
    public enum VesselNodeRole
    {
        None = 0,

        Command = 1 << 0,
        Crew = 1 << 1,
        Engine = 1 << 2,
        SolidPropulsion = 1 << 3,
        LiquidPropulsion = 1 << 4,

        StoresLiquidFuel = 1 << 5,
        StoresOxidizer = 1 << 6,
        StoresMonopropellant = 1 << 7,
        StoresSolidFuel = 1 << 8,
        StoresXenonGas = 1 << 9,
        StoresElectricCharge = 1 << 10,

        Decoupler = 1 << 11,
        Separator = 1 << 12,
        Fairing = 1 << 13,

        RcsThruster = 1 << 14,
        ReactionWheel = 1 << 15,

        SolarGeneration = 1 << 16,
        ElectricalGeneration = 1 << 17,
        FuelCell = 1 << 18,

        DockingPort = 1 << 19,
        Antenna = 1 << 20,

        Science = 1 << 21,
        Cargo = 1 << 22,
        Structural = 1 << 23
    }
}
