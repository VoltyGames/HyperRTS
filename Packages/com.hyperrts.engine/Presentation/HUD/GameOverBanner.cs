using System;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Victory / defeat banner for the local team once the match ends or the local player is defeated. Games pass
    /// <c>title</c> to show localized text.
    /// </summary>
    public sealed class GameOverBanner : IHUDPanel
    {
        private readonly Label _title;
        private readonly Func<MatchOutcome, string> _format;
        private MatchOutcome _shown;

        public GameOverBanner(Func<MatchOutcome, string> title = null)
        {
            _format = title ?? (outcome => outcome.ToString().ToUpperInvariant());
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
            var outcome = MatchOutcomes.Of(context.View);
            if (outcome == _shown)
            {
                return;
            }

            _shown = outcome;
            Root.SetVisible(outcome != MatchOutcome.None);
            _title.text = outcome == MatchOutcome.None ? "" : _format(outcome);
            _title.EnableInClassList("hud-banner__title--victory", outcome == MatchOutcome.Victory);
            _title.EnableInClassList("hud-banner__title--defeat", outcome == MatchOutcome.Defeat);
        }
    }
}
