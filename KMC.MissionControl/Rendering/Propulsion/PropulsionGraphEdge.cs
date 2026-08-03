namespace KMC.MissionControl.Rendering.Propulsion
{
    public enum PropulsionGraphEdgeKind
    {
        Structural = 0,
        Separation = 1,
        Propellant = 2
    }

    /// <summary>
    /// Directed edge between two visible propulsion graph nodes.
    /// Structural edges point away from the vessel root. Propellant edges
    /// point from a resource source toward an engine.
    /// </summary>
    public sealed class PropulsionGraphEdge
    {
        public uint FromPartId { get; set; }

        public uint ToPartId { get; set; }

        public PropulsionGraphEdgeKind Kind { get; set; }

        public string ResourceName { get; set; } = string.Empty;
    }
}
