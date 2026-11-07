// Dithers away tree pixels near the match camera and on the line from it to its focus, so units behind trees show.
// Include after URP's Core.hlsl. Call TreeViewFade from a colour or depth fragment, never the shadow caster.
#ifndef HYPERRTS_VIEW_FADE_INCLUDED
#define HYPERRTS_VIEW_FADE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

// Camera: xyz position, w radius of the faded ball. Focus: xyz point, w radius of the faded line. Reach: metres the
// line runs on past the focus, so trees over the ground in view fade too. Zero amount is off.
float4 _HyperRTSFadeCamera;
float4 _HyperRTSFadeFocus;
float _HyperRTSFadeReach;
float _HyperRTSFadeAmount;

// Fade 0 (solid) to 1 (gone) for a world point: full inside half the radius, eased out to the radius.
half ViewFadeAt(float3 positionWS)
{
    float3 camera = _HyperRTSFadeCamera.xyz;
    float3 toFocus = _HyperRTSFadeFocus.xyz - camera;
    float span = max(length(toFocus), 1e-2);
    float along = clamp(dot(positionWS - camera, toFocus) / (span * span), 0.0, 1.0 + _HyperRTSFadeReach / span);
    float lineDistance = distance(positionWS, camera + toFocus * along);
    half aroundCamera = 1.0 - smoothstep(_HyperRTSFadeCamera.w * 0.5, _HyperRTSFadeCamera.w, distance(positionWS, camera));
    half between = 1.0 - smoothstep(_HyperRTSFadeFocus.w * 0.5, _HyperRTSFadeFocus.w, lineDistance);
    return max(aroundCamera, between) * _HyperRTSFadeAmount;
}

// Only the match camera fades: scene views and capture cameras sit elsewhere. positionCS is the fragment's SV_POSITION.
void TreeViewFade(float4 positionCS)
{
    if (_HyperRTSFadeAmount <= 0.0 || distance(_WorldSpaceCameraPos, _HyperRTSFadeCamera.xyz) > 0.05)
    {
        return;
    }

    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    float3 positionWS = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);
    float noise = frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
    clip(1.0 - ViewFadeAt(positionWS) - (noise * 0.98 + 0.01));
}

#endif
