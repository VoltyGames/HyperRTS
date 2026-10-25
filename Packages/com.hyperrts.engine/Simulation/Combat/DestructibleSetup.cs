using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds the components of destructible map scenery.</summary>
    public static class DestructibleSetup
    {
        /// <summary>A <paramref name="block"/> of zero leaves pathing open.</summary>
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

        /// <summary><paramref name="trunk"/> is a unit vector.</summary>
        public static void AddTopple<TWriter>(ref TWriter writer, float3 trunk, float height, float lie, float crown)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Topple { Trunk = trunk, Height = height, Lie = lie, Crown = crown });
            writer.Add<ToppleHeading>();
        }
    }
}
