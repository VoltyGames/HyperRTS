using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One amount label per cost entry, coloured by resource type.</summary>
    public sealed class CostRow : VisualElement
    {
        public CostRow(DynamicBuffer<ResourceCost> costs)
        {
            AddToClassList("hud-costs");
            foreach (var cost in costs)
            {
                var label = HUDElements.Text(cost.Amount.ToString(), "hud-costs__amount", this);
                var type = cost.Type.Value;
                if (type != null)
                {
                    label.style.color = type.color;
                }
            }
        }
    }
}
