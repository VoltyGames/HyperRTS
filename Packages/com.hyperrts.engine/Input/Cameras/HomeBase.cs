using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>Finds the centre of the local player's buildings, where the camera opens a match.</summary>
    public sealed class HomeBase
    {
        private readonly LiveQuery _localPlayer = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<Player>()));

        private readonly LiveQuery _buildings = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<BuildingTag>(), ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>()));

        /// <summary>False until the local player exists (the map is still streaming in, or a network join is pending).</summary>
        public bool TryGetLocalFaction(EntityManager entityManager, out byte faction)
        {
            var found = _localPlayer.In(entityManager).TryGetSingleton(out Player local);
            faction = local.Faction;
            return found;
        }

        /// <summary>False when <paramref name="faction"/> owns no buildings.</summary>
        public bool TryFind(EntityManager entityManager, byte faction, out Vector3 centre)
        {
            centre = default;
            var query = _buildings.In(entityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var sum = float3.zero;
            var count = 0;
            for (var i = 0; i < factions.Length; i++)
            {
                if (factions[i].Value == faction)
                {
                    sum += transforms[i].Position;
                    count++;
                }
            }

            if (count == 0)
            {
                return false;
            }

            centre = sum / count;
            return true;
        }
    }
}
