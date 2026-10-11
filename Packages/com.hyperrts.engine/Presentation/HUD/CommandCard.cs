using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Actions for the owned part of the selection, gathered from <see cref="ICommandSource"/>s: by default building,
    /// training and research, abilities and support powers, stances, and return, unload and sell. Games add their own
    /// sources, whose entries can take grid slots and hotkeys.
    /// </summary>
    public sealed class CommandCard : HUDPanel
    {
        private readonly List<ICommandSource> _sources;
        private readonly List<CommandEntry> _entries = new();
        private readonly List<Entity> _owned = new();
        private int _hash = -1;

        /// <summary>The engine's sources, then <paramref name="extra"/>.</summary>
        public CommandCard(params ICommandSource[] extra) : base("hud-commands")
        {
            _sources = new List<ICommandSource>
            {
                new PurchaseCommands(), new AbilityCommands(), new StanceCommands(), new UnitActionCommands(),
            };
            _sources.AddRange(extra);
        }

        public override void Refresh(HUDContext context)
        {
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
            }

            foreach (var source in _sources)
            {
                source.Refresh(context);
            }

            PressHotkeys(context);
        }

        private void Rebuild(HUDContext context)
        {
            Root.Clear();
            _entries.Clear();
            _owned.Clear();
            foreach (var entity in context.Selected)
            {
                if (context.IsOwned(entity))
                {
                    _owned.Add(entity);
                }
            }

            foreach (var source in _sources)
            {
                source.Build(context, _owned, _entries);
            }

            // OrderBy is stable, so entries without a slot keep their sources' order after the slotted ones.
            foreach (var entry in _entries.OrderBy(entry => entry.Slot < 0 ? int.MaxValue : entry.Slot))
            {
                if (entry.Hotkey != '\0')
                {
                    HUDElements.Text(entry.Hotkey.ToString(), "hud-command__hotkey", entry.Button);
                }

                Root.Add(entry.Button);
            }

            Root.SetShown(Root.childCount > 0);
        }

        private void PressHotkeys(HUDContext context)
        {
            foreach (var entry in _entries)
            {
                var pressed = entry.Hotkey != '\0' && context.WasHotkeyPressed(entry.Hotkey);
                if (pressed && entry.Button.enabledInHierarchy)
                {
                    entry.Invoke();
                }
            }
        }
    }
}
