using HyperRTS.Simulation.Match;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The match has players, at most one local human, and no player on the neutral team.</summary>
    public sealed class MatchRule : AuthoringRule<MatchAuthoring>
    {
        protected override void Check(MatchAuthoring match, ValidationIssues issues)
        {
            if (match.players.Count == 0)
            {
                issues.Error(match, "The match has no players.");
            }

            if (match.players.FindAll(player => player.control == PlayerControl.LocalHuman).Count > 1)
            {
                issues.Warn(match, "More than one player is Local Human; only one can be controlled.");
            }

            if (match.players.Exists(player => player.team == 0))
            {
                issues.Warn(match, "Team 0 is neutral; give every player a team of 1 or more.");
            }
        }
    }
}
