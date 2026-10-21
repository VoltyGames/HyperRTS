using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Victory or defeat banner for the local team once the match ends or the local player is defeated.</summary>
    public sealed class GameOverBanner : HUDPanel
    {
        private readonly Label _title;
        private MatchOutcome _shown;

        public GameOverBanner(HUDPanelLayouts layouts)
            : base(layouts.GameOverBanner)
        {
            _title = Root.Require<Label>("title");
        }

        public override bool BlocksPointer => false;

        public override bool NeedsPlayer => false;

        public override void Refresh(HUDContext context)
        {
            var outcome = MatchOutcomes.Get(context.View);
            if (outcome == _shown)
            {
                return;
            }

            _shown = outcome;
            Root.SetHidden(outcome == MatchOutcome.None);
            _title.text = outcome == MatchOutcome.None ? "" : HUDText.OutcomeTitle(outcome);
            _title.EnableInClassList("hud-banner__title--victory", outcome == MatchOutcome.Victory);
            _title.EnableInClassList("hud-banner__title--defeat", outcome == MatchOutcome.Defeat);
        }
    }
}
