using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Base for the authoring of anything a producer makes (units, buildings, research): the type identity that
    /// prerequisites and production options refer to, and what producing it takes.
    /// </summary>
    public abstract class ProducibleAuthoring : AuthoringBehaviour
    {
        [Header("Identity")]
        [Tooltip("Name shown in the HUD; also the type's identity. Instances sharing a name are one type.")]
        public string displayName;

        [Tooltip("Icon for HUD buttons and the selection panel.")]
        public Texture2D icon;

        [Header("Production")]
        [Tooltip("Resources spent to produce, place or research this.")]
        public List<ResourceQuantity> cost = new();

        [Tooltip("Seconds to produce (units), construct (buildings) or research (upgrades).")]
        [Min(0f)]
        public float buildTime = 10f;

        [Tooltip("Completed buildings or finished research the owner needs before this can be produced.")]
        public List<ProducibleAuthoring> prerequisites = new();

        /// <summary>Name shown in the HUD; also the type's identity.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public int TypeId => EntityInfo.TypeIdFromName(DisplayName);

        private void Reset() => displayName = name;

        /// <summary>Bakes the identity, cost and prerequisites every producible carries.</summary>
        protected EntityInfo BakeProducible(IBaker baker, out List<ResourceCost> costs,
            out List<Prerequisite> required)
        {
            costs = new List<ResourceCost>();
            foreach (var quantity in cost)
            {
                if (quantity.type != null)
                {
                    costs.Add(new ResourceCost { Type = quantity.type, Amount = quantity.amount });
                }
            }

            required = new List<Prerequisite>();
            foreach (var type in prerequisites)
            {
                if (type != null)
                {
                    // Only the type id is kept, so renaming a required type must rebake this.
                    baker.DependsOn(type);
                    required.Add(new Prerequisite { TypeId = type.TypeId });
                }
            }

            var text = new FixedString64Bytes();
            text.CopyFromTruncated(DisplayName);
            return new EntityInfo { TypeId = TypeId, Name = text, Icon = icon };
        }
    }
}
