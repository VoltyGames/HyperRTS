using Unity.Entities;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Seconds a thrown turret has been in the air; disabled once it lies on the ground.</summary>
    public struct TossClock : IComponentData, IEnableableComponent
    {
        public float Elapsed;
    }
}
