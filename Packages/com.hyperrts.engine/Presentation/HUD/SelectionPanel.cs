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

        private readonly VisualTreeAsset _groupTemplate;
        private readonly VisualElement _single;
        private readonly HUDIcon _icon;
        private readonly Label _name;
        private readonly HUDBar _health;
        private readonly Label _healthText;
        private readonly HUDBar _construction;
        private readonly ProductionQueueView _queue;
        private readonly VisualElement _groups;
        private int _hash = -1;
        private float _shownCurrent = float.NaN;
        private float _shownMax = float.NaN;

        public SelectionPanel(HUDPanelLayouts layouts) : base(layouts.SelectionPanel)
        {
            _groupTemplate = layouts.SelectionPanelGroup;
            _single = Root.Require<VisualElement>("single");
            _icon = Root.Require<HUDIcon>("icon");
            _name = Root.Require<Label>("name");
            _health = Root.Require<HUDBar>("health");
            _healthText = Root.Require<Label>("health-text");
            _construction = Root.Require<HUDBar>("construction");
            _queue = new ProductionQueueView(Root.Require<VisualElement>("queue"), layouts.ProductionQueueViewSlot);
            _groups = Root.Require<VisualElement>("groups");
        }

        public override void Refresh(HUDContext context)
        {
            var selected = context.Selected;
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            Root.SetInvisible(selected.Count == 0);
            if (selected.Count == 1)
            {
                RefreshSingle(context, selected[0]);
            }
        }

        private void Rebuild(HUDContext context)
        {
            var selected = context.Selected;
            _single.SetHidden(selected.Count != 1);
            _groups.SetHidden(selected.Count <= 1);
            if (selected.Count == 1)
            {
                var entity = selected[0];
                var entityManager = context.EntityManager;
                _icon.Bind(entityManager, entity);
                _icon.style.borderBottomColor = entityManager.HasComponent<Faction>(entity)
                    ? context.View.GetColor(entityManager.GetComponentData<Faction>(entity).Value)
                    : new StyleColor(StyleKeyword.Null);
                _name.text = HUDText.EntityName(entityManager.GetComponentData<EntityInfo>(entity).Name.ToString());
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
            _health.SetHidden(!hasHealth);
            _healthText.SetHidden(!hasHealth);
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
            _construction.SetHidden(!building);
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
                var tile = _groupTemplate.CloneRoot();
                _groups.Add(tile);
                tile.Require<HUDIcon>("icon").Bind(entityManager, entity);
                var typeId = entityManager.GetComponentData<EntityInfo>(entity).TypeId;
                tile.Require<Label>("count").text = counts[typeId].ToString();
            }
        }
    }
}
