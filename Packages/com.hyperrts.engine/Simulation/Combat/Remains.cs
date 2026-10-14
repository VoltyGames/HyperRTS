using Unity.Entities;
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
        public Felling Felling;
        public bool Felled;

        /// <summary><paramref name="fall"/> is read only for <see cref="Topple"/> pieces.</summary>
        public static Remains Of(Entity piece, in LocalTransform standing, byte fall, bool witnessed,
            ComponentLookup<PostTransformMatrix> stretches, ComponentLookup<PartStretch> partStretches,
            ComponentLookup<Topple> topples)
        {
            var remains = new Remains { Transform = standing };
            remains.Stretched = stretches.TryGetComponent(piece, out remains.Stretch);
            remains.PartsStretched = partStretches.TryGetComponent(piece, out remains.Parts);
            remains.Felled = topples.TryGetComponent(piece, out var topple);
            if (remains.Felled)
            {
                remains.Felling = ToppleMath.Fell(standing, topple, fall, witnessed, out remains.Transform);
            }

            return remains;
        }
    }
}
