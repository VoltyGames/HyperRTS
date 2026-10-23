using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A details card that the panel owning it anchors: a title, then the lines, stats and costs the hovered or focused
    /// <see cref="ITooltipSource"/> writes. It never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class HUDTooltip : VisualElement
    {
        // One delegate each, so tracking a reused button again finds them registered and adds nothing.
        private readonly EventCallback<PointerEnterEvent> _onEnter;
        private readonly EventCallback<PointerLeaveEvent> _onLeave;
        private readonly EventCallback<FocusInEvent> _onFocusIn;
        private readonly EventCallback<FocusOutEvent> _onFocusOut;

        private ITooltipSource _hovered;
        private ITooltipSource _focused;
        private ITooltipSource _shown;
        private int _version;

        public HUDTooltip()
        {
            _onEnter = evt => _hovered = evt.currentTarget as ITooltipSource;
            _onLeave = evt => Leave(ref _hovered, evt.currentTarget as ITooltipSource);
            _onFocusIn = evt => _focused = evt.currentTarget as ITooltipSource;
            _onFocusOut = evt => Leave(ref _focused, evt.currentTarget as ITooltipSource);
            AddToClassList("hud-tooltip");
            pickingMode = PickingMode.Ignore;
            this.SetHidden(true);
        }

        /// <summary>Shows <paramref name="source"/>'s details while the pointer is over it or it has focus.</summary>
        public void Track<T>(T source) where T : VisualElement, ITooltipSource
        {
            source.RegisterCallback(_onEnter);
            source.RegisterCallback(_onLeave);
            source.RegisterCallback(_onFocusIn);
            source.RegisterCallback(_onFocusOut);
        }

        /// <summary>Drops every tracked source: their elements are gone and will never send leave events.</summary>
        public void Forget()
        {
            _hovered = null;
            _focused = null;
            _shown = null;
            Clear();
            this.SetHidden(true);
        }

        /// <summary>Refills when the hovered source, else the focused one, or what it shows changes.</summary>
        public void Refresh()
        {
            var source = _hovered ?? _focused;
            var stale = source != null && source.TooltipVersion != _version;
            if (source == _shown && !stale)
            {
                return;
            }

            Clear();
            _shown = source;
            _version = source?.TooltipVersion ?? 0;
            var filled = source != null && source.Describe(this);
            this.SetHidden(!filled);
        }

        public void AddTitle(string text) => AddLabel(text, "hud-tooltip__title", this);

        public void AddLine(string text) => AddLabel(text, "hud-tooltip__line", this);

        /// <summary>A line in the warning colour, for what stops the action.</summary>
        public void AddWarning(string text) =>
            AddLabel(text, "hud-tooltip__line", this).AddToClassList("hud-tooltip__line--warning");

        public void AddStat(string name, string value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("hud-tooltip__stat");
            AddLabel(name, "hud-tooltip__stat-name", row);
            AddLabel(value, "hud-tooltip__stat-value", row);
            Add(row);
        }

        public void AddCosts(DynamicBuffer<ResourceCost> costs)
        {
            var row = new CostRow { pickingMode = PickingMode.Ignore };
            row.AddToClassList("hud-tooltip__costs");
            row.Bind(costs);
            Add(row);
        }

        private static Label AddLabel(string text, string className, VisualElement parent)
        {
            var label = HUDElements.Text(text, className, parent);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private static void Leave(ref ITooltipSource tracked, ITooltipSource source)
        {
            if (tracked == source)
            {
                tracked = null;
            }
        }
    }
}
