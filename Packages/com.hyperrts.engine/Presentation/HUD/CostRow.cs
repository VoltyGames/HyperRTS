using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One amount label per cost entry, coloured by resource type.</summary>
    [UxmlElement]
    public partial class CostRow : VisualElement
    {
        public CostRow()
        {
            AddToClassList("hud-costs");
        }

        public void Bind(DynamicBuffer<ResourceCost> costs)
        {
            Clear();
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
