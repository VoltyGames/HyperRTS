// A VFX Graph Initialize block for streams: like Place At Burst, at a random point of this frame's path from one
// stream position to the next.
#include "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/PoseAtOrigin.hlsl"

void PlaceAlongPath(inout VFXAttributes attributes, in float3 from, in float3 to, in float3 normal, in float3 forward, in float3 scale)
{
    PoseAtOrigin(lerp(from, to, VFXRAND), normal, forward, scale,
        attributes.position, attributes.velocity, attributes.size, attributes.targetPosition);
}
