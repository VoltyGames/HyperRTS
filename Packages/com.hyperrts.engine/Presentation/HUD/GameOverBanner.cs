using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Match;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Victory / defeat banner for the local team once the match ends or the local player is defeated.</summary>
    public sealed class GameOverBanner : IHUDPanel
    {
        private readonly Label _title;
        private MatchOutcome _shown;

        public GameOverBanner()
        {
            Root = HUDElements.Box("hud-banner");
            Root.pickingMode = PickingMode.Ignore;
            _title = HUDElements.Text("", "hud-banner__title", Root);
            _title.pickingMode = PickingMode.Ignore;
            Root.SetVisible(false);
        }

        public VisualElement Root { get; }

        public bool BlocksPointer => false;

        public void Refresh(HUDContext context)
        {
            var outcome = Outcome(context.View);
            if (outcome == _shown)
            {
                return;
            }

            _shown = outcome;
            Root.SetVisible(outcome != MatchOutcome.None);
            _title.text = outcome == MatchOutcome.None ? "" : outcome.ToString().ToUpperInvariant();
            _title.EnableInClassList("hud-banner__title--victory", outcome == MatchOutcome.Victory);
            _title.EnableInClassList("hud-banner__title--defeat", outcome == MatchOutcome.Defeat);
        }

        private static MatchOutcome Outcome(MatchView view)
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
