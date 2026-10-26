// URP's screen-space shadows, darkened where a short depth-buffer trace toward the sun hits something (ContactShadowsPass).
Shader "Hidden/HyperRTS/ContactShadows"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ContactShadows"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_ScreenSpaceShadowmapTexture);
            float4x4 _ContactShadowProjection;
            float4x4 _ContactShadowInverseProjection;
            float3 _ContactShadowSun;
            // Length, steps, thickness, strength.
            float4 _ContactShadowTrace;
            // Width, height and their reciprocals.
            float4 _ContactShadowScreen;

            // Keeps a surface from shadowing itself through depth precision at the start of the trace.
            static const float SurfaceBias = 0.02;

            // A trace shorter on screen takes fewer steps, since two samples in one pixel find nothing new.
            static const float PixelsPerStep = 3.0;

            float3 ViewPosition(float2 uv, float rawDepth)
            {
                #if !UNITY_REVERSED_Z
                    rawDepth = rawDepth * 2.0 - 1.0;
                #endif
                float4 view = mul(_ContactShadowInverseProjection, float4(uv * 2.0 - 1.0, rawDepth, 1.0));
                return view.xyz / view.w;
            }

            bool IsSky(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth == 0.0;
                #else
                    return rawDepth == 1.0;
                #endif
            }

            // 1 where the first hit is at the start of the trace, fading to 0 at its far end. Clip space is linear
            // along the ray, so each step lerps it instead of projecting again.
            half Occlusion(float3 start, float jitter)
            {
                float4 clipStart = mul(_ContactShadowProjection, float4(start, 1.0));
                float4 clipEnd = mul(_ContactShadowProjection, float4(start + _ContactShadowSun * _ContactShadowTrace.x, 1.0));
                float2 screenTrace = (clipEnd.xy / clipEnd.w - clipStart.xy / clipStart.w) * 0.5 * _ContactShadowScreen.xy;
                int steps = (int)clamp(ceil(length(screenTrace) / PixelsPerStep), 1.0, _ContactShadowTrace.y);
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float reach = (i + jitter) / steps;
                    float4 clip = lerp(clipStart, clipEnd, reach);
                    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
                    if (any(uv != saturate(uv)))
                    {
                        return 0.0h;
                    }

                    float sceneDepth = LinearEyeDepth(LoadSceneDepth(uv * _ContactShadowScreen.xy), _ZBufferParams);
                    float behind = clip.w - sceneDepth;
                    if (behind > SurfaceBias && behind < _ContactShadowTrace.z)
                    {
                        return half(1.0 - reach * reach);
                    }
                }

                return 0.0h;
            }

            half Frag(Varyings input) : SV_Target
            {
                uint2 pixel = uint2(input.positionCS.xy);
                half shadow = LOAD_TEXTURE2D(_ScreenSpaceShadowmapTexture, pixel).x;
                float rawDepth = LoadSceneDepth(pixel);
                if (shadow <= 0.0h || IsSky(rawDepth))
                {
                    return shadow;
                }

                float3 start = ViewPosition(input.positionCS.xy * _ContactShadowScreen.zw, rawDepth);
                // Fixed per pixel rather than per frame, so the trace's banding doesn't crawl.
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                return shadow * (1.0h - half(_ContactShadowTrace.w) * Occlusion(start, jitter));
            }
            ENDHLSL
        }
    }
}
