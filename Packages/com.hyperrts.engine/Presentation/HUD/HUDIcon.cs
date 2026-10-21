using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>An icon texture, or the name's initials on a plain tile when there is none.</summary>
    [UxmlElement]
    public partial class HUDIcon : VisualElement
    {
        private readonly Label _initials;

        public HUDIcon()
        {
            AddToClassList("hud-icon");
            _initials = HUDElements.Text("", "hud-icon__initials", this);
        }

        public void Bind(Texture2D texture, string name)
        {
            var hasTexture = texture != null;
            style.backgroundImage = hasTexture ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.Null);
            _initials.text = hasTexture ? "" : Initials(name);
            _initials.SetHidden(hasTexture);
        }

        /// <summary>Shows a unit or building prefab or instance, from its <see cref="EntityInfo"/>.</summary>
        public void Bind(EntityManager entityManager, Entity entity)
        {
            var info = entityManager.GetComponentData<EntityInfo>(entity);
            Bind(info.Icon.Value, HUDText.EntityName(info.Name.ToString()));
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
