using Unity.Entities;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Seconds into the fall shown for felled remains; disabled once they rest in their final pose.</summary>
    public struct FallClock : IComponentData, IEnableableComponent
    {
        public float Time;
    }
}
