using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The nose-up pitch and right-side-up roll a wreck lies at, in radians: the slope of the ground under its hull.
    /// Enabled until <c>HullTiltSystem</c> has laid the wreck.
    /// </summary>
    public struct HullTilt : IComponentData, IEnableableComponent
    {
        public float2 Value;
    }
}
