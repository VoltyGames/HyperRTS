using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Actions for the owned part of the selection, gathered from <see cref="ICommandSource"/>s: by default building,
    /// training and research, abilities and support powers, stances, and return, unload and sell. Games add their own
    /// sources, whose entries can take grid slots and hotkeys. Hovering or focusing a button shows its tooltip above
    /// the card.
    /// </summary>
    public sealed class CommandCard : HUDPanel
    {
        private readonly List<ICommandSource> _sources;
        private readonly List<CommandEntry> _entries = new();
        private readonly List<Entity> _owned = new();
        private readonly HUDTooltip _tooltip = new();
        private int _hash = -1;

        /// <summary>The engine's sources, then <paramref name="extra"/>.</summary>
        public CommandCard(HUDPanelLayouts layouts, params ICommandSource[] extra) : base(layouts.CommandCard)
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

            _tooltip.Refresh();
            PressHotkeys(context);
        }

        private void Rebuild(HUDContext context)
        {
            Root.Clear();
            _tooltip.Forget();
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
                _tooltip.Track(entry.Button);
            }

            Root.SetInvisible(_entries.Count == 0);
            Root.Add(_tooltip);
        }

        private void PressHotkeys(HUDContext context)
        {
            var hotkeys = context.Hotkeys;
            if (hotkeys.Keys.IsEmpty)
            {
                return;
            }

            foreach (var entry in _entries)
            {
                var pressed = entry.Hotkey != '\0' && hotkeys.Contains(entry.Hotkey);
                if (pressed && entry.Button.CanPress)
                {
                    entry.Invoke();
                }
            }
        }
    }
}
