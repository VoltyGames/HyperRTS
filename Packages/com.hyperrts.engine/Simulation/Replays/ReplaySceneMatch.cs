using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Pairs recorded entities with the scene's: same type, nearest position, each scene entity used once.</summary>
    public static class ReplaySceneMatch
    {
        /// <summary>Fills <paramref name="live"/> (key → scene entity) and lists the scene entities left unclaimed.</summary>
        public static void Match(NativeHashMap<int, ReplayEntity> recorded, NativeArray<Entity> entities,
            NativeArray<EntityInfo> infos, NativeArray<LocalTransform> transforms, NativeHashMap<int, Entity> live,
            NativeList<Entity> unclaimed)
        {
            var claimed = new NativeArray<bool>(entities.Length, Allocator.Temp);
            foreach (var pair in recorded)
            {
                var match = Nearest(pair.Value, entities, infos, transforms, claimed);
                if (match >= 0)
                {
                    claimed[match] = true;
                    live[pair.Key] = entities[match];
                }
            }

            for (var i = 0; i < entities.Length; i++)
            {
                if (!claimed[i])
                {
                    unclaimed.Add(entities[i]);
                }
            }

            claimed.Dispose();
        }

        private static int Nearest(in ReplayEntity recorded, NativeArray<Entity> entities, NativeArray<EntityInfo> infos,
            NativeArray<LocalTransform> transforms, NativeArray<bool> claimed)
        {
            var closest = Closest.None;
            for (var i = 0; i < entities.Length; i++)
            {
                if (claimed[i] || infos[i].TypeId != recorded.TypeId)
                {
                    continue;
                }

                closest.Consider(i, entities[i], math.distancesq(transforms[i].Position, recorded.Position));
            }

            return closest.Index;
        }
    }
}
