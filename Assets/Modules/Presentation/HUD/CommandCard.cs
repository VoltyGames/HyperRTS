using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Actions for the owned part of the selection: build, train and combat stance.</summary>
    public sealed class CommandCard
    {
        private const string ActiveClass = "hud-command--active";
        private static readonly Stance[] Stances = (Stance[])Enum.GetValues(typeof(Stance));

        private readonly List<(Button Button, Entity Prefab)> _costed = new();
        private readonly List<(Button Button, Stance Stance)> _stances = new();
        private readonly List<Entity> _armed = new();
        private int _hash = -1;

        public CommandCard()
        {
            Root = HudElements.Box("hud-commands");
            Root.AddToClassList("hud-panel");
        }

        public VisualElement Root { get; }

        public void Refresh(HudContext context)
        {
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            RefreshAvailability(context);

            foreach (var (button, stance) in _stances)
            {
                button.EnableInClassList(ActiveClass, AllHaveStance(context.EntityManager, stance));
            }
        }

        private void RefreshAvailability(HudContext context)
        {
            if (_costed.Count == 0)
            {
                return;
            }

            var completed = context.CompletedBuildings;
            using var infos = completed.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            using var owners = completed.ToComponentDataArray<Faction>(Allocator.Temp);
            foreach (var (button, prefab) in _costed)
            {
                button.SetEnabled(context.CanAfford(prefab) && context.PrerequisitesMet(prefab, infos, owners));
            }
        }

        private void Rebuild(HudContext context)
        {
            Root.Clear();
            _costed.Clear();
            _stances.Clear();
            _armed.Clear();

            var builds = new List<Entity>();
            var products = new List<Entity>();
            CollectOptions(context, builds, products);
            AddPrefabButtons(context, builds, prefab => context.StartPlacement(prefab));
            AddPrefabButtons(context, products, prefab => context.Issue(new PlayerCommand { Type = CommandType.Produce, Prefab = prefab }));
            AddStanceButtons(context);
            HudElements.SetShown(Root, Root.childCount > 0);
        }

        private void CollectOptions(HudContext context, List<Entity> builds, List<Entity> products)
        {
            var entityManager = context.EntityManager;
            foreach (var entity in context.Selected)
            {
                if (!context.IsOwned(entity))
                {
                    continue;
                }

                if (entityManager.HasBuffer<BuildOption>(entity))
                {
                    foreach (var option in entityManager.GetBuffer<BuildOption>(entity, true))
                    {
                        AddDistinct(builds, option.Prefab);
                    }
                }

                if (entityManager.HasBuffer<ProductionOption>(entity))
                {
                    foreach (var option in entityManager.GetBuffer<ProductionOption>(entity, true))
                    {
                        AddDistinct(products, option.Prefab);
                    }
                }

                if (entityManager.HasComponent<CombatStance>(entity))
                {
                    _armed.Add(entity);
                }
            }
        }

        private void AddPrefabButtons(HudContext context, List<Entity> prefabs, Action<Entity> onClick)
        {
            foreach (var prefab in prefabs)
            {
                var button = CommandButtons.ForPrefab(context.EntityManager, prefab, () => onClick(prefab));
                _costed.Add((button, prefab));
                Root.Add(button);
            }
        }

        private void AddStanceButtons(HudContext context)
        {
            if (_armed.Count == 0)
            {
                return;
            }

            foreach (var stance in Stances)
            {
                var button = CommandButtons.Plain(Caption(stance), () => context.Issue(new PlayerCommand
                {
                    Type = CommandType.SetStance,
                    Argument = (int)stance,
                }));
                button.AddToClassList("hud-command--stance");
                _stances.Add((button, stance));
                Root.Add(button);
            }
        }

        private bool AllHaveStance(EntityManager entityManager, Stance stance)
        {
            foreach (var entity in _armed)
            {
                if (!entityManager.Exists(entity) || entityManager.GetComponentData<CombatStance>(entity).Value != stance)
                {
                    return false;
                }
            }

            return true;
        }

        private static string Caption(Stance stance) => stance switch
        {
            Stance.HoldPosition => "Hold",
            _ => stance.ToString(),
        };

        private static void AddDistinct(List<Entity> list, Entity entity)
        {
            if (entity != Entity.Null && !list.Contains(entity))
            {
                list.Add(entity);
            }
        }
    }
}
