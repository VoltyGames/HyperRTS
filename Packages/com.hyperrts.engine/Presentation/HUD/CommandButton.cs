using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Command card button: a caption, or a prefab's icon, name and cost.</summary>
    public sealed class CommandButton : Button
    {
        public CommandButton(string caption, Action onClick) : base(onClick)
        {
            text = caption;
            AddToClassList("hud-command");
        }

        public static CommandButton ForPrefab(EntityManager entityManager, Entity prefab, Action onClick)
        {
            var button = new CommandButton("", onClick);
            button.Add(HUDIcon.Of(entityManager, prefab, "hud-command__icon"));

            var name = HUDText.EntityName(entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString());
            HUDElements.Text(name, "hud-command__name", button);
            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                button.Add(new CostRow(entityManager.GetBuffer<ResourceCost>(prefab, true)));
            }

            return button;
        }
    }
}
