// A building's flag from a texture array, with a team band at the hoist, waving downwind as cloth (FlagCloth.hlsl).
// FlagSystem sets _FlagState and _FlagColor per entity (DOTS instancing).
Shader "HyperRTS/Flag"
{
    Properties
    {
        _FlagArray ("Flags (layer = side)", 2DArray) = "" {}
        [HideInInspector] _FlagState ("State (layer, hoist, shown, drop)", Vector) = (0, 1, 0, 0)
        [HideInInspector] _FlagColor ("Band Colour", Color) = (0, 0, 0, 0)
        _BandWidth ("Band Width (share of the length)", Range(0, 0.5)) = 0.12
        _Translucency ("Light Through", Range(0, 1)) = 0.35
        _WaveLength ("Wave Length (flag heights)", Float) = 1.1
        _WaveSpeed ("Wave Speed", Float) = 7
        _WaveAmplitude ("Wave Amplitude (flag heights)", Range(0, 0.3)) = 0.09
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagCloth.hlsl"
        ENDHLSL

        Pass
        {
            Name "Flag"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                Cloth cloth = FlagCloth(input.positionOS.xyz, input.uv);
                output.positionWS = cloth.positionWS;
                output.positionCS = TransformWorldToHClip(cloth.positionWS);
                output.uv = input.uv;
                output.normalWS = cloth.normalWS;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 texel = SAMPLE_TEXTURE2D_ARRAY(_FlagArray, sampler_FlagArray, input.uv, _FlagState.x).rgb;
                half band = step(input.uv.x, _BandWidth) * _FlagColor.a;
                half3 albedo = lerp(texel, _FlagColor.rgb, band);

                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                half facing = dot(normal, sun.direction);
                half lit = saturate(facing) + saturate(-facing) * _Translucency;

                half occlusion = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    occlusion = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
                #endif

                half3 color = albedo * (sun.color * sun.shadowAttenuation * lit + SampleSH(normal) * occlusion);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                Cloth cloth = FlagCloth(input.positionOS.xyz, input.uv);
                output.positionCS = ShadowCasterPositionCS(cloth.positionWS, cloth.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformWorldToHClip(FlagCloth(input.positionOS.xyz, input.uv).positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                Cloth cloth = FlagCloth(input.positionOS.xyz, input.uv);
                output.positionCS = TransformWorldToHClip(cloth.positionWS);
                output.normalWS = cloth.normalWS;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return DepthNormalOutput(normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0));
            }
            ENDHLSL
        }
    }
}
