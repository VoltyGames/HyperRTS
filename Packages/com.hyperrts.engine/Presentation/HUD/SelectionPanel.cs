using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Details of one selected entity, or a tile per entity type with counts for a group.</summary>
    public sealed class SelectionPanel : HUDPanel
    {
        private const int MaxGroups = 12;

        private readonly VisualElement _single;
        private readonly VisualElement _iconSlot;
        private readonly Label _name;
        private readonly HUDBar _health;
        private readonly Label _healthText;
        private readonly HUDBar _construction;
        private readonly ProductionQueueView _queue = new();
        private readonly VisualElement _groups;
        private int _hash = -1;
        private float _shownCurrent = float.NaN;
        private float _shownMax = float.NaN;

        public SelectionPanel() : base("hud-selection")
        {
            _single = HUDElements.Box("hud-single", Root);
            var header = HUDElements.Box("hud-single__header", _single);
            _iconSlot = HUDElements.Box("hud-single__icon-slot", header);
            var details = HUDElements.Box("hud-single__details", header);
            _name = HUDElements.Text("", "hud-single__name", details);
            _health = new HUDBar("hud-health", details);
            _healthText = HUDElements.Text("", "hud-single__caption", details);
            _construction = new HUDBar("hud-construction", details);
            _single.Add(_queue.Root);

            _groups = HUDElements.Box("hud-groups", Root);
        }

        public override void Refresh(HUDContext context)
        {
            var selected = context.Selected;
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            Root.SetShown(selected.Count > 0);
            if (selected.Count == 1)
            {
                RefreshSingle(context, selected[0]);
            }
        }

        private void Rebuild(HUDContext context)
        {
            var selected = context.Selected;
            _single.SetVisible(selected.Count == 1);
            _groups.SetVisible(selected.Count > 1);
            if (selected.Count == 1)
            {
                var entity = selected[0];
                var entityManager = context.EntityManager;
                _iconSlot.Clear();
                var icon = HUDIcon.Of(entityManager, entity, "hud-single__icon");
                if (entityManager.HasComponent<Faction>(entity))
                {
                    var color = context.View.ColorOf(entityManager.GetComponentData<Faction>(entity).Value);
                    icon.style.borderBottomColor = color;
                }

                _iconSlot.Add(icon);
                _name.text = entityManager.GetComponentData<EntityInfo>(entity).Name.ToString();
            }
            else if (selected.Count > 1)
            {
                RebuildGroups(context);
            }
        }

        private void RefreshSingle(HUDContext context, Entity entity)
        {
            var entityManager = context.EntityManager;
            var hasHealth = entityManager.HasComponent<Health>(entity);
            _health.SetVisible(hasHealth);
            _healthText.SetVisible(hasHealth);
            if (hasHealth)
            {
                var health = entityManager.GetComponentData<Health>(entity);
                _health.Fraction = health.Fraction;
                if (health.Current != _shownCurrent || health.Max != _shownMax)
                {
                    _shownCurrent = health.Current;
                    _shownMax = health.Max;
                    _healthText.text = $"{health.Current:0} / {health.Max:0}";
                }
            }

            var building = entityManager.HasEnabled<ConstructionProgress>(entity);
            _construction.SetVisible(building);
            if (building)
            {
                _construction.Fraction = entityManager.GetComponentData<ConstructionProgress>(entity).Value;
            }

            _queue.Refresh(context, entity);
        }

        private void RebuildGroups(HUDContext context)
        {
            _groups.Clear();
            var entityManager = context.EntityManager;
            var counts = new Dictionary<int, int>();
            var representatives = new List<Entity>();
            foreach (var entity in context.Selected)
            {
                var typeId = entityManager.GetComponentData<EntityInfo>(entity).TypeId;
                counts.TryGetValue(typeId, out var count);
                if (count == 0)
                {
                    representatives.Add(entity);
                }

                counts[typeId] = count + 1;
            }

            for (var i = 0; i < representatives.Count && i < MaxGroups; i++)
            {
                var entity = representatives[i];
                var tile = HUDElements.Box("hud-group", _groups);
                tile.Add(HUDIcon.Of(entityManager, entity, "hud-group__icon"));
                var typeId = entityManager.GetComponentData<EntityInfo>(entity).TypeId;
                HUDElements.Text(counts[typeId].ToString(), "hud-group__count", tile);
            }
        }
    }
}
