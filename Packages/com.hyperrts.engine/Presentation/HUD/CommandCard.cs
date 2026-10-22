using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Actions for the owned part of the selection, gathered from <see cref="ICommandSource"/>s: by default building,
    /// training and research, abilities and support powers, stances, and return, unload and sell. Games add their own
    /// sources, whose entries can take grid slots and hotkeys. Entries on the second page (a builder's advanced
    /// options) sit behind a More card in the last slot; Escape, a right click or a new selection turns back. Hovering
    /// or focusing a button shows its tooltip above the card.
    /// </summary>
    public sealed class CommandCard : HUDPanel
    {
        /// <summary>Five by three; the page card takes the last.</summary>
        public const int Slots = 15;

        private readonly List<ICommandSource> _sources;
        private readonly List<CommandEntry> _entries = new();
        private readonly List<Entity> _owned = new();
        private readonly HUDTooltip _tooltip = new();
        private CommandButton _more;
        private CommandButton _back;
        private int _hash = -1;
        private int _page = -1;
        private bool _turnBack;

        /// <summary>The engine's sources, then <paramref name="extra"/>.</summary>
        public CommandCard(HUDPanelLayouts layouts, params ICommandSource[] extra) : base(layouts.CommandCard)
        {
            _sources = new List<ICommandSource>
            {
                new PurchaseCommands(), new AbilityCommands(), new StanceCommands(), new UnitActionCommands(),
            };
            _sources.AddRange(extra);
            Root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        }

        public override void Refresh(HUDContext context)
        {
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                _turnBack = true;
                Rebuild(context);
            }

            if (_turnBack && context.CommandPage != 0)
            {
                context.ShowCommandPage(0);
            }

            _turnBack = false;
            if (context.CommandPage != _page)
            {
                Show(context.CommandPage);
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
            var built = new List<CommandEntry>();
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
                source.Build(context, _owned, built);
            }

            // OrderBy is stable, so entries without a slot keep their sources' order after the slotted ones.
            _entries.AddRange(built.OrderBy(entry => entry.Slot < 0 ? int.MaxValue : entry.Slot));
            foreach (var entry in _entries)
            {
                if (entry.Hotkey != '\0')
                {
                    HUDElements.Text(entry.Hotkey.ToString(), "hud-command__hotkey", entry.Button);
                }

                _tooltip.Track(entry.Button);
            }

            _more = null;
            _back = null;
            if (_entries.Exists(entry => entry.Page > 0))
            {
                _more = PageCard("command.more", HUDGlyphShape.ChevronRight, () => context.ShowCommandPage(1));
                _back = PageCard("command.back", HUDGlyphShape.ChevronLeft, () => context.ShowCommandPage(0));
            }

            _page = -1;
        }

        private void Show(int page)
        {
            _page = page;
            Root.Clear();
            _tooltip.Forget();
            var shown = 0;
            foreach (var entry in _entries.Where(entry => entry.Page == page))
            {
                Root.Add(entry.Button);
                shown++;
            }

            var turn = page == 0 ? _more : _back;
            if (turn != null)
            {
                // Blank slots keep the page card bottom right, where it is on both pages.
                for (; shown < Slots - 1; shown++)
                {
                    var blank = new VisualElement { pickingMode = PickingMode.Ignore };
                    blank.AddToClassList("hud-command");
                    blank.AddToClassList("hud-command--blank");
                    Root.Add(blank);
                }

                Root.Add(turn);
            }

            Root.SetInvisible(_entries.Count == 0);
            Root.Add(_tooltip);
        }

        private static CommandButton PageCard(string key, HUDGlyphShape chevron, System.Action turn) =>
            new CommandButton().BindPage(HUDText.Text(key), chevron, turn);

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == (int)MouseButton.RightMouse)
            {
                _turnBack = true;
            }
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
                var pressed = entry.Page == _page && entry.Hotkey != '\0' && hotkeys.Contains(entry.Hotkey);
                if (pressed && entry.Button.CanPress)
                {
                    entry.Invoke();
                }
            }
        }
    }
}
