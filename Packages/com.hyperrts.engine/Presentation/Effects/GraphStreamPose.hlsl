// A VFX Graph Initialize block for streams: moves a particle authored in rig space (+Y up, +Z forward, unscaled) to a
// random point of this frame's path from one stream position to the next, at the stream's pose. A Custom HLSL block
// sees only the attributes its own file names, so this repeats Place At Burst rather than calling it.
void PlaceAlongPath(inout VFXAttributes attributes, in float3 from, in float3 to, in float3 normal, in float3 forward, in float3 scale)
{
    float3 origin = lerp(from, to, VFXRAND);
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
