// A VFX Graph Initialize block: where a particle starts and how fast it flies, in rig space before Place At Burst.
// shape 0 is a cone from a disc, its tilt growing to spread degrees at the rim; 1 a half ball flying out; 2 a ring on the ground.
// rim 1 starts every particle on the disc's edge rather than inside it.
void EmitFromShape(inout VFXAttributes attributes, in float3 origin, in float shape, in float radius, in float spread, in float2 speed, in float rim)
{
    float turn = VFXRAND * 6.2831853;
    float2 around = float2(cos(turn), sin(turn));
    float reach = lerp(sqrt(VFXRAND), 1.0, rim);
    float3 offset = float3(around.x, 0.0, around.y) * reach * radius;
    float3 direction = float3(around.x, 0.0, around.y);
    if (shape < 0.5)
    {
        float tilt = radians(spread) * (radius > 0.0 ? reach : VFXRAND);
        direction = float3(around.x * sin(tilt), cos(tilt), around.y * sin(tilt));
    }
    else if (shape < 1.5)
    {
        float up = VFXRAND;
        float side = sqrt(1.0 - up * up);
        direction = float3(around.x * side, up, around.y * side);
        offset = direction * radius * pow(VFXRAND, 1.0 / 3.0);
    }

    attributes.position = origin + offset;
    attributes.velocity = direction * lerp(speed.x, speed.y, VFXRAND);
}
