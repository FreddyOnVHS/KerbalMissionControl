namespace KMC.Engine.Capabilities
{
    public enum PartCapabilityType
    {
        Unknown = 0,
        Command,
        CrewSupport,
        ElectricalStorage,
        ElectricalProducer,
        ElectricalConsumer,
        ResourceStorage,
        ResourceConsumer,
        Propulsion,
        ReactionControl,
        AttitudeControl,
        Communication,
        Science,
        Docking,
        Separation,
        Structural
    }

    public enum CapabilitySource
    {
        Unknown = 0,
        ExistingRole,
        StoredResource,
        PropellantRequirement,
        Inferred
    }

    public enum ResourceCategory
    {
        Unknown = 0,
        Electrical,
        Fuel,
        Oxidizer,
        ReactionControl,
        SolidPropellant,
        NobleGas,
        Coolant,
        LifeSupport
    }

    public enum ClassificationConfidence
    {
        Low = 0,
        Medium,
        High,
        Explicit
    }
}
