using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>Specs, catalogs and kits made in code for the command card tests; <see cref="Destroy"/> frees them.</summary>
    internal static class CommandTestKit
    {
        private static readonly List<Object> Made = new();

        /// <summary>A kit with one prefab spec per name, in the first slots.</summary>
        public static CommandKit Prefabs(params string[] names) => Kit(names.Select((name, slot) =>
        {
            var producible = new GameObject(name).AddComponent<UpgradeAuthoring>();
            Made.Add(producible.gameObject);
            return CommandSpec.Create(CommandTarget.Prefab, slot: slot, prefab: producible);
        }).ToArray());

        /// <summary>A kit with an order spec for each type; Retreat and Reinforce get R and T.</summary>
        public static CommandKit Orders(params CommandType[] orders) => Kit(orders.Select(order =>
        {
            return CommandSpec.Create(CommandTarget.Order, Hotkey(order),
                labelKey: $"command.{order.ToString().ToLowerInvariant()}", order: order);
        }).ToArray());

        private static Key Hotkey(CommandType order) => order switch
        {
            CommandType.Retreat => Key.R,
            CommandType.Reinforce => Key.T,
            _ => Key.None,
        };

        public static CommandKit Kit(params CommandSpec[] specs)
        {
            Made.AddRange(specs);
            var catalog = CommandCatalog.Create(specs);
            Made.Add(catalog);
            return new CommandKit(HUDPanelTests.Layouts.CommandButton, catalog);
        }

        /// <summary>A caption button of its own.</summary>
        public static CommandButton Button(string label) =>
            new(HUDPanelTests.Layouts.CommandButton, CommandLook.Text, label, null, "", () => { });

        public static void Destroy()
        {
            foreach (var made in Made)
            {
                Object.DestroyImmediate(made);
            }

            Made.Clear();
        }
    }
}
