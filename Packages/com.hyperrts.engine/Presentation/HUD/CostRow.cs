using System.Collections.Generic;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One amount label per cost entry, coloured by resource type.</summary>
    [UxmlElement]
    public partial class CostRow : VisualElement
    {
        private readonly List<(Label Amount, ResourceType Type)> _amounts = new();
        private bool _short;

        public CostRow()
        {
            AddToClassList("hud-costs");
        }

        /// <summary>Shows <paramref name="count"/> times each cost, e.g. a whole line of segments.</summary>
        public void Bind(DynamicBuffer<ResourceCost> costs, int count = 1)
        {
            Clear();
            _amounts.Clear();
            foreach (var cost in costs)
            {
                var label = HUDElements.Text((cost.Amount * count).ToString(), "hud-costs__amount", this);
                _amounts.Add((label, cost.Type.Value));
                Paint(label, cost.Type.Value);
            }
        }

        /// <summary>Swaps the resource colours for the sheet's <c>hud-costs--short</c> one while the player can't pay.</summary>
        public void MarkShort(bool isShort)
        {
            if (isShort == _short)
            {
                return;
            }

            _short = isShort;
            EnableInClassList("hud-costs--short", isShort);
            foreach (var (amount, type) in _amounts)
            {
                Paint(amount, type);
            }
        }

        // Inline resource colours would beat the sheet, so a short row clears them.
        private void Paint(Label amount, ResourceType type)
        {
            var colored = type != null && !_short;
            amount.style.color = colored ? new StyleColor(type.color) : new StyleColor(StyleKeyword.Null);
        }
    }
}
