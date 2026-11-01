using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// Makes map scenery wreckable by anyone (fences, trees, wrecks, bridges). On a building or unit it only marks
    /// it destructible (its own health and death spawn apply); on its own it adds health, death and remains. Scenery
    /// placed in a SubScene is no ghost: multiplayer sends its state by <see cref="SceneryId"/>.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Destruction + "Destructible")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Destruction)]
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

        /// <summary>Adds the components of map scenery. A <paramref name="block"/> of zero leaves pathing open.</summary>
        public static void AddScenery<TWriter>(ref TWriter writer, float maxHealth, int crush, Entity remains,
            float2 block, SceneryId id) where TWriter : struct, IEntityWriter
        {
            writer.Add(new Health { Current = maxHealth, Max = maxHealth });
            writer.Add(new Faction { Value = Faction.Neutral });
            writer.Add<Dead>();
            writer.SetEnabled<Dead>(false);
            writer.Add<LastAttacker>();
            writer.Add(new Destructible { Crush = crush });
            writer.Add(id);
            if (remains != Entity.Null)
            {
                writer.Add(new SpawnOnDeath { Prefab = remains });
            }

            if (math.all(block > 0f))
            {
                NavSetup.AddObstacle(ref writer, block);
            }
        }

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
                AddScenery(ref writer, authoring.maxHealth, authoring.crushedBy, remains,
                    authoring.block, SceneryId.At(place.position, place.rotation));
            }
        }
    }
}
