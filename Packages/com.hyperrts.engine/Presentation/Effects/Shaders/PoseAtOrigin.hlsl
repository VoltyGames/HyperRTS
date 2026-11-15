// The pose both Graph pose blocks share: moves a rig-space particle (+Y up, +Z forward, unscaled) to origin.
// targetPosition ends as the ground plane's point: origin raised by the scaled height Emit From Shape left in its y.
void PoseAtOrigin(in float3 origin, in float3 normal, in float3 forward, in float3 scale,
    inout float3 position, inout float3 velocity, inout float size, inout float3 targetPosition)
{
    float3 up = normalize(normal);
    float3 right = normalize(cross(up, forward));
    float3 ahead = cross(right, up);
    float3 offset = position * scale.x;
    position = origin + offset.x * right + offset.y * up + offset.z * ahead;
    float3 moved = velocity * scale.x;
    velocity = moved.x * right + moved.y * up + moved.z * ahead;
    size *= scale.x;
    targetPosition = origin + float3(0.0, targetPosition.y * scale.x, 0.0);
}
