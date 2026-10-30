// A VFX Graph Update block: turns particles about the vertical through their spawn point (targetPosition), as
// Shuriken's orbital velocity swirls a dust devil. Each particle keeps one rate between the ends, in degrees a second.
void OrbitSpawnPoint(inout VFXAttributes attributes, in float2 rate, in float deltaTime)
{
    float angle = radians(lerp(rate.x, rate.y, FixedRand(attributes.particleId * 747796405u))) * deltaTime;
    float2 offset = attributes.position.xz - attributes.targetPosition.xz;
    float s = sin(angle);
    float c = cos(angle);
    attributes.position.xz = attributes.targetPosition.xz + float2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);
}
