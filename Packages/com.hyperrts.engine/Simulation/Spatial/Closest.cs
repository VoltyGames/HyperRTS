using Unity.Entities;

namespace HyperRTS.Simulation.Spatial
{
    /// <summary>
    /// Running nearest-candidate search. Ties go to the lower entity index, so every machine picks the same entity
    /// whatever order its queries return.
    /// </summary>
    public struct Closest
    {
        /// <summary>The candidate's index in the searched collection; negative until one is offered.</summary>
        public int Index;

        public Entity Entity;

        /// <summary>The measure being minimised, usually a squared distance.</summary>
        public float Distance;

        public readonly bool Found => Index >= 0;

        /// <summary>No candidate yet; only ones nearer than <paramref name="limit"/> are taken.</summary>
        public static Closest Within(float limit) => new() { Index = -1, Entity = Entity.Null, Distance = limit };

        public static Closest None => Within(float.MaxValue);

        public static bool IsCloser(float distance, Entity entity, float bestDistance, Entity best) =>
            distance < bestDistance || (distance == bestDistance && entity.Index < best.Index);

        public readonly bool IsBeatenBy(float distance, Entity entity) => IsCloser(distance, entity, Distance, Entity);

        /// <summary>Takes the candidate when it is closer; true if it did.</summary>
        public bool Offer(int index, Entity entity, float distance)
        {
            if (!IsBeatenBy(distance, entity))
            {
                return false;
            }

            Index = index;
            Entity = entity;
            Distance = distance;
            return true;
        }
    }
}
