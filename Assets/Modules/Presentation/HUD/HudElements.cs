using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Small builders for the HUD's recurring elements; styling lives in Hud.uss.</summary>
    public static class HudElements
    {
        public static VisualElement Box(string className, VisualElement parent = null)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            parent?.Add(element);
            return element;
        }

        public static Label Text(string text, string className, VisualElement parent = null)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent?.Add(label);
            return label;
        }

        /// <summary>The icon texture, or the name's initials on a plain tile when there is none.</summary>
        public static VisualElement Icon(Texture2D texture, string name, string className)
        {
            var icon = Box(className);
            icon.AddToClassList("hud-icon");
            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                Text(Initials(name), "hud-icon__initials", icon);
            }

            return icon;
        }

        /// <summary>A track with a fill child; set the fill width in percent.</summary>
        public static VisualElement Bar(string className, VisualElement parent, out VisualElement fill)
        {
            var track = Box(className, parent);
            track.AddToClassList("hud-bar");
            fill = Box("hud-bar__fill", track);
            return track;
        }

        public static void SetFraction(VisualElement fill, float fraction) =>
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);

        /// <summary>One coloured "amount" label per cost entry.</summary>
        public static VisualElement Costs(DynamicBuffer<ResourceCost> costs)
        {
            var row = Box("hud-costs");
            foreach (var cost in costs)
            {
                var label = Text(cost.Amount.ToString(), "hud-costs__amount", row);
                var type = cost.Type.Value;
                if (type != null)
                {
                    label.style.color = type.color;
                }
            }

            return row;
        }

        /// <summary>Removes the element from layout.</summary>
        public static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Hides the element but keeps its space, so neighbouring panels don't shift.</summary>
        public static void SetShown(VisualElement element, bool shown) =>
            element.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;

        /// <summary>Display name and icon of a unit or building prefab/instance.</summary>
        public static VisualElement EntityIcon(EntityManager entityManager, Entity entity, string className)
        {
            var info = entityManager.HasComponent<EntityInfo>(entity) ? entityManager.GetComponentData<EntityInfo>(entity) : default;
            return Icon(info.Icon.Value, info.Name.ToString(), className);
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1
                ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
                : name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
        }
    }
}
