// River and lake surface: colour and clarity from the water depth (depth texture), refraction of the bottom
// (opaque texture), ripples that drift downstream along the mesh's V coordinate (metres), sun glint, sky reflection
// by fresnel, foam where the water meets the shore, and cloud shadows from the sun's cookie. Needs the URP asset's
// Depth and Opaque Textures.
Shader "HyperRTS/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.34, 0.42, 0.33, 1)
        _DeepColor ("Deep Color", Color) = (0.07, 0.15, 0.14, 1)
        _DepthRange ("Depth To Deep Color (m)", Float) = 2.5
        _Clarity ("Clarity (m)", Float) = 0.7
        [Normal] _NormalMap ("Ripples", 2D) = "bump" {}
        _RippleSize ("Ripple Size (m)", Float) = 5
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 0.55
        _FlowSpeed ("Current (m/s)", Float) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.93
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _FoamWidth ("Shore Foam Width (m)", Float) = 0.5
        _FoamColor ("Foam Color", Color) = (0.82, 0.82, 0.76, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                float _Clarity;
                float4 _NormalMap_ST;
                float _RippleSize;
                half _RippleStrength;
                float _FlowSpeed;
                half _Smoothness;
                half _Refraction;
                float _FoamWidth;
                half4 _FoamColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Eye-space depth for perspective and orthographic cameras (map tools render the water top-down).
            float EyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w == 0)
                {
                    return LinearEyeDepth(rawDepth, _ZBufferParams);
                }

                #if UNITY_REVERSED_Z
                    rawDepth = 1.0 - rawDepth;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
            }

            half3 Ripples(float2 uv, out half3 first)
            {
                float2 flow = float2(0.0, _Time.y * _FlowSpeed);
                first = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, (uv - flow) / _RippleSize), _RippleStrength);
                half3 second = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    (uv * 0.43 + float2(0.31, 0.0) - flow * 0.7) / _RippleSize), _RippleStrength);
                return normalize(half3(first.xy + second.xy, first.z * second.z));
            }

            // Clouds dim the sunlight the water scatters back up, about two thirds of it; the sky's share stays. The
            // refracted bottom already carries the cloud shadow on the ground.
            half3 CloudShade(float3 positionWS)
            {
                #if defined(_LIGHT_COOKIES)
                    return lerp(half3(1.0, 1.0, 1.0), SampleMainLightCookie(positionWS), 0.65);
                #else
                    return half3(1.0, 1.0, 1.0);
                #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 first;
                half3 ripple = Ripples(input.uv, first);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(ripple, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float surface = EyeDepth(input.positionCS.z);
                float depth = max(0.0, EyeDepth(SampleSceneDepth(screenUV)) - surface);

                float2 refracted = screenUV + ripple.xy * _Refraction * saturate(depth);
                if (EyeDepth(SampleSceneDepth(refracted)) < surface)
                {
                    refracted = screenUV;
                }

                half3 bottom = SampleSceneColor(refracted);
                half3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth / _DepthRange)) * CloudShade(input.positionWS);
                half3 body = lerp(tint, bottom * _ShallowColor.rgb * 1.8, exp(-depth / _Clarity));

                // The cookie variant tints the sun's colour with the cloud cover, shading glint and foam.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfway = normalize(sun.direction + view);
                half glint = pow(saturate(dot(normalWS, halfway)), exp2(_Smoothness * 11.0)) * sun.shadowAttenuation;
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half3 sky = GlossyEnvironmentReflection(reflect(-view, normalWS), 1.0 - _Smoothness, 1.0);
                half3 color = lerp(body, sky, fresnel) + glint * sun.color * 1.5;

                half foam = saturate(1.0 - depth / _FoamWidth) * saturate(first.x * 2.0 + 0.6);
                color = lerp(color, _FoamColor.rgb * (sun.color * sun.shadowAttenuation * 0.6 + 0.4), foam * 0.7);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
