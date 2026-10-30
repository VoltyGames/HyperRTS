// The Shader Graph HyperRTS/EffectDistort's one function: bends URP's opaque texture along a normal map.
// Weight is the particle's alpha; Strength is in metres, so far particles bend little of the screen.
#ifndef HYPERRTS_EFFECT_DISTORT
#define HYPERRTS_EFFECT_DISTORT

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"
#endif

void EffectDistort_float(float2 UV, float3 WorldPosition, UnityTexture2D NormalMap, float Strength, float2 Scroll,
    float SoftFade, float Weight, out float3 Color, out float Alpha)
{
#ifdef SHADERGRAPH_PREVIEW
    Color = float3(0.5, 0.5, 0.5);
    Alpha = Weight;
#else
    float4 screen = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    float2 screenUV = screen.xy / screen.w;
    float eyeDepth = screen.w;
    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
    float soft = saturate((sceneDepth - eyeDepth) / max(SoftFade, 0.001));
    // Bending the scene inside the fog would show what the fog hides.
    float weight = Weight * soft * (1.0 - FogOfWarAt(WorldPosition).a);
    float2 bend = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, UV + Scroll * _Time.y)).xy;
    float2 metresToScreen = 0.5 * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) / eyeDepth;
    Color = SampleSceneColor(screenUV + bend * (Strength * weight) * metresToScreen);
    // Only bent pixels are replaced, since the opaque texture is a blurrier, half-size copy.
    Alpha = weight * saturate(length(bend) * 4.0);
#endif
}

#endif
