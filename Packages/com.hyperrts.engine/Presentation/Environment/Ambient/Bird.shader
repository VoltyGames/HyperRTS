// Unlit alpha-clipped bird quads drawn from _Instances by BirdRenderer. Each instance picks its wing pose from a 2x2
// flipbook in _BaseMap: frames left to right, top to bottom, seen from above with the head up.
Shader "HyperRTS/Bird"
{
    Properties
    {
        _BaseMap ("Flipbook", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #pragma use_dxc

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Ambient/BirdInstance.cs.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Cutoff;
        CBUFFER_END

        StructuredBuffer<BirdInstance> _Instances;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
        };

        Varyings BirdVert(Attributes input)
        {
            BirdInstance bird = _Instances[input.instanceID];
            float2 cell = float2(bird.Frame & 1u, 1u - (bird.Frame >> 1u)) * 0.5;
            Varyings output;
            output.positionWS = mul(bird.ObjectToWorld, float4(input.positionOS.xyz, 1.0)).xyz;
            output.normalWS = normalize(mul((float3x3)bird.ObjectToWorld, input.normalOS));
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv * 0.5 + cell;
            return output;
        }

        half4 BirdColor(Varyings input)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            clip(color.a - _Cutoff);
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Bird"
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex BirdVert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _EXPOSURE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DistanceFog.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = BirdColor(input);
                color.rgb = BlendDistanceFogExposed(color.rgb, input.positionCS, GetPreExposureMultiplier());
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex BirdVert
            #pragma fragment Frag

            half Frag(Varyings input) : SV_Target
            {
                BirdColor(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma vertex BirdVert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                BirdColor(input);
                return DepthNormalOutput(normalize(input.normalWS));
            }
            ENDHLSL
        }

        // BirdRenderer draws without shadows; the pass keeps the cutout depth prepasses complete.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output = BirdVert(input);
                output.positionCS = ShadowCasterPositionCS(output.positionWS, output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                BirdColor(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
