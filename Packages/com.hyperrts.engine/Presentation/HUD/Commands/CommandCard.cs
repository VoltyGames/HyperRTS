using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// Actions for the owned part of the selection, gathered from <see cref="ICommandSource"/>s. Each entry comes from a
    /// <see cref="CommandSpec"/> that gives its slot, page and hotkey; a taken slot moves to the next free one, spilling
    /// to the next page, whose last slot turns the page.
    /// </summary>
    public sealed class CommandCard : HUDPanel
    {
        public const int Slots = 15;

        private readonly CommandKit _kit;
        private readonly List<ICommandSource> _sources;
        private readonly List<CommandEntry> _entries = new();
        private readonly List<CommandButton> _turns = new();
        private readonly List<Entity> _owned = new();
        private readonly HUDTooltip _tooltip;
        private readonly VisualTreeAsset _blank;
        private int _hash = -1;
        private int _page = -1;
        private bool _cancel;

        /// <summary>The engine's sources, then <paramref name="extra"/>.</summary>
        public CommandCard(VisualElement root, HUDPanelLayouts layouts, CommandCatalogue catalogue,
            params ICommandSource[] extra) : base(root)
        {
            _kit = new CommandKit(layouts.CommandButton, catalogue);
            _blank = layouts.CommandBlank;
            _tooltip = new HUDTooltip(layouts.TooltipStat);
            _sources = new List<ICommandSource>
            {
                new PurchaseCommands(_kit), new AbilityCommands(_kit), new OrderCommands(_kit), new SquadCommands(_kit),
            };
            _sources.AddRange(extra);
            Root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        }

        public override void Refresh(HUDContext context)
        {
            if (context.SelectionHash != _hash)
            {
                _hash = context.SelectionHash;
                Rebuild(context);
                if (context.CommandPage != 0)
                {
                    context.ShowCommandPage(0);
                }
            }

            if (_cancel)
            {
                _cancel = false;
                context.View.CancelInteraction();
            }

            if (context.CommandPage != _page)
            {
                Show(context.CommandPage);
            }

            foreach (var entry in _entries)
            {
                entry.Button.SetEnabled(context.CanCommand);
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

            // OrderBy is stable, so specs sharing a slot keep their sources' order.
            _entries.AddRange(built.OrderBy(entry => entry.Spec.Page).ThenBy(entry => entry.Spec.Slot));
            var pages = Place(Slots);
            if (pages > 1)
            {
                pages = Place(Slots - 1);
            }

            foreach (var entry in _entries)
            {
                _tooltip.Track(entry.Button);
            }

            BuildTurns(context, pages);
            _page = -1;
        }

        // Gives each entry a free slot among the first `usable` of its page, spilling past the end to the next page.
        private int Place(int usable)
        {
            var taken = new HashSet<(int Page, int Slot)>();
            var pages = 1;
            foreach (var entry in _entries)
            {
                var page = entry.Spec.Page;
                var slot = entry.Spec.Slot;
                while (slot >= usable || taken.Contains((page, slot)))
                {
                    slot++;
                    if (slot >= usable)
                    {
                        page++;
                        slot = 0;
                    }
                }

                taken.Add((page, slot));
                entry.Page = page;
                entry.Slot = slot;
                pages = Math.Max(pages, page + 1);
            }

            return pages;
        }

        // Every page but the last has More, and the last has Back to the first.
        private void BuildTurns(HUDContext context, int pages)
        {
            _turns.Clear();
            if (pages == 1)
            {
                return;
            }

            for (var page = 0; page < pages; page++)
            {
                var more = page < pages - 1;
                var target = more ? page + 1 : 0;
                var turn = new CommandButton(_kit.ButtonParts, CommandLook.Page,
                    HUDText.Text(more ? "command.more" : "command.back"), null, "", () => context.ShowCommandPage(target));
                turn.ShowChevron(more ? HUDGlyphShape.ChevronRight : HUDGlyphShape.ChevronLeft);
                _turns.Add(turn);
            }
        }

        private void Show(int page)
        {
            _page = page;
            Root.Clear();
            _tooltip.Forget();
            var cells = new VisualElement[Slots];
            foreach (var entry in _entries.Where(entry => entry.Page == page))
            {
                cells[entry.Slot] = entry.Button;
            }

            if (_turns.Count > 0)
            {
                cells[Slots - 1] = _turns[page];
            }

            // Blank cells hold the slots before the last button in place.
            var last = Array.FindLastIndex(cells, cell => cell != null);
            for (var slot = 0; slot <= last; slot++)
            {
                Root.Add(cells[slot] ?? _blank.CloneRoot());
            }

            Root.SetInvisible(_entries.Count == 0);
            Root.Add(_tooltip);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == (int)MouseButton.RightMouse)
            {
                _cancel = true;
            }
        }

        // A key presses the one entry at that key on the shown page.
        private void PressHotkeys(HUDContext context)
        {
            var presses = context.Hotkeys;
            for (var i = 0; i < presses.Count; i++)
            {
                var key = presses.At(i);
                var entry = _entries.Find(other => other.Page == _page && other.Hotkey == key);
                if (entry != null && entry.Button.CanPress)
                {
                    entry.Invoke();
                }
            }
        }
    }
}
