using System;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Inspector pair of resource type and amount, for costs and starting stockpiles.</summary>
    [Serializable]
    public struct ResourceQuantity
    {
        [Tooltip("Resource type asset.")]
        public ResourceType type;

        [Tooltip("Amount of that resource.")]
        public int amount;
    }
}
