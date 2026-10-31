// A VFX Graph Update block: pushes particles along a smooth field of crossed waves that scrolls over time, so
// flames and embers wander. strength is metres a second, frequency waves a metre.
void StirWithNoise(inout VFXAttributes attributes, in float strength, in float frequency, in float scroll, in float time, in float deltaTime)
{
    float3 p = attributes.position * frequency + time * scroll;
    float3 push = float3(sin(p.y * 1.7 + cos(p.z * 2.3)), sin(p.z * 1.9 + cos(p.x * 1.3)), sin(p.x * 2.1 + cos(p.y * 1.1)));
    attributes.position += push * (strength * deltaTime);
}
