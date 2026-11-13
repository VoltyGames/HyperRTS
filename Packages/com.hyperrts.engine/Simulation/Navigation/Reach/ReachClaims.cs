using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Reach
{
    /// <summary>
    /// Where units working a shared target stand or are heading, so each walks to a spot of its own. Gathered each
    /// frame by the system that runs those orders.
    /// </summary>
    public struct ReachClaims
    {
        /// <summary>A unit's spot at a target: its goal while it walks there, else where it stands.</summary>
        public struct Claim
        {
            public Entity Unit;
            public float2 Point;
        }

        /// <summary>Orders whose units don't share spots.</summary>
        public static ReachClaims None => default;

        private NativeParallelMultiHashMap<Entity, Claim> _spots;
        private Entity _unit;

        public ReachClaims(NativeParallelMultiHashMap<Entity, Claim> spots)
        {
            _spots = spots;
            _unit = Entity.Null;
        }

        /// <summary>These claims as seen by <paramref name="unit"/>, whose own claim never blocks it.</summary>
        public readonly ReachClaims For(Entity unit) => new() { _spots = _spots, _unit = unit };

        /// <summary>Whether another unit at <paramref name="target"/> stands within <paramref name="spacing"/> of the spot.</summary>
        public readonly bool IsTaken(Entity target, float2 spot, float spacing)
        {
            if (!_spots.IsCreated || !_spots.TryGetFirstValue(target, out var claim, out var iterator))
            {
                return false;
            }

            do
            {
                if (claim.Unit != _unit && math.distancesq(claim.Point, spot) < spacing * spacing)
                {
                    return true;
                }
            }
            while (_spots.TryGetNextValue(out claim, ref iterator));

            return false;
        }

        /// <summary>Records the unit's new spot, so a unit choosing later this frame takes another.</summary>
        public void Take(Entity target, float2 spot)
        {
            if (_spots.IsCreated)
            {
                _spots.Add(target, new Claim { Unit = _unit, Point = spot });
            }
        }
    }
}
