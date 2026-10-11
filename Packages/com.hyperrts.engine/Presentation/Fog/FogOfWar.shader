// Darkens everything the camera sees by the fog-of-war grid: drawn as a box around the map, it rebuilds each
// pixel's world position from the depth texture, so hills, trees and buildings are fogged where they stand.
// Needs the URP asset's Depth Texture. The R8 grid (one texel per fog cell) is sampled bilinearly for soft edges.
Shader "HyperRTS/Fog Of War"
{
    Properties
    {
        _FogTex ("Fog Opacity (R)", 2D) = "black" {}
        _Color ("Fog Color", Color) = (0, 0, 0, 1)
        _BorderOpacity ("Opacity Beyond The Map", Range(0, 1)) = 0.65
        _BorderFade ("Border Fade (m)", Float) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FogOfWar"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_FogTex);
            SAMPLER(sampler_FogTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FogTex_ST;
                half _BorderOpacity;
                float _BorderFade;
            CBUFFER_END

            // Fog grid rectangle on the XZ plane: min x, min z, size x, size z. Set per draw.
            float4 _FogRect;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    clip(depth - 1e-6);
                #else
                    clip(1.0 - 1e-6 - depth);
                #endif

                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float2 fogUV = (world.xz - _FogRect.xy) / _FogRect.zw;
                half opacity = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, saturate(fogUV)).r;

                // Scenery past the playable edge stays dim whatever the grid says.
                float2 outside = max(_FogRect.xy - world.xz, world.xz - (_FogRect.xy + _FogRect.zw));
                opacity = max(opacity, _BorderOpacity * saturate(max(outside.x, outside.y) / _BorderFade));
                return half4(_Color.rgb, opacity * _Color.a);
            }
            ENDHLSL
        }
    }
}
