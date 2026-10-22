// Six-way light map shading from the sun, clustered lights and the ambient probe. Needs URP Lighting.hlsl and SceneLighting.hlsl first.
#ifndef HYPERRTS_SIX_WAY_LIGHTING_INCLUDED
#define HYPERRTS_SIX_WAY_LIGHTING_INCLUDED

// The six light maps of one pixel, already blended between flipbook frames.
struct SixWayMaps
{
    half3 positive; // lit from +X right, +Y top, -Z back
    half3 negative; // lit from -X left, -Y bottom, +Z front
};

// Rows are the particle basis: X right, Y up, Z toward the camera.
half3x3 SixWayBasis(SixWayVaryings input)
{
    return half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
}

// Response to a unit light from direction, in the particle basis.
// Z swaps maps because the positive map holds the back (-Z) and the negative map the front (+Z).
half SixWayResponse(half3 direction, SixWayMaps maps)
{
    half3 towardPositive = half3(maps.positive.rg, maps.negative.b);
    half3 towardNegative = half3(maps.negative.rg, maps.positive.b);
    half3 lit = lerp(towardNegative, towardPositive, step(0.0h, direction));
    return dot(direction * direction, lit);
}

half3 SixWayMainLight(float3 positionWS, half3x3 basis, SixWayMaps maps)
{
    Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
    half visible = sun.shadowAttenuation * sun.distanceAttenuation;
    return sun.color * (visible * SixWayResponse(mul(basis, sun.direction), maps));
}

// Unshadowed point and spot lights, so explosion flashes and fires light the smoke around them.
half3 SixWayAdditionalLights(float3 positionWS, float2 screenUV, half3x3 basis, SixWayMaps maps)
{
    half3 lighting = half3(0.0h, 0.0h, 0.0h);
    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData = LightLoopInput(positionWS, screenUV);
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, positionWS);
            lighting += light.color * (light.distanceAttenuation * SixWayResponse(mul(basis, light.direction), maps));
        LIGHT_LOOP_END
    #endif
    return lighting;
}

// Sky, ground and the mean horizon from the probe, evaluated per vertex.
void SixWayAmbientColors(out half3 up, out half3 down, out half3 side)
{
    up = SampleSH(half3(0.0h, 1.0h, 0.0h));
    down = SampleSH(half3(0.0h, -1.0h, 0.0h));
    side = 0.25h * (SampleSH(half3(1.0h, 0.0h, 0.0h)) + SampleSH(half3(-1.0h, 0.0h, 0.0h))
        + SampleSH(half3(0.0h, 0.0h, 1.0h)) + SampleSH(half3(0.0h, 0.0h, -1.0h)));
}

// The maps lit from world up and down take the sky and ground; the other four directions share the horizon colour.
half3 SixWayAmbient(SixWayVaryings input, half3x3 basis, SixWayMaps maps)
{
    half3 up = mul(basis, half3(0.0h, 1.0h, 0.0h));
    half fromUp = SixWayResponse(up, maps);
    half fromDown = SixWayResponse(-up, maps);
    half total = dot(maps.positive + maps.negative, half3(1.0h, 1.0h, 1.0h));
    half3 ambient = input.ambientUp * fromUp + input.ambientDown * fromDown + input.ambientSide * (total - fromUp - fromDown);
    return ambient * _AmbientStrength;
}

#endif
