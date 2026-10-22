using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Command card button: a caption, an ability's icon and name, or a prefab's icon, name and cost.</summary>
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

        public CommandButton Bind(Texture2D icon, string name, Action onClick)
        {
            clicked += onClick;
            AddIcon().Bind(icon, name);
            AddName(name);
            return this;
        }

        public CommandButton BindPrefab(EntityManager entityManager, Entity prefab, Action onClick)
        {
            clicked += onClick;
            AddIcon().Bind(entityManager, prefab);
            AddName(HUDText.EntityName(entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString()));
            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                var costs = new CostRow();
                costs.Bind(entityManager.GetBuffer<ResourceCost>(prefab, true));
                Add(costs);
            }

            return this;
        }

        private HUDIcon AddIcon()
        {
            var icon = new HUDIcon();
            icon.AddToClassList("hud-command__icon");
            Add(icon);
            return icon;
        }

        private void AddName(string name) => HUDElements.Text(name, "hud-command__name", this);
    }
}
