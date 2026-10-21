using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>What a destroyed piece's remains spawn with: its transform and stretches, lying along the fall if it topples.</summary>
    public struct Remains
    {
        public LocalTransform Transform;
        public PostTransformMatrix Stretch;
        public bool Stretched;
        public PartStretch Parts;
        public bool PartsStretched;
        public ToppleFall ToppleFall;
        public bool Felled;

        public float4x4 StretchMatrix => Stretched ? Stretch.Value : float4x4.identity;

        /// <summary><paramref name="fall"/> is read only for <see cref="Topple"/> pieces.</summary>
        public static Remains FromPiece(Entity piece, in LocalTransform standing, byte fall, bool witnessed,
            in RemainsLookups lookups)
        {
            var remains = new Remains { Transform = standing };
            remains.Stretched = lookups.Stretches.TryGetComponent(piece, out remains.Stretch);
            remains.PartsStretched = lookups.PartStretches.TryGetComponent(piece, out remains.Parts);
            remains.Felled = lookups.Topples.TryGetComponent(piece, out var topple);
            if (remains.Felled)
            {
                remains.ToppleFall = ToppleMath.Fell(standing, topple, fall, witnessed, out remains.Transform);
            }

            return remains;
        }
    }
}
