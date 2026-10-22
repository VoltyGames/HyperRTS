using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Command card button: a caption, or a picture card (an ability's or prefab's icon filling the button, a prefab's
    /// cost along the bottom) whose name and details are in the tooltip. Purchase states show as classes and badges.
    /// </summary>
    [UxmlElement]
    public partial class CommandButton : Button, ITooltipSource
    {
        private Action _onClick;
        private Action<HUDTooltip> _describe;
        private PurchaseRefusal _refusal;
        private CostRow _costs;
        private HUDSweep _sweep;
        private Label _count;
        private HUDGlyph _unpowered;
        private Label _limit;

        public CommandButton()
        {
            AddToClassList("hud-command");
            clicked += Press;
        }

        /// <summary>True while a purchase rule refuses it: it still shows its tooltip but ignores presses.</summary>
        public bool IsRefused => _refusal != PurchaseRefusal.None;

        /// <summary>Enabled and not refused, so a press or its hotkey acts.</summary>
        public bool CanPress => enabledInHierarchy && !IsRefused;

        public int TooltipVersion { get; private set; }

        /// <summary>Writes the tooltip's content; null for a button without one.</summary>
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
            _describe?.Invoke(tooltip);
            return _describe != null;
        }

        public CommandButton Bind(string caption, Action onClick)
        {
            text = caption;
            _onClick = onClick;
            return this;
        }

        public CommandButton Bind(Texture2D icon, string name, Action onClick)
        {
            _onClick = onClick;
            AddPicture(icon, name);
            Description = tooltip => tooltip.AddTitle(name);
            return this;
        }

        /// <summary>A build, train or research card; the caller sets its <see cref="Description"/>.</summary>
        public CommandButton BindPrefab(EntityManager entityManager, Entity prefab, Action onClick)
        {
            _onClick = onClick;
            AddPicture(entityManager.GetComponentData<EntityInfo>(prefab).Icon.Value,
                HUDText.EntityName(entityManager, prefab));
            _sweep = new HUDSweep();
            _sweep.AddToClassList("hud-command__sweep");
            Add(_sweep);
            var padlock = new HUDGlyph(HUDGlyphShape.Padlock);
            padlock.AddToClassList("hud-command__lock");
            Add(padlock);

            var badges = new VisualElement { pickingMode = PickingMode.Ignore };
            badges.AddToClassList("hud-command__badges");
            Add(badges);
            _count = HUDElements.Text("", "hud-command__count", badges);
            _unpowered = new HUDGlyph(HUDGlyphShape.Lightning);
            _unpowered.AddToClassList("hud-command__unpowered");
            badges.Add(_unpowered);

            var strip = new HUDFade();
            strip.AddToClassList("hud-command__strip");
            Add(strip);
            _costs = new CostRow { pickingMode = PickingMode.Ignore };
            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                _costs.Bind(entityManager.GetBuffer<ResourceCost>(prefab, true));
            }

            strip.Add(_costs);
            _limit = HUDElements.Text("", "hud-command__limit", strip);

            ShowQueue(new QueuedOption(0, QueuedOption.Idle));
            ShowUnpowered(false);
            ShowLimit(0, 0);
            return this;
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
            _costs?.MarkShort(refusal == PurchaseRefusal.CannotAfford);
        }

        /// <summary>The queued count badge, and the sweep while one is in work.</summary>
        public void ShowQueue(QueuedOption queued)
        {
            _count.text = queued.Count.ToString();
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
            _limit.text = max == 0 ? "" : $"{count}/{max}";
        }

        private void Press()
        {
            if (!IsRefused)
            {
                _onClick?.Invoke();
            }
        }

        // Without a texture the icon shows initials, so the name stays on the card to tell options apart.
        private void AddPicture(Texture2D icon, string name)
        {
            AddToClassList("hud-command--picture");
            var picture = new HUDIcon { pickingMode = PickingMode.Ignore };
            picture.AddToClassList("hud-command__icon");
            picture.Bind(icon, name);
            Add(picture);
            if (icon == null)
            {
                HUDElements.Text(name, "hud-command__name", this).pickingMode = PickingMode.Ignore;
            }
        }
    }
}
