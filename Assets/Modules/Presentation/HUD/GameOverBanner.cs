using HyperRTS.Simulation.Match;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Victory / defeat banner for the local team once the match ends or the local player is defeated.</summary>
    public sealed class GameOverBanner
    {
        private readonly Label _title;
        private string _shown;

        public GameOverBanner()
        {
            Root = HudElements.Box("hud-banner");
            Root.pickingMode = PickingMode.Ignore;
            _title = HudElements.Text("", "hud-banner__title", Root);
            _title.pickingMode = PickingMode.Ignore;
            HudElements.SetVisible(Root, false);
        }

        public VisualElement Root { get; }

        public void Refresh(HudContext context)
        {
            var outcome = Outcome(context);
            if (outcome == _shown)
            {
                return;
            }

            _shown = outcome;
            HudElements.SetVisible(Root, outcome != null);
            _title.text = outcome ?? "";
            _title.EnableInClassList("hud-banner__title--victory", outcome == "VICTORY");
            _title.EnableInClassList("hud-banner__title--defeat", outcome == "DEFEAT");
        }

        private static string Outcome(HudContext context)
        {
            var view = context.View;
            if (view.TryGetMatch(out var match) && match.Phase == MatchPhase.Ended)
            {
                return match.WinningTeam == 0 ? "DRAW"
                    : match.WinningTeam == view.Relations.TeamOf(view.Local.Faction) ? "VICTORY"
                    : "DEFEAT";
            }

            return view.IsLocalDefeated() ? "DEFEAT" : null;
        }
    }
}
