// A VFX Graph Initialize block: where a particle starts and how fast it flies, in rig space before Place At Burst.
// shape 0 is a cone from a disc, its tilt growing to spread degrees at the rim; 1 a half ball flying out; 2 a ring on
// the ground; 3 a whole ball flying out; 4 a box of half size radius flying up, tilted by up to spread degrees.
// rim 1 starts every particle on the disc's edge. shapeScale scales the start offset per axis, then the shape turns its
// +Y onto axis. ground is the bounce plane's height, kept in targetPosition.y for Place At Burst.
void EmitFromShape(inout VFXAttributes attributes, in float3 origin, in float shape, in float radius, in float spread, in float2 speed, in float rim, in float3 axis, in float3 shapeScale, in float ground)
{
    float turn = VFXRAND * 6.2831853;
    float2 around = float2(cos(turn), sin(turn));
    float reach = lerp(sqrt(VFXRAND), 1.0, rim);
    float3 offset = float3(around.x, 0.0, around.y) * reach * radius;
    float3 direction = float3(around.x, 0.0, around.y);
    if (shape < 0.5 || shape > 3.5)
    {
        float tilt = radians(spread) * (radius > 0.0 && shape < 0.5 ? reach : VFXRAND);
        direction = float3(around.x * sin(tilt), cos(tilt), around.y * sin(tilt));
        if (shape > 3.5)
        {
            offset = (float3(VFXRAND, VFXRAND, VFXRAND) * 2.0 - 1.0) * radius;
        }
    }
    else if (shape < 1.5 || shape > 2.5)
    {
        float up = shape < 1.5 ? VFXRAND : VFXRAND * 2.0 - 1.0;
        float side = sqrt(1.0 - up * up);
        direction = float3(around.x * side, up, around.y * side);
        offset = direction * radius * pow(VFXRAND, 1.0 / 3.0);
    }

    // Rodrigues' turn of +Y onto the axis; straight down is a half turn about X.
    float3 normal = normalize(axis);
    float3x3 onto = float3x3(1.0, 0.0, 0.0, 0.0, -1.0, 0.0, 0.0, 0.0, -1.0);
    if (normal.y > -0.9999)
    {
        float3 k = float3(normal.z, 0.0, -normal.x);
        float3x3 across = float3x3(0.0, -k.z, k.y, k.z, 0.0, -k.x, -k.y, k.x, 0.0);
        float3x3 along = float3x3(k.x * k, k.y * k, k.z * k) / (1.0 + normal.y);
        onto = float3x3(normal.y, 0.0, 0.0, 0.0, normal.y, 0.0, 0.0, 0.0, normal.y) + across + along;
    }

    attributes.position = origin + mul(onto, offset * shapeScale);
    attributes.velocity = mul(onto, direction) * lerp(speed.x, speed.y, VFXRAND);
    attributes.targetPosition = float3(0.0, ground, 0.0);
}
