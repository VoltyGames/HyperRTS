using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
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
            ComponentType.ReadOnly<BuildingTag>(), ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<LocalTransform>()));

        private bool _done;
        private float _deadline = float.NaN;

        /// <summary>
        /// The base centre the first time it is found, looking for <paramref name="searchSeconds"/> after the local
        /// player exists; false before that, after it was given or the search ended, and when <paramref name="enabled"/> is off.
        /// </summary>
        public bool TryFindOnce(bool enabled, float searchSeconds, out Vector3 centre)
        {
            centre = default;
            if (_done || !enabled)
            {
                return false;
            }

            if (!DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return false;
            }

            if (!TryGetLocalOwner(entityManager, out var owner))
            {
                return false;
            }

            if (float.IsNaN(_deadline))
            {
                _deadline = Time.unscaledTime + searchSeconds;
            }

            var found = TryFind(entityManager, owner, out centre);
            _done = found || Time.unscaledTime >= _deadline;
            return found;
        }

        // False until the local player exists (the map is still streaming in, or a network join is pending).
        private bool TryGetLocalOwner(EntityManager entityManager, out byte owner)
        {
            var found = _localPlayer.In(entityManager).TryGetSingleton(out Player local);
            owner = local.Owner;
            return found;
        }

        // False when the owner has no buildings.
        private bool TryFind(EntityManager entityManager, byte owner, out Vector3 centre)
        {
            centre = default;
            var query = _buildings.In(entityManager);
            using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var sum = float3.zero;
            var count = 0;
            for (var i = 0; i < owners.Length; i++)
            {
                if (owners[i].Value == owner)
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
