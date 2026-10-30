// Screen-space refraction for particles: bends URP's opaque texture along a normal map, by vertex colour alpha.
Shader "HyperRTS/ParticlesDistort"
{
    Properties
    {
        [Normal] _BumpMap("Distortion Normal", 2D) = "bump" {}
        _DistortionStrength("Strength (m)", Range(0.0, 2.0)) = 0.1
        _Scroll("Normal Scroll (UV a second)", Vector) = (0, 0, 0, 0)
        _EdgeSoftness("Edge Softness", Range(0.0, 0.5)) = 0.0
        _SoftFade("Soft Fade Distance (m)", Float) = 1.0
    }

    SubShader
    {
        // Before the other transparents, which the opaque texture lacks, so smoke and flames are never bent away.
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DistortVertex
            #pragma fragment DistortFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                float4 _Scroll;
                half _DistortionStrength;
                half _EdgeSoftness;
                float _SoftFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings DistortVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 DistortFragment(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float eyeDepth = input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneDepth - eyeDepth) / _SoftFade);
                half edge = saturate((0.5 - distance(input.uv, 0.5)) / max(_EdgeSoftness, 0.001));
                // Bending the scene inside the fog would show what the fog hides.
                half seen = 1.0h - FogOfWarAt(input.positionWS).a;
                half weight = input.color.a * edge * soft * seen;

                float2 uv = TRANSFORM_TEX(input.uv, _BumpMap) + _Scroll.xy * _Time.y;
                half2 bend = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv)).xy;
                // Strength is in metres, so a far ring bends as little of the screen as its size suggests.
                float2 metresToScreen = 0.5 * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) / eyeDepth;
                float2 offset = bend * (_DistortionStrength * weight) * metresToScreen;
                // Only bent pixels are replaced, since the opaque texture is a blurrier, half-size copy.
                half cover = weight * saturate(length(bend) * 4.0h);
                return half4(SampleSceneColor(screenUV + offset), cover);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
