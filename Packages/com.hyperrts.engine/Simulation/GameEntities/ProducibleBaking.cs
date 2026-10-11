using System.Collections.Generic;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Bakes the inspector cost and prerequisite lists shared by game entities and upgrades.</summary>
    public static class ProducibleBaking
    {
        public static List<ResourceCost> Costs(IEnumerable<ResourceQuantity> quantities)
        {
            var costs = new List<ResourceCost>();
            foreach (var quantity in quantities)
            {
                if (quantity.type != null)
                {
                    costs.Add(new ResourceCost { Type = quantity.type, Amount = quantity.amount });
                }
            }

            return costs;
        }

        /// <summary>Rebakes the owner when a required type is renamed, since only its type id is kept.</summary>
        public static List<Prerequisite> Prerequisites(IBaker baker, IEnumerable<ProducibleAuthoring> types)
        {
            var required = new List<Prerequisite>();
            foreach (var type in types)
            {
                if (type != null)
                {
                    baker.DependsOn(type);
                    required.Add(new Prerequisite { TypeId = type.TypeId });
                }
            }

            return required;
        }
    }
}
