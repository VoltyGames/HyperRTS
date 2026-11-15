// A VFX Graph Initialize block: moves a particle authored in rig space to the pose its Burst event carries.
// Wire the inputs to the source attributes position, direction, axisZ and scale.
#include "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/PoseAtOrigin.hlsl"

void PlaceAtBurst(inout VFXAttributes attributes, in float3 origin, in float3 normal, in float3 forward, in float3 scale)
{
    PoseAtOrigin(origin, normal, forward, scale,
        attributes.position, attributes.velocity, attributes.size, attributes.targetPosition);
}
