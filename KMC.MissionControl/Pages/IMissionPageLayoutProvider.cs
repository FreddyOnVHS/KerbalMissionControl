namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Optional page capability. Pages that do not implement this interface
    /// automatically use MissionPageLayoutProfile.Standard.
    /// </summary>
    public interface IMissionPageLayoutProvider
    {
        MissionPageLayoutProfile LayoutProfile { get; }
    }
}
