using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Spatial;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Sums how far a unit must move to stop overlapping neighbouring units (half each, as both move). Units on
    /// layers that never share a surface (a ship under a bridge, a tank on it) ignore each other.
    /// </summary>
    internal struct SeparationVisitor : ISpatialVisitor
    {
        public Entity Self;
        public float2 Position;
        public float Radius;
        public NavLayer Layer;
        public float2 Push;

        public void Visit(in SpatialEntry entry)
        {
            if (entry.Entity == Self || !entry.IsUnit)
            {
                return;
            }

            if (!NavLayers.CanBlock(Layer, entry.Layer))
            {
                return;
            }

            var offset = Position - entry.Position.xz;
            var reach = Radius + entry.Radius;
            var distanceSq = math.lengthsq(offset);
            if (distanceSq >= reach * reach)
            {
                return;
            }

            var distance = math.sqrt(distanceSq);
            var direction = distance > 1e-4f ? offset / distance : TieBreak(entry.Entity);
            Push += direction * ((reach - distance) * 0.5f);
        }

        // Stacked units need opposite, deterministic directions: hash the pair, flip by index order.
        private readonly float2 TieBreak(Entity other)
        {
            var pair = new int2(math.min(Self.Index, other.Index), math.max(Self.Index, other.Index));
            math.sincos(math.hash(pair) * (math.PI * 2f / uint.MaxValue), out var sin, out var cos);
            return new float2(cos, sin) * (Self.Index < other.Index ? 1f : -1f);
        }
    }
}
