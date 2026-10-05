namespace KMC.Engine.CelestialMechanics
{
    /// <summary>
    /// Selects which zero-revolution geometric arc is used by the Lambert solver.
    /// ShortWay uses the smaller transfer angle; LongWay uses the supplementary
    /// path around the central body. Collinear endpoint geometry is intentionally
    /// unsupported because the transfer plane is not uniquely defined.
    /// </summary>
    public enum LambertTransferPath
    {
        ShortWay = 0,
        LongWay = 1
    }
}
