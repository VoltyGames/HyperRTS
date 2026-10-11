using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Makes map scenery wreckable by anyone (fences, trees, wrecks, bridges). On a building or unit it only marks
    /// it destructible (its own health and death spawn apply); on its own it adds health, death and remains. Scenery
    /// placed in a SubScene is no ghost: multiplayer sends its state by <see cref="SceneryId"/>.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Destructible")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class DestructibleAuthoring : AuthoringBehaviour
    {
        [Tooltip("Smallest crusher size that flattens it by driving over it; 0 = never crushed.")]
        [Min(0)]
        public int crushedBy;

        [Tooltip("Scenery only (ignored on buildings and units): health.")]
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("Scenery only: what is left when it is destroyed (fallen tree, broken fence, ruin).")]
        public GameObject remains;

        [Tooltip("Scenery only: area (X by Z) blocked for pathing; zero leaves it open (fences, small trees).")]
        public Vector2 block;

        public class Baker : Baker<DestructibleAuthoring>
        {
            public override void Bake(DestructibleAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                if (GetComponent<GameEntityAuthoring>() != null)
                {
                    writer.Add(new Destructible { Crush = authoring.crushedBy });
                    return;
                }

                var remains = authoring.remains != null
                    ? GetEntity(authoring.remains, TransformUsageFlags.Dynamic)
                    : Entity.Null;
                var place = GetComponent<Transform>();
                DestructibleSetup.AddScenery(ref writer, authoring.maxHealth, authoring.crushedBy, remains,
                    authoring.block, SceneryId.At(place.position, place.rotation));
            }
        }
    }
}
