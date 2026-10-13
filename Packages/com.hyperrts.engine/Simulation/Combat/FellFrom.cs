using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>The fall heading (<see cref="ToppleMath"/>) a <see cref="Topple"/> piece's lethal hit gave it.</summary>
    public struct FellFrom : IComponentData
    {
        public byte Heading;
    }
}
