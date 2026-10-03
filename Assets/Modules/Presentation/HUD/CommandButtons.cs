using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Command card buttons: prefab buttons show icon, name and cost; plain ones show a caption.</summary>
    public static class CommandButtons
    {
        public static Button ForPrefab(EntityManager entityManager, Entity prefab, Action onClick)
        {
            var button = Plain("", onClick);
            button.Add(HudElements.EntityIcon(entityManager, prefab, "hud-command__icon"));

            var name = entityManager.HasComponent<EntityInfo>(prefab)
                ? entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString()
                : "?";
            HudElements.Text(name, "hud-command__name", button);
            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                button.Add(HudElements.Costs(entityManager.GetBuffer<ResourceCost>(prefab, true)));
            }

            return button;
        }

        public static Button Plain(string caption, Action onClick)
        {
            var button = new Button(onClick) { text = caption };
            button.AddToClassList("hud-command");
            return button;
        }
    }
}
