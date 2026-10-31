// A VFX Graph Initialize block: moves a particle authored in rig space (+Y up, +Z forward, unscaled) to the pose its
// Burst event carries. targetPosition ends as the ground plane's point: the burst origin raised by the scaled
// height Emit From Shape left in its y. Wire the inputs to the source attributes position, direction, axisZ and scale.
void PlaceAtBurst(inout VFXAttributes attributes, in float3 origin, in float3 normal, in float3 forward, in float3 scale)
{
    float3 up = normalize(normal);
    float3 right = normalize(cross(up, forward));
    float3 ahead = cross(right, up);
    float3 offset = attributes.position * scale.x;
    attributes.position = origin + offset.x * right + offset.y * up + offset.z * ahead;
    float3 velocity = attributes.velocity * scale.x;
    attributes.velocity = velocity.x * right + velocity.y * up + velocity.z * ahead;
    attributes.size *= scale.x;
    attributes.targetPosition = origin + float3(0.0, attributes.targetPosition.y * scale.x, 0.0);
}
