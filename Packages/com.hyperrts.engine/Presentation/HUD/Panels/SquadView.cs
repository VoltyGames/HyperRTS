using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Squads;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>
    /// A selected squad's soldiers as cards, its suppression with the level it has reached, and while it retreats or
    /// reinforces, that state with the progress of the soldier being brought back.
    /// </summary>
    public sealed class SquadView
    {
        private const string LevelClass = "hud-squad__suppression--";

        private readonly VisualElement _root;
        private readonly VisualElement _members;
        private readonly HUDBar _suppression;
        private readonly Label _level;
        private readonly VisualElement _status;
        private readonly Label _statusText;
        private readonly HUDBar _progress;
        private readonly List<SquadMemberCard> _cards = new();
        private int _shownLevel = -1;
        private int _shownStatus = -1;

        public SquadView(VisualElement root)
        {
            _root = root;
            _members = root.Require<VisualElement>("members");
            _suppression = root.Require<HUDBar>("suppression");
            _level = root.Require<Label>("level");
            _status = root.Require<VisualElement>("status");
            _statusText = root.Require<Label>("status-text");
            _progress = root.Require<HUDBar>("progress");
        }

        public void Refresh(HUDContext context, Entity entity)
        {
            var entityManager = context.EntityManager;
            if (!entityManager.HasBuffer<SquadMember>(entity) || !context.TryGetSquadRules(out var rules))
            {
                _root.SetHidden(true);
                return;
            }

            _root.SetHidden(false);

            var members = entityManager.GetBuffer<SquadMember>(entity, true);
            Resize(members.Length);
            for (var i = 0; i < members.Length; i++)
            {
                _cards[i].Show(i, members[i]);
            }

            var suppression = entityManager.GetComponentData<Suppression>(entity);
            _suppression.Fraction = suppression.Value;
            ShowLevel(rules.Level(suppression.Value));
            ShowStatus(entityManager, entity, suppression, rules);
        }

        private void Resize(int count)
        {
            while (_cards.Count < count)
            {
                var card = new SquadMemberCard();
                _cards.Add(card);
                _members.Add(card);
            }

            while (_cards.Count > count)
            {
                _cards[^1].RemoveFromHierarchy();
                _cards.RemoveAt(_cards.Count - 1);
            }
        }

        private void ShowLevel(SuppressionLevel level)
        {
            if ((int)level == _shownLevel)
            {
                return;
            }

            if (_shownLevel >= 0)
            {
                _suppression.RemoveFromClassList(LevelClass + ((SuppressionLevel)_shownLevel).ToString().ToLowerInvariant());
            }

            _shownLevel = (int)level;
            _suppression.AddToClassList(LevelClass + level.ToString().ToLowerInvariant());
            _level.text = HUDText.Text(HUDText.Key("squad", level.ToString()));
        }

        // Retreating hides the bar; reinforcing fills it with the current soldier's time.
        private void ShowStatus(EntityManager entityManager, Entity entity, in Suppression suppression,
            in SquadRules rules)
        {
            var reinforcing = entityManager.HasEnabled<Reinforcing>(entity);
            var status = 0;
            if (suppression.Retreating)
            {
                status = 1;
            }
            else if (reinforcing)
            {
                status = 2;
            }

            _status.SetHidden(status == 0);
            _progress.SetHidden(status != 2);
            if (status != _shownStatus)
            {
                _shownStatus = status;
                _statusText.text = status == 1
                    ? HUDText.Text("squad.retreating")
                    : HUDText.Text("squad.reinforcing");
            }

            if (reinforcing)
            {
                var elapsed = entityManager.GetComponentData<Reinforcing>(entity).Elapsed;
                _progress.Fraction = rules.ReinforceTime > 0f ? elapsed / rules.ReinforceTime : 1f;
            }
        }
    }
}
