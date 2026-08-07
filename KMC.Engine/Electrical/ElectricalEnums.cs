namespace KMC.Engine.Electrical
{
    public enum ElectricalNodeKind
    {
        Unknown = 0,
        Source,
        Storage,
        Consumer,
        SourceAndStorage,
        SourceAndConsumer,
        StorageAndConsumer,
        Multifunction
    }

    public enum PowerSourceType
    {
        Unknown = 0,
        Solar,
        Generator,
        FuelCell,
        Radioisotope,
        Other
    }

    public enum PowerConsumerType
    {
        Unknown = 0,
        Command,
        AttitudeControl,
        Communication,
        Science,
        Propulsion,
        ReactionControl,
        Utility,
        Other
    }

    public enum ElectricalConnectionType
    {
        Unknown = 0,
        SharedVesselElectricChargeBus
    }

    public enum ElectricalEvidenceType
    {
        Unknown = 0,
        ExistingRole,
        StoredResource,
        ModuleInput,
        ModuleOutput,
        PropellantRequirement
    }
}
