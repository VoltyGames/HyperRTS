// Water.shader's screen-space reflection, a short ray march over the depth texture.
// Include after the water's CBUFFER, DeclareDepthTexture.hlsl and DeclareOpaqueTexture.hlsl.
#ifndef HYPERRTS_WATER_REFLECTION_INCLUDED
#define HYPERRTS_WATER_REFLECTION_INCLUDED

#define WATER_REFLECTION_STEPS 16
#define WATER_REFLECTION_REFINE 4
#define WATER_REFLECTION_EDGE 0.08 // screen fraction over which hits fade out at the borders

// Screen UV (as SampleSceneDepth reads it) and eye depth of a perspective clip-space position.
float3 ClipToScreen(float4 positionCS)
{
    float2 uv = positionCS.xy / positionCS.w * float2(0.5, 0.5 * _ProjectionParams.x) + 0.5;
    return float3(uv, positionCS.w);
}

// Metres a ray position lies behind the opaque scene along the view, negative in front of it.
float BehindScene(float3 probe)
{
    return probe.z - LinearEyeDepth(SampleSceneDepth(probe.xy), _ZBufferParams);
}

bool OnScreen(float2 uv)
{
    return all(uv == saturate(uv));
}

// Fraction along the ray of the first surface it meets, or -1. A deep gap after refining a crossing means the ray
// slipped behind a thin silhouette, so the march goes on.
float MarchReflection(float4 startCS, float4 endCS, float2 pixel)
{
    // A per-pixel start offset trades the march's banding for a fine dither.
    float jitter = InterleavedGradientNoise(pixel, 0);
    float previous = 0.0;
    [loop] for (int i = 1; i <= WATER_REFLECTION_STEPS; i++)
    {
        // Squared spacing packs the steps near the surface, where banks and hulls meet the water.
        float t = (i - jitter) / WATER_REFLECTION_STEPS;
        t *= t;
        float3 probe = ClipToScreen(lerp(startCS, endCS, t));
        if (!OnScreen(probe.xy))
        {
            return -1.0;
        }

        float gap = BehindScene(probe);
        if (gap > 0.0)
        {
            float front = previous;
            float back = t;
            [unroll] for (int j = 0; j < WATER_REFLECTION_REFINE; j++)
            {
                float middle = 0.5 * (front + back);
                float middleGap = BehindScene(ClipToScreen(lerp(startCS, endCS, middle)));
                bool behind = middleGap > 0.0;
                front = behind ? front : middle;
                back = behind ? middle : back;
                gap = behind ? middleGap : gap;
            }

            if (gap < _ReflectionThickness)
            {
                return back;
            }
        }

        previous = t;
    }

    return -1.0;
}

// rgb the reflected scene, a its weight (0 on a miss). Orthographic map renders and Ground detail Low skip the march.
half4 ScreenReflection(float3 positionWS, half3 directionWS, float2 pixel)
{
    #if defined(_TERRAIN_SIMPLE)
        return half4(0.0, 0.0, 0.0, 0.0);
    #else
        if (unity_OrthoParams.w != 0.0)
        {
            return half4(0.0, 0.0, 0.0, 0.0);
        }

        float3 startVS = TransformWorldToView(positionWS);
        float3 directionVS = TransformWorldToViewDir(directionWS, true);
        float reach = _ReflectionDistance;

        // View space looks down -Z: a ray turning back to the camera stops short of the near plane.
        if (directionVS.z > 0.0)
        {
            reach = min(reach, (-startVS.z - _ProjectionParams.y * 2.0) / directionVS.z);
        }

        float4 startCS = TransformWViewToHClip(startVS);
        float4 endCS = TransformWViewToHClip(startVS + directionVS * reach);
        float hit = MarchReflection(startCS, endCS, pixel);
        if (hit < 0.0)
        {
            return half4(0.0, 0.0, 0.0, 0.0);
        }

        float2 uv = ClipToScreen(lerp(startCS, endCS, hit)).xy;
        float2 edge = min(uv, 1.0 - uv) / WATER_REFLECTION_EDGE;
        // Rays turning back to the camera fade too: their hits are far sides the camera can't see.
        half weight = saturate(min(edge.x, edge.y)) * saturate((1.0 - hit) * 4.0) * saturate(1.0 - directionVS.z * 2.0);
        return half4(SampleSceneColor(uv), weight);
    #endif
}

#endif
