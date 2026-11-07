using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Trampling
{
    /// <summary>
    /// Dents the terrain under this building's footprint once it is built, as a ditch along its long side. Clients
    /// only: the simulation's ground never changes. Models and cover spots come from the prefab as for any building.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Environment + "Trench")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(Simulation.Buildings.BuildingAuthoring), "a Building")]
    public class TrenchAuthoring : AuthoringBehaviour
    {
        [Tooltip("Metres the ditch is dug into the ground, at most the trample maps' crater depth.")]
        [Range(0.1f, TrampleStamps.MaxCraterDepth)]
        public float depth = 0.5f;

        public class Baker : Baker<TrenchAuthoring>
        {
            public override void Bake(TrenchAuthoring authoring) =>
                this.Writer().Add(new Trench { Depth = authoring.depth });
        }
    }
}
