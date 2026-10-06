using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Transport;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Actions for the owned part of the selection: build, train and research, abilities and support powers, stance,
    /// return to base, unload and sell.
    /// </summary>
    public sealed class CommandCard : HUDPanel
    {
        private const string ActiveClass = "hud-command--active";
        private static readonly Stance[] Stances = (Stance[])Enum.GetValues(typeof(Stance));

        private readonly List<(Button Button, Entity Prefab)> _costed = new();
        private readonly List<(Button Button, Stance Stance)> _stances = new();
        private readonly List<Entity> _armed = new();
        private readonly List<Entity> _casters = new();
        private readonly AbilityButtons _abilities = new();
        private bool _canSell;
        private bool _canUnload;
        private bool _canReturn;
        private int _hash = -1;

        public CommandCard() : base("hud-commands")
        {
        }

        public override void Refresh(HUDContext context)
        {
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            RefreshAvailability(context);
            _abilities.Refresh(context);

            var common = CommonStance(context.EntityManager);
            foreach (var (button, stance) in _stances)
            {
                button.EnableInClassList(ActiveClass, common == stance);
            }
        }

        private void RefreshAvailability(HUDContext context)
        {
            if (_costed.Count == 0)
            {
                return;
            }

            var completed = context.SnapshotCompleted();
            var queued = context.SnapshotQueued();
            foreach (var (button, prefab) in _costed)
            {
                var available = context.CanAfford(prefab) && context.PrerequisitesMet(prefab, completed);
                button.SetEnabled(available && context.CanQueueResearch(prefab, queued));
            }
        }

        private void Rebuild(HUDContext context)
        {
            Root.Clear();
            _costed.Clear();
            _stances.Clear();
            _armed.Clear();
            _casters.Clear();
            _canSell = false;
            _canUnload = false;
            _canReturn = false;

            var builds = new List<Entity>();
            var products = new List<Entity>();
            CollectOptions(context, builds, products);
            AddPrefabButtons(context, builds, prefab => context.StartPlacement(prefab));
            AddPrefabButtons(context, products, prefab => context.Issue(new PlayerCommand { Type = CommandType.Produce, Prefab = prefab }));
            _abilities.Build(context, Root, _casters);
            AddStanceButtons(context);
            AddActionButton(context, _canReturn, "Return", CommandType.ReturnToBase, 0);
            AddActionButton(context, _canUnload, "Unload", CommandType.Unload, -1);
            AddActionButton(context, _canSell, "Sell", CommandType.Sell, 0);
            Root.SetShown(Root.childCount > 0);
        }

        private void CollectOptions(HUDContext context, List<Entity> builds, List<Entity> products)
        {
            var entityManager = context.EntityManager;
            foreach (var entity in context.Selected)
            {
                if (!context.IsOwned(entity))
                {
                    continue;
                }

                CollectPrefabs(entityManager, entity, builds, products);
                if (entityManager.HasComponent<CombatStance>(entity))
                {
                    _armed.Add(entity);
                }

                if (entityManager.HasBuffer<Ability>(entity))
                {
                    _casters.Add(entity);
                }

                _canSell |= entityManager.HasComponent<BuildingTag>(entity);
                _canUnload |= entityManager.HasComponent<Container>(entity);
                _canReturn |= entityManager.HasComponent<HomePad>(entity);
            }
        }

        private static void CollectPrefabs(EntityManager entityManager, Entity entity, List<Entity> builds,
            List<Entity> products)
        {
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
        }

        private void AddActionButton(HUDContext context, bool shown, string caption, CommandType type, int argument)
        {
            if (!shown)
            {
                return;
            }

            var button = new CommandButton(caption, () => context.Issue(new PlayerCommand { Type = type, Argument = argument }));
            button.AddToClassList("hud-command--stance");
            Root.Add(button);
        }

        private void AddPrefabButtons(HUDContext context, List<Entity> prefabs, Action<Entity> onClick)
        {
            foreach (var prefab in prefabs)
            {
                var button = CommandButton.ForPrefab(context.EntityManager, prefab, () => onClick(prefab));
                _costed.Add((button, prefab));
                Root.Add(button);
            }
        }

        private void AddStanceButtons(HUDContext context)
        {
            if (_armed.Count == 0)
            {
                return;
            }

            foreach (var stance in Stances)
            {
                var button = new CommandButton(Caption(stance), () => context.Issue(new PlayerCommand
                {
                    Type = CommandType.SetStance,
                    Argument = (int)stance,
                }));
                button.AddToClassList("hud-command--stance");
                _stances.Add((button, stance));
                Root.Add(button);
            }
        }

        /// <summary>The stance every armed unit shares, or null when they differ.</summary>
        private Stance? CommonStance(EntityManager entityManager)
        {
            Stance? common = null;
            foreach (var entity in _armed)
            {
                if (!entityManager.Exists(entity))
                {
                    return null;
                }

                var stance = entityManager.GetComponentData<CombatStance>(entity).Value;
                if (common.HasValue && common != stance)
                {
                    return null;
                }

                common = stance;
            }

            return common;
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
