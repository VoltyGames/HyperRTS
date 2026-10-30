// Six-way light map sampling and shading from the sun, clustered lights and the ambient probe, shared by the particle
// shader and the effect graph output. Needs URP Lighting.hlsl, SceneLighting.hlsl and SceneFog.hlsl first.
#ifndef HYPERRTS_SIX_WAY_LIGHTING_INCLUDED
#define HYPERRTS_SIX_WAY_LIGHTING_INCLUDED

// The six light maps of one pixel, already blended between flipbook frames.
struct SixWayMaps
{
    half3 positive; // lit from +X right, +Y top, -Z back
    half3 negative; // lit from -X left, -Y bottom, +Z front
};

// Both maps blended between two flipbook frames; positive alpha is the opacity and negative alpha the emission.
SixWayMaps SampleSixWayMaps(TEXTURE2D_PARAM(positiveMap, positiveSampler), TEXTURE2D_PARAM(negativeMap, negativeSampler),
    float2 current, float2 next, half frameBlend, out half opacity, out half emission)
{
    half4 positive = lerp(SAMPLE_TEXTURE2D(positiveMap, positiveSampler, current),
        SAMPLE_TEXTURE2D(positiveMap, positiveSampler, next), frameBlend);
    half4 negative = lerp(SAMPLE_TEXTURE2D(negativeMap, negativeSampler, current),
        SAMPLE_TEXTURE2D(negativeMap, negativeSampler, next), frameBlend);
    SixWayMaps maps;
    maps.positive = positive.rgb;
    maps.negative = negative.rgb;
    opacity = positive.a;
    emission = negative.a;
    return maps;
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

// Sky, ground and the mean horizon from the probe.
void SixWayAmbientColors(out half3 up, out half3 down, out half3 side)
{
    up = SampleSH(half3(0.0h, 1.0h, 0.0h));
    down = SampleSH(half3(0.0h, -1.0h, 0.0h));
    side = 0.25h * (SampleSH(half3(1.0h, 0.0h, 0.0h)) + SampleSH(half3(-1.0h, 0.0h, 0.0h))
        + SampleSH(half3(0.0h, 0.0h, 1.0h)) + SampleSH(half3(0.0h, 0.0h, -1.0h)));
}

// The maps lit from world up and down take the sky and ground; the other four directions share the horizon colour.
half3 SixWayAmbient(half3 ambientUp, half3 ambientDown, half3 ambientSide, half3x3 basis, SixWayMaps maps)
{
    half3 up = mul(basis, half3(0.0h, 1.0h, 0.0h));
    half fromUp = SixWayResponse(up, maps);
    half fromDown = SixWayResponse(-up, maps);
    half total = dot(maps.positive + maps.negative, half3(1.0h, 1.0h, 1.0h));
    return ambientUp * fromUp + ambientDown * fromDown + ambientSide * (total - fromUp - fromDown);
}

// Hot cores ramp toward the ramp's right end (white-yellow), cooling edges toward its left (red).
half3 SixWayGlow(TEXTURE2D_PARAM(ramp, rampSampler), half3 color, half emissionMap, half heat)
{
    half emission = saturate(emissionMap * heat);
    return color * SAMPLE_TEXTURE2D_LOD(ramp, rampSampler, float2(emission, 0.5), 0).rgb * emission;
}

// Premultiplied alpha: the lit smoke is fogged and scaled by its alpha, the glow adds on top and dims in fog.
half4 SixWayShade(SixWayMaps maps, float3 positionWS, float2 screenUV, half3x3 basis, half3 ambient, half3 tint,
    half alpha, half3 glow, SceneFog fog)
{
    half3 light = SixWayMainLight(positionWS, basis, maps) + SixWayAdditionalLights(positionWS, screenUV, basis, maps) + ambient;
    half3 smoke = ApplySceneFog(tint * light, fog) * alpha;
    return half4(smoke + glow * ((1.0h - fog.atmosphere.a) * (1.0h - fog.war.a)), alpha);
}

#endif
