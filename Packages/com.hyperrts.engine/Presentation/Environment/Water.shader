// River and lake surface: depth-tinted colour, refracted bottom, ripples drifting along V (metres), fresnel sky and
// sun glint kept under bloom, cloud shadows. Vertex colour A thins the water at fords; G streaks foam on fast water.
// Needs the URP asset's Depth and Opaque Textures.
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
        _RippleFadeStart ("Ripple Fade Start (m)", Float) = 60
        _RippleFadeEnd ("Ripple Fade End (m)", Float) = 250
        _RippleFar ("Ripple Strength Far (fraction)", Range(0, 1)) = 0.4
        _FlowSpeed ("Current (m/s)", Float) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.93
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _GlintStrength ("Glint Strength", Float) = 12
        _GlintMax ("Glint Peak (HDR, under bloom threshold)", Range(0, 1)) = 0.85
        _GlintFadeStart ("Glint Fade Start (m)", Float) = 80
        _GlintFadeEnd ("Glint Fade End (m)", Float) = 300
        _EdgeFade ("Edge Fade Depth (m)", Float) = 0.15
        _FoamColor ("Foam Color", Color) = (0.82, 0.82, 0.76, 1)
        _StreakWidth ("Fast Water Streak Width (m)", Float) = 1.5
        _StreakLength ("Fast Water Streak Length (m)", Float) = 10
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

            Blend SrcAlpha OneMinusSrcAlpha
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
                float _RippleFadeStart;
                float _RippleFadeEnd;
                half _RippleFar;
                float _FlowSpeed;
                half _Smoothness;
                half _Refraction;
                half _GlintStrength;
                half _GlintMax;
                float _GlintFadeStart;
                float _GlintFadeEnd;
                float _EdgeFade;
                half4 _FoamColor;
                float _StreakWidth;
                float _StreakLength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half4 color : TEXCOORD5;
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
                output.color = input.color;
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

            // 1 before start metres, 0 past end.
            half Fade(float metres, float start, float end)
            {
                return 1.0 - saturate((metres - start) / max(end - start, 0.001));
            }

            half Brightest(half3 color)
            {
                return max(color.r, max(color.g, color.b));
            }

            half3 Ripples(float2 uv, half strength)
            {
                float2 flow = float2(0.0, _Time.y * _FlowSpeed);
                half3 first = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, (uv - flow) / _RippleSize), strength);
                half3 second = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    (uv * 0.43 + float2(0.31, 0.0) - flow * 0.7) / _RippleSize), strength);
                return normalize(half3(first.xy + second.xy, first.z * second.z));
            }

            // Fills only the headroom under _GlintMax, so the glint never sets off bloom.
            half3 Glint(Light sun, half3 normalWS, half3 view, half3 base, float cameraDistance)
            {
                half3 halfway = normalize(sun.direction + view);
                half highlight = pow(saturate(dot(normalWS, halfway)), exp2(_Smoothness * 11.0));
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(view, halfway)), 5.0);
                half fade = Fade(cameraDistance, _GlintFadeStart, _GlintFadeEnd);
                half3 glint = sun.color * (highlight * fresnel * _GlintStrength * sun.shadowAttenuation * fade);
                half headroom = max(0.0, _GlintMax - Brightest(base));
                return glint * min(1.0, headroom / max(Brightest(glint), 0.0001));
            }

            // Foam only on fast water (vertex G), stretched along the current and broken into patches so the ford
            // doesn't read as a striped block.
            half Streaks(float2 uv, half fast)
            {
                float drift = _Time.y * _FlowSpeed * 2.5;
                float2 streakUV = float2(uv.x / _StreakWidth, (uv.y - drift) / _StreakLength);
                half3 streak = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, streakUV));
                float2 patchUV = float2(uv.x * 0.37 + 0.21, uv.y - drift * 0.6) / (_StreakLength * 1.7);
                half3 patches = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, patchUV));
                return fast * smoothstep(0.1, 0.45, streak.x) * smoothstep(-0.1, 0.35, patches.y);
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
                float cameraDistance = length(_WorldSpaceCameraPos - input.positionWS);
                half calm = lerp(_RippleFar, 1.0, Fade(cameraDistance, _RippleFadeStart, _RippleFadeEnd));
                half3 ripple = Ripples(input.uv, _RippleStrength * calm);
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
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half3 sky = GlossyEnvironmentReflection(reflect(-view, normalWS), 1.0 - _Smoothness, 1.0);
                half3 color = lerp(body, sky, fresnel);
                color += Glint(sun, normalWS, view, color, cameraDistance);

                half foam = Streaks(input.uv, input.color.g) * 0.5;
                color = lerp(color, _FoamColor.rgb * (sun.color * sun.shadowAttenuation * 0.6 + 0.4), foam);
                half alpha = max(input.color.a * saturate(depth / _EdgeFade), foam);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
