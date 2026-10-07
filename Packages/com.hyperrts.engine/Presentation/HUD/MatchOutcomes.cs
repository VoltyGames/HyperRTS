using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Match;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>The match result from the local team's side, shared by the banner and game end screens.</summary>
    public static class MatchOutcomes
    {
        /// <summary>None while the match goes on and the local player is still in it.</summary>
        public static MatchOutcome Of(MatchView view)
        {
            if (!view.TryGetMatch(out var match) || match.Phase != MatchPhase.Ended)
            {
                return view.IsLocalDefeated() ? MatchOutcome.Defeat : MatchOutcome.None;
            }

            if (match.WinningTeam == 0)
            {
                return MatchOutcome.Draw;
            }

            var localTeam = view.Relations.TeamOf(view.Local.Faction);
            return match.WinningTeam == localTeam ? MatchOutcome.Victory : MatchOutcome.Defeat;
        }
    }
}
