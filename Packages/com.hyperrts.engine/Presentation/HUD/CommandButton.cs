using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Command card button: a caption, or a prefab's icon, name and cost.</summary>
    [UxmlElement]
    public partial class CommandButton : Button
    {
        public CommandButton()
        {
            AddToClassList("hud-command");
        }

        public CommandButton Bind(string caption, Action onClick)
        {
            text = caption;
            clicked += onClick;
            return this;
        }

        public CommandButton BindPrefab(EntityManager entityManager, Entity prefab, Action onClick)
        {
            Bind("", onClick);
            var icon = AddIcon();
            icon.Bind(entityManager, prefab);

            var name = HUDText.EntityName(entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString());
            HUDElements.Text(name, "hud-command__name", this);
            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                var costs = new CostRow();
                costs.Bind(entityManager.GetBuffer<ResourceCost>(prefab, true));
                Add(costs);
            }

            return this;
        }

        public void AddIcon(Texture2D texture, string name) => AddIcon().Bind(texture, name);

        private HUDIcon AddIcon()
        {
            var icon = new HUDIcon();
            icon.AddToClassList("hud-command__icon");
            Add(icon);
            return icon;
        }
    }
}
