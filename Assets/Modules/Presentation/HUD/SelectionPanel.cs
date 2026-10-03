using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Details of one selected entity, or a tile per entity type with counts for a group.</summary>
    public sealed class SelectionPanel
    {
        private const int MaxGroups = 12;

        private readonly VisualElement _single;
        private readonly VisualElement _iconSlot;
        private readonly Label _name;
        private readonly VisualElement _healthFill;
        private readonly Label _healthText;
        private readonly VisualElement _construction;
        private readonly VisualElement _constructionFill;
        private readonly ProductionQueueView _queue = new();
        private readonly VisualElement _groups;
        private int _hash = -1;
        private float _shownCurrent = float.NaN;
        private float _shownMax = float.NaN;

        public SelectionPanel()
        {
            Root = HUDElements.Box("hud-selection");
            Root.AddToClassList("hud-panel");

            _single = HUDElements.Box("hud-single", Root);
            var header = HUDElements.Box("hud-single__header", _single);
            _iconSlot = HUDElements.Box("hud-single__icon-slot", header);
            var details = HUDElements.Box("hud-single__details", header);
            _name = HUDElements.Text("", "hud-single__name", details);
            HUDElements.Bar("hud-health", details, out _healthFill);
            _healthText = HUDElements.Text("", "hud-single__caption", details);
            _construction = HUDElements.Bar("hud-construction", details, out _constructionFill);
            _single.Add(_queue.Root);

            _groups = HUDElements.Box("hud-groups", Root);
        }

        public VisualElement Root { get; }

        public void Refresh(HUDContext context)
        {
            var selected = context.Selected;
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            HUDElements.SetShown(Root, selected.Count > 0);
            if (selected.Count == 1)
            {
                RefreshSingle(context, selected[0]);
            }
        }

        private void Rebuild(HUDContext context)
        {
            var selected = context.Selected;
            HUDElements.SetVisible(_single, selected.Count == 1);
            HUDElements.SetVisible(_groups, selected.Count > 1);
            if (selected.Count == 1)
            {
                var entity = selected[0];
                var entityManager = context.EntityManager;
                _iconSlot.Clear();
                var icon = HUDElements.EntityIcon(entityManager, entity, "hud-single__icon");
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
            HUDElements.SetVisible(_healthFill.parent, hasHealth);
            HUDElements.SetVisible(_healthText, hasHealth);
            if (hasHealth)
            {
                var health = entityManager.GetComponentData<Health>(entity);
                HUDElements.SetFraction(_healthFill, health.Fraction);
                if (health.Current != _shownCurrent || health.Max != _shownMax)
                {
                    _shownCurrent = health.Current;
                    _shownMax = health.Max;
                    _healthText.text = $"{health.Current:0} / {health.Max:0}";
                }
            }

            var building = ConstructionRules.IsUnderConstruction(entityManager, entity);
            HUDElements.SetVisible(_construction, building);
            if (building)
            {
                HUDElements.SetFraction(_constructionFill, entityManager.GetComponentData<ConstructionProgress>(entity).Value);
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
                tile.Add(HUDElements.EntityIcon(entityManager, entity, "hud-group__icon"));
                var typeId = entityManager.GetComponentData<EntityInfo>(entity).TypeId;
                HUDElements.Text(counts[typeId].ToString(), "hud-group__count", tile);
            }
        }
    }
}
