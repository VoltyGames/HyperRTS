using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>How deep a mover's footprint would sink into the walls of its grid; aircraft have none.</summary>
    internal struct WallProbe
    {
        private readonly float2 _position;
        private readonly float _radius;
        private NavAccess _access;
        private bool _hasWalls;

        public WallProbe(float2 position, float radius)
        {
            _position = position;
            _radius = radius;
            _access = default;
            _hasWalls = false;
            Reach = 0f;
        }

        /// <summary>How far the path runs before it turns, which bounds the look-ahead.</summary>
        public float Reach;

        public void Add(in NavAccess access)
        {
            _access = access;
            _hasWalls = true;
        }

        public readonly bool IsNear(float2 axis)
        {
            if (!_hasWalls)
            {
                return false;
            }

            var nearest = math.min(_access.Distance(_position - axis), _access.Distance(_position + axis));
            return nearest < _access.WallMargin;
        }

        /// <summary>The depth the footprint already sits in the walls.</summary>
        public readonly float Resting(float2 axis) => _hasWalls ? DepthAt(_position, axis) : 0f;

        // Only as far as the next corner, since the path turns there and a straight line on would cross its wall.
        // Only sinking deeper counts, so a unit already touching a wall may still slide along it.
        public readonly float Depth(float2 candidate, float2 axis, float time, float resting)
        {
            if (!_hasWalls)
            {
                return 0f;
            }

            var travel = math.min(math.length(candidate) * time, Reach);
            var center = _position + math.normalizesafe(candidate) * travel;
            return math.max(0f, DepthAt(center, axis) - resting);
        }

        private readonly float DepthAt(float2 center, float2 axis)
        {
            var open = _radius - NavTolerances.WallOverlap;
            return math.max(0f, open - _access.Distance(center - axis)) +
                   math.max(0f, open - _access.Distance(center + axis));
        }
    }
}
