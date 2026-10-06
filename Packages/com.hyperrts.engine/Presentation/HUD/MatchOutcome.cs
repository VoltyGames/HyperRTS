namespace HyperRTS.Presentation.HUD
{
    /// <summary>How the match ended for the local team, as the banner shows it.</summary>
    public enum MatchOutcome : byte
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
        Draw = 3,
    }
}
