using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>An icon texture, or the name's initials on a plain tile when there is none.</summary>
    public sealed class HUDIcon : VisualElement
    {
        public HUDIcon(Texture2D texture, string name, string className)
        {
            AddToClassList(className);
            AddToClassList("hud-icon");
            if (texture != null)
            {
                style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                HUDElements.Text(Initials(name), "hud-icon__initials", this);
            }
        }

        /// <summary>Icon of a unit or building prefab or instance, from its <see cref="EntityInfo"/>.</summary>
        public static HUDIcon Of(EntityManager entityManager, Entity entity, string className)
        {
            var info = entityManager.GetComponentData<EntityInfo>(entity);
            return new HUDIcon(info.Icon.Value, HUDText.EntityName(info.Name.ToString()), className);
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
