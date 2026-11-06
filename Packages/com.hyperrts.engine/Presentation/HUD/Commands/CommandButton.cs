using System;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// A command card button built from CommandButton.uxml. Its parts stay hidden until a source shows them, and
    /// refusals show as classes and badges.
    /// </summary>
    public sealed class CommandButton : Button, ITooltipSource
    {
        private readonly Action _onClick;
        private readonly HUDIcon _icon;
        private readonly Label _name;
        private readonly HUDGlyph _chevron;
        private readonly Label _caption;
        private readonly HUDSweep _sweep;
        private readonly Label _count;
        private readonly HUDGlyph _unpowered;
        private readonly VisualElement _strip;
        private readonly CostRow _costs;
        private readonly Label _limit;
        private readonly Label _cooldown;
        private Action<HUDTooltip> _describe;
        private PurchaseRefusal _refusal;
        private SquadRefusal _squadRefusal;
        private int _shownCount = -1;
        private int _shownLimitCount = -1;
        private int _shownLimitMax = -1;
        private int _shownSeconds = -1;

        public CommandButton(VisualTreeAsset parts, CommandLook look, string label, Texture2D icon, string hotkey,
            Action onClick)
        {
            _onClick = onClick;
            AddToClassList("hud-command");
            clicked += Press;
            parts.CloneTree(this);
            _icon = this.Require<HUDIcon>("icon");
            _name = this.Require<Label>("name");
            _chevron = this.Require<HUDGlyph>("chevron");
            _caption = this.Require<Label>("caption");
            _sweep = this.Require<HUDSweep>("sweep");
            _count = this.Require<Label>("count");
            _unpowered = this.Require<HUDGlyph>("unpowered");
            _strip = this.Require<VisualElement>("strip");
            _costs = this.Require<CostRow>("costs");
            _limit = this.Require<Label>("limit");
            _cooldown = this.Require<Label>("cooldown");
            ShowLook(look, label, icon);
            var key = this.Require<Label>("hotkey");
            key.text = hotkey;
            key.SetHidden(hotkey.Length == 0);
            Description = tooltip => tooltip.AddTitle(label);
        }

        /// <summary>True while a purchase rule or squad order refuses it: it still shows its tooltip but ignores presses.</summary>
        public bool IsRefused => _refusal != PurchaseRefusal.None || _squadRefusal != SquadRefusal.None;

        /// <summary>Enabled and not refused, so a press or its hotkey acts.</summary>
        public bool CanPress => enabledInHierarchy && !IsRefused;

        public int TooltipVersion { get; private set; }

        /// <summary>Writes the tooltip's content.</summary>
        public Action<HUDTooltip> Description
        {
            set
            {
                _describe = value;
                TooltipVersion++;
            }
        }

        public bool Describe(HUDTooltip tooltip)
        {
            _describe(tooltip);
            return true;
        }

        /// <summary>Puts a chevron over the caption, for the page cards.</summary>
        public void ShowChevron(HUDGlyphShape shape)
        {
            _chevron.Shape = shape;
            _chevron.SetHidden(false);
        }

        /// <summary>Lists the prefab's cost along the bottom.</summary>
        public void ShowPrice(DynamicBuffer<ResourceCost> costs)
        {
            _costs.Bind(costs);
            _strip.SetHidden(false);
        }

        /// <summary>
        /// Applies a purchase rule's verdict as <c>hud-command--unaffordable</c>, <c>--locked</c>, <c>--maxed</c> or
        /// <c>--researched</c>.
        /// </summary>
        public void ShowRefusal(PurchaseRefusal refusal)
        {
            if (refusal == _refusal)
            {
                return;
            }

            _refusal = refusal;
            TooltipVersion++;
            EnableInClassList("hud-command--unaffordable", refusal == PurchaseRefusal.CannotAfford);
            EnableInClassList("hud-command--locked", refusal == PurchaseRefusal.MissingPrerequisite);
            EnableInClassList("hud-command--maxed", refusal == PurchaseRefusal.AtLimit);
            EnableInClassList("hud-command--researched", refusal == PurchaseRefusal.Researched);
            _costs.MarkShort(refusal == PurchaseRefusal.CannotAfford);
        }

        /// <summary>
        /// Applies a squad order's verdict as <c>hud-command--no-point</c>, <c>--full-strength</c> or
        /// <c>--too-poor</c>.
        /// </summary>
        public void ShowSquadRefusal(SquadRefusal refusal)
        {
            if (refusal == _squadRefusal)
            {
                return;
            }

            _squadRefusal = refusal;
            TooltipVersion++;
            EnableInClassList("hud-command--no-point", refusal == SquadRefusal.NoPoint);
            EnableInClassList("hud-command--full-strength", refusal == SquadRefusal.FullStrength);
            EnableInClassList("hud-command--too-poor", refusal == SquadRefusal.TooPoor);
        }

        /// <summary>The queued count badge, and the sweep while one is in work.</summary>
        public void ShowQueue(QueuedOption queued)
        {
            if (queued.Count != _shownCount)
            {
                _shownCount = queued.Count;
                _count.text = queued.Count.ToString();
            }

            _count.SetHidden(queued.Count == 0);
            _sweep.SetHidden(!queued.InWork);
            EnableInClassList("hud-command--queued", queued.Count > 0);
            if (queued.InWork)
            {
                _sweep.Fraction = queued.Progress;
            }
        }

        public void ShowUnpowered(bool unpowered) => _unpowered.SetHidden(!unpowered);

        /// <summary>"count/max" for a build-limited type; a max of 0 hides it.</summary>
        public void ShowLimit(int count, int max)
        {
            _limit.SetHidden(max == 0);
            var changed = count != _shownLimitCount || max != _shownLimitMax;
            if (max > 0 && changed)
            {
                _shownLimitCount = count;
                _shownLimitMax = max;
                _limit.text = $"{count}/{max}";
            }
        }

        /// <summary>Whole seconds left before it can be used again; 0 hides the countdown.</summary>
        public void ShowCooldown(int seconds)
        {
            if (seconds == _shownSeconds)
            {
                return;
            }

            _shownSeconds = seconds;
            _cooldown.text = seconds.ToString();
            _cooldown.SetHidden(seconds == 0);
        }

        /// <summary>What a click does: nothing while a rule refuses it.</summary>
        public void Press()
        {
            if (!IsRefused)
            {
                _onClick();
            }
        }

        private void ShowLook(CommandLook look, string label, Texture2D icon)
        {
            switch (look)
            {
                case CommandLook.Picture:
                    AddToClassList("hud-command--picture");
                    _icon.Bind(icon, label);
                    _icon.SetHidden(false);
                    _name.text = label;
                    _name.SetHidden(icon != null);
                    return;
                case CommandLook.Page:
                    AddToClassList("hud-command--page");
                    break;
                default:
                    AddToClassList("hud-command--text");
                    break;
            }

            _caption.text = label;
            _caption.SetHidden(false);
        }
    }
}
