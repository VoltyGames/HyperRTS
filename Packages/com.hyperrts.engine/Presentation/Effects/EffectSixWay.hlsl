// The Shader Graph HyperRTS/EffectSixWay's one function: six-way smoke and fire for effect graphs, lit, fogged and
// glowing as ParticlesSixWay lights its particles. Frame is the particle's flipbook index, blending into the next.
#ifndef HYPERRTS_EFFECT_SIX_WAY
#define HYPERRTS_EFFECT_SIX_WAY

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Effects/FlipbookMotion.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneFog.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Effects/SixWayLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/EyeDepth.hlsl"

// The particle materials' camera fade, so smoke thins as the camera flies into it.
#define SIX_WAY_NEAR_FADE_START 1.0
#define SIX_WAY_NEAR_FADE_END 4.0

// One frame of a sheet whose frame 0 is top left, as VFX Graph and Shuriken number them.
float2 SixWayFrameUV(float frame, float2 uv, float2 sheet)
{
    float2 tile = float2(fmod(frame, sheet.x), sheet.y - 1.0 - floor(frame / sheet.x));
    return (tile + uv) / sheet;
}
#endif

void EffectSixWay_float(float2 UV, float3 WorldPosition, float3 Normal, float3 Tangent, float3 Bitangent,
    UnityTexture2D PositiveMap, UnityTexture2D NegativeMap, UnityTexture2D MotionMap, UnityTexture2D EmissionRamp,
    float2 FlipbookSize, float Frame, float3 ParticleColor, float ParticleAlpha, float Opacity, float AmbientStrength,
    float3 EmissionColor, float Heat, float SoftDistance, out float3 Color, out float Alpha)
{
#ifdef SHADERGRAPH_PREVIEW
    Color = ParticleColor;
    Alpha = ParticleAlpha;
#else
    float frame = floor(Frame);
    half frameBlend = Frame - frame;
    float2 current = SixWayFrameUV(frame, UV, FlipbookSize);
    float2 next = SixWayFrameUV(fmod(frame + 1.0, FlipbookSize.x * FlipbookSize.y), UV, FlipbookSize);
    FlipbookMotionWarp(TEXTURE2D_ARGS(MotionMap.tex, MotionMap.samplerstate), current, next, frameBlend);
    half opacity;
    half emissionMap;
    SixWayMaps maps = SampleSixWayMaps(TEXTURE2D_ARGS(PositiveMap.tex, PositiveMap.samplerstate),
        TEXTURE2D_ARGS(NegativeMap.tex, NegativeMap.samplerstate), current, next, frameBlend, opacity, emissionMap);

    float4 screen = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    float2 screenUV = screen.xy / screen.w;
    float eyeDepth = LinearEyeDepth(WorldPosition, GetWorldToViewMatrix());
    half soft = saturate((EyeDepth(SampleSceneDepth(screenUV)) - eyeDepth) / max(SoftDistance, 0.001));
    half near = saturate((eyeDepth - SIX_WAY_NEAR_FADE_START) / (SIX_WAY_NEAR_FADE_END - SIX_WAY_NEAR_FADE_START));
    half fade = ParticleAlpha * soft * near;
    half alpha = saturate(opacity * Opacity) * fade;

    // Rows are the particle basis: X right, Y up, Z toward the camera.
    half3x3 basis = half3x3(Tangent, Bitangent, Normal);
    half3 up;
    half3 down;
    half3 side;
    SixWayAmbientColors(up, down, side);
    half3 ambient = SixWayAmbient(up, down, side, basis, maps) * AmbientStrength;
    half3 glow = SixWayGlow(TEXTURE2D_ARGS(EmissionRamp.tex, sampler_LinearClamp), EmissionColor, emissionMap, Heat) * fade;
    SceneFog fog;
    fog.atmosphere = AtmosphereFogAt(screenUV, WorldPosition, eyeDepth);
    fog.war = FogOfWarAt(WorldPosition);
    half4 shaded = SixWayShade(maps, WorldPosition, screenUV, basis, ambient, ParticleColor, alpha, glow, fog);
    Color = shaded.rgb;
    Alpha = shaded.a;
#endif
}

#endif
