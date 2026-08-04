namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Controls how much of the virtual CRT canvas is available to a page.
    /// Standard preserves the established ASCENT and ORBIT presentation.
    /// FullCanvas minimizes outer margins for dense engineering displays.
    /// </summary>
    public enum MissionPageLayoutProfile
    {
        Standard,
        FullCanvas
    }
}
