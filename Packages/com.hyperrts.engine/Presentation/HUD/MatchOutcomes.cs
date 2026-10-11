using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Match;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>The match result from the local team's side (or an observer's), shared by the banner and end screens.</summary>
    public static class MatchOutcomes
    {
        /// <summary>None while the match goes on and the local player is still in it.</summary>
        public static MatchOutcome Get(MatchView view)
        {
            if (!view.TryGetMatch(out var match) || match.Phase != MatchPhase.Ended)
            {
                return view.IsLocalDefeated() ? MatchOutcome.Defeat : MatchOutcome.None;
            }

            if (match.WinningTeam == 0)
            {
                return MatchOutcome.Draw;
            }

            if (!view.HasLocalPlayer)
            {
                return MatchOutcome.Ended;
            }

            var localTeam = view.Relations.GetTeam(view.Local.Faction);
            return match.WinningTeam == localTeam ? MatchOutcome.Victory : MatchOutcome.Defeat;
        }
    }
}
