using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using UnityEngine;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>The command specs a HUD draws its buttons from, found by what each is for and the card it sits on.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "Command Catalog", fileName = "CommandCatalog")]
    public sealed class CommandCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Every command the card can show.")]
        private List<CommandSpec> specs = new();

        public IReadOnlyList<CommandSpec> Specs => specs;

        /// <summary>A catalog made in code, for tests and tools.</summary>
        public static CommandCatalog Create(params CommandSpec[] specs)
        {
            var catalog = CreateInstance<CommandCatalog>();
            catalog.specs.AddRange(specs);
            return catalog;
        }

        /// <summary>The spec for building, training or researching a type on a card, or null after logging the gap.</summary>
        public CommandSpec FindPrefab(int cardTypeId, int prefabTypeId, string name) =>
            Find(cardTypeId, $"prefab '{name}'",
                spec => spec.Target == CommandTarget.Prefab && spec.Prefab != null && spec.Prefab.TypeId == prefabTypeId);

        /// <summary>The spec for an ability on a card (card 0 for a support power), or null after logging the gap.</summary>
        public CommandSpec FindAbility(int cardTypeId, int abilityId, string name) =>
            Find(cardTypeId, $"ability '{name}'",
                spec => spec.Target == CommandTarget.Ability && EntityInfo.TypeIdFromName(spec.Ability) == abilityId);

        /// <summary>The spec for an order and its argument on a card, or null after logging the gap.</summary>
        public CommandSpec FindOrder(int cardTypeId, CommandType order, int argument) =>
            Find(cardTypeId, $"order {order} {argument}",
                spec => spec.Target == CommandTarget.Order && spec.Order == order && spec.Argument == argument);

        private CommandSpec Find(int cardTypeId, string what, Func<CommandSpec, bool> matches)
        {
            CommandSpec general = null;
            foreach (var spec in specs)
            {
                if (spec == null || !matches(spec))
                {
                    continue;
                }

                if (spec.Card == null)
                {
                    general = spec;
                }
                else if (spec.Card.TypeId == cardTypeId)
                {
                    return spec;
                }
            }

            if (general == null)
            {
                Debug.LogError($"HyperRTS: the command catalog '{name}' has no spec for {what}.", this);
            }

            return general;
        }
    }
}
