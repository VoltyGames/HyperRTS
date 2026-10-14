using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// How far a stretched prop's caps sit out from their rest place (root-space metres). Its remains copy it so
    /// their own <see cref="StretchParts"/> stretch to match.
    /// </summary>
    public struct PartStretch : IComponentData
    {
        public float Extension;
    }
}
