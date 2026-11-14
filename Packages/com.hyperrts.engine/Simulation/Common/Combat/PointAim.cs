using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Combat
{
    /// <summary>
    /// A ground point the primary weapon is ordered to fire a salvo at, with no entity to aim at. Enabled while a
    /// fire mission is in range and firing.
    /// </summary>
    public struct PointAim : IComponentData, IEnableableComponent
    {
        public float3 Value;

        /// <summary>The weapon's completed salvos when the mission began; one more means its salvo has fired.</summary>
        public uint SalvosAtStart;
    }
}
