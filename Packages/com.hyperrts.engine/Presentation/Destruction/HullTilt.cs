using Unity.Entities;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Marks a wreck <c>HullTiltSystem</c> has yet to lay along the ground under its hull.</summary>
    public struct HullTilt : IComponentData, IEnableableComponent
    {
    }
}
