using HyperRTS.Simulation.Common;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>One soldier of a selected squad: its number, health, and the cover it holds or its posture. A dead one is greyed.</summary>
    [UxmlElement]
    public partial class SquadMemberCard : VisualElement
    {
        private readonly Label _number;
        private readonly HUDBar _health;
        private readonly Label _state;
        private int _shownState = -1;

        public SquadMemberCard()
        {
            AddToClassList("hud-member");
            _number = HUDElements.Text("", "hud-member__number", this);
            _health = new HUDBar();
            _health.AddToClassList("hud-member__health");
            Add(_health);
            _state = HUDElements.Text("", "hud-member__state", this);
        }

        public void Show(int index, in SquadMember member)
        {
            if (_number.text.Length == 0)
            {
                _number.text = (index + 1).ToString();
            }

            _health.Fraction = member.MaxHealth > 0f ? member.Health / member.MaxHealth : 0f;
            EnableInClassList("hud-member--dead", !member.Alive);
            var state = member.Alive ? 1 + (int)member.Posture + 4 * (int)member.Cover : 0;
            if (state != _shownState)
            {
                _shownState = state;
                _state.text = HUDText.Text(StateKey(member));
            }
        }

        // Cover shows over posture: a soldier behind a wall is described by the wall.
        private static string StateKey(in SquadMember member)
        {
            if (!member.Alive)
            {
                return "squad.dead";
            }

            return member.Cover == CoverKind.None
                ? HUDText.Key("squad", member.Posture.ToString())
                : HUDText.Key("squad", member.Cover + "Cover");
        }
    }
}
