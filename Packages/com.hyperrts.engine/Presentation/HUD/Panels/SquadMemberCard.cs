using HyperRTS.Simulation.Common;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>One soldier of a selected squad: its weapon, health, and the cover it holds or its posture. A dead one is greyed.</summary>
    [UxmlElement]
    public partial class SquadMemberCard : VisualElement
    {
        private readonly Label _weapon;
        private readonly HUDBar _health;
        private readonly Label _state;
        private int _shownState = -1;

        public SquadMemberCard()
        {
            AddToClassList("hud-member");
            _weapon = HUDElements.Text("", "hud-member__weapon", this);
            _health = new HUDBar();
            _health.AddToClassList("hud-member__health");
            Add(_health);
            _state = HUDElements.Text("", "hud-member__state", this);
        }

        /// <summary>Shows <paramref name="member"/> carrying <paramref name="weapon"/>, the designation baked on its squad.</summary>
        public void Show(in SquadMember member, string weapon)
        {
            if (_weapon.text != weapon)
            {
                _weapon.text = weapon;
            }

            _health.Fraction = member.MaxHealth > 0f ? member.Health / member.MaxHealth : 0f;
            EnableInClassList("hud-member--dead", !member.Alive);
            var state = member.Alive ? 1 + (int)member.Posture + 4 * (int)member.Cover + 16 * (int)member.Action : 0;
            if (state != _shownState)
            {
                _shownState = state;
                _state.text = HUDText.Text(StateKey(member));
            }
        }

        // Work shows over cover and cover over posture: a soldier behind a wall is described by the wall.
        private static string StateKey(in SquadMember member)
        {
            if (!member.Alive)
            {
                return "squad.dead";
            }

            if (member.Action == MemberAction.Dig)
            {
                return "squad.digging";
            }

            return member.Cover == CoverKind.None
                ? HUDText.Key("squad", member.Posture.ToString())
                : HUDText.Key("squad", member.Cover + "Cover");
        }
    }
}
