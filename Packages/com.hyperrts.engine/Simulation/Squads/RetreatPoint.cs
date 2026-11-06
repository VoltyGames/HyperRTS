using Unity.Entities;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A place friendly squads retreat to; a squad is safe once it is within <see cref="Radius"/> of it.</summary>
    public struct RetreatPoint : IComponentData, ISquadPoint
    {
        public float Radius;

        public readonly float Reach => Radius;
    }
}
