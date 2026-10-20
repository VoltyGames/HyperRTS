// River and lake surface, ripples drifting along V (metres). Vertex A is how shallow a ford is, G fast water.
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
        _RippleStrengthFar ("Ripple Strength Far (fraction)", Range(0, 1)) = 0.4
        _FlowSpeed ("Current (m/s)", Float) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.93
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _GlintStrength ("Glint Strength", Float) = 12
        _GlintPeak ("Glint Peak (HDR, under bloom threshold)", Range(0, 1)) = 0.85
        _GlintFadeStart ("Glint Fade Start (m)", Float) = 80
        _GlintFadeEnd ("Glint Fade End (m)", Float) = 300
        _ShoreFadeDepth ("Shore Fade Depth (m)", Float) = 0.15
        _FordAlpha ("Water Alpha Mid-Ford", Range(0, 1)) = 0.25
        _FoamColor ("Foam Color", Color) = (0.82, 0.82, 0.76, 1)
        _RapidsFoamWidth ("Rapids Foam Width (m)", Float) = 1.5
        _RapidsFoamLength ("Rapids Foam Length (m)", Float) = 10
        _FlowFoamAmount ("Flow Foam Amount", Range(0, 1)) = 0.5
        _FlowFoamScale ("Flow Foam Scale (m)", Float) = 6
        _BankFoamWidth ("Bank Foam Width (m)", Float) = 2.5
        _BankFoamAmount ("Bank Foam Amount", Range(0, 1)) = 0.35
        _ShoreWaveFoam ("Shore Wave Foam", Range(0, 1)) = 0.4
        _ShoreWaveLength ("Shore Wave Length (m)", Float) = 1.6
        _ShoreWavePeriod ("Shore Wave Period (s)", Float) = 3.5
        _ShoreLap ("Shore Lap (fraction of fade depth)", Range(0, 1)) = 0.6
        _WakeFoam ("Wake Foam", Range(0, 1)) = 0.8
        _WakeRipple ("Wake Ripple Strength", Range(0, 2)) = 0.5
        _ReflectionStrength ("Scene Reflection Strength", Range(0, 1)) = 1
        _ReflectionFloor ("Reflection Looking Straight Down", Range(0.02, 0.5)) = 0.06
        _ReflectionDistance ("Scene Reflection Reach (m)", Float) = 40
        _ReflectionThickness ("Scene Reflection Hit Thickness (m)", Float) = 2
        _ReflectionDistortion ("Scene Reflection Ripple Distortion", Range(0, 1)) = 0.4
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
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/EyeDepth.hlsl"

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
                half _RippleStrengthFar;
                float _FlowSpeed;
                half _Smoothness;
                half _Refraction;
                half _GlintStrength;
                half _GlintPeak;
                float _GlintFadeStart;
                float _GlintFadeEnd;
                float _ShoreFadeDepth;
                half4 _FoamColor;
                half _FordAlpha;
                float _RapidsFoamWidth;
                float _RapidsFoamLength;
                half _FlowFoamAmount;
                float _FlowFoamScale;
                float _BankFoamWidth;
                half _BankFoamAmount;
                half _ShoreWaveFoam;
                float _ShoreWaveLength;
                float _ShoreWavePeriod;
                half _ShoreLap;
                half _WakeFoam;
                half _WakeRipple;
                half _ReflectionStrength;
                half _ReflectionFloor;
                float _ReflectionDistance;
                float _ReflectionThickness;
                half _ReflectionDistortion;
            CBUFFER_END

            #include "Trample.hlsl"
            #include "WaterFoam.hlsl"
            #include "WaterReflection.hlsl"
            #include "HeightFog.hlsl"

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
                half4 color : TEXCOORD4;
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
                output.color = input.color;
                return output;
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

            // Fills only the headroom under _GlintPeak, so the glint never sets off bloom.
            half3 Glint(Light sun, half3 normalWS, half3 view, half3 base, float cameraDistance)
            {
                half fade = Fade(cameraDistance, _GlintFadeStart, _GlintFadeEnd);
                if (fade <= 0.0)
                {
                    return half3(0.0, 0.0, 0.0);
                }

                half3 halfway = normalize(sun.direction + view);
                half highlight = pow(saturate(dot(normalWS, halfway)), exp2(_Smoothness * 11.0));
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(view, halfway)), 5.0);
                half3 glint = sun.color * (highlight * fresnel * _GlintStrength * sun.shadowAttenuation * fade);
                half headroom = max(0.0, _GlintPeak - Brightest(base));
                return glint * min(1.0, headroom / max(Brightest(glint), 0.0001));
            }

            // Clouds dim about two thirds of the scattered sunlight. The refracted bottom already has the cloud shadow.
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
                half rippleScale = lerp(_RippleStrengthFar, 1.0, Fade(cameraDistance, _RippleFadeStart, _RippleFadeEnd));
                half3 ripple = Ripples(input.uv, _RippleStrength * rippleScale);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(ripple, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                // A wake tilts the surface down its slope, as if the foam were a height.
                WakeSample wake = SampleWakeFoam(input.positionWS, input.uv);
                normalWS = normalize(normalWS - half3(wake.slope.x, 0.0, wake.slope.y) * _WakeRipple);

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float surface = EyeDepth(input.positionCS.z);
                float depth = max(0.0, EyeDepth(SampleSceneDepth(screenUV)) - surface);
                float shoreDistance = ShoreDistance(depth, input.positionWS);
                float shoreDepth = depth + ShoreLap(input.uv);

                float2 refracted = screenUV + ripple.xy * _Refraction * saturate(depth);
                if (EyeDepth(SampleSceneDepth(refracted)) < surface)
                {
                    refracted = screenUV;
                }

                // The opaque texture already holds the height fog; take it out so the water fogs it once.
                half4 fog = HeightFogAt(input.positionWS);
                half3 bottom = RemoveHeightFog(SampleSceneColor(refracted), fog);
                half3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth / _DepthRange)) * CloudShade(input.positionWS);
                half3 body = lerp(tint, bottom * _ShallowColor.rgb * 1.8, exp(-depth / _Clarity));

                // The cookie variant tints the sun's colour with the cloud cover, shading glint and foam.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half grazing = pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half fresnel = _ReflectionFloor + (1.0 - _ReflectionFloor) * grazing;
                half3 sky = GlossyEnvironmentReflection(reflect(-view, normalWS), 1.0 - _Smoothness, 1.0);
                // Ripples only partly bend the scene reflection, so banks and hulls stay readable in it.
                half3 mirror = normalize(lerp(input.normalWS, normalWS, _ReflectionDistortion));
                half4 scene = half4(0.0, 0.0, 0.0, 0.0);
                if (_ReflectionStrength > 0.0)
                {
                    scene = ScreenReflection(input.positionWS, reflect(-view, mirror), input.positionCS.xy);
                }

                half3 reflection = lerp(sky, RemoveHeightFog(scene.rgb, fog), scene.a * _ReflectionStrength);
                half3 color = lerp(body, reflection, fresnel);
                color += Glint(sun, normalWS, view, color, cameraDistance);

                // Calm and wake foam thin out on the last few centimetres so they never paint over the dry bank.
                half rapids = RapidsFoam(input.uv, input.color.g) * 0.5;
                half shallowFade = saturate(shoreDepth / (_ShoreFadeDepth * 0.5));
                half calm = max(CalmFoam(input.uv, shoreDistance), wake.foam) * shallowFade;
                half foam = max(rapids, calm);
                color = lerp(color, _FoamColor.rgb * (sun.color * sun.shadowAttenuation * 0.6 + 0.4), foam);
                half alpha = max(lerp(1.0h, _FordAlpha, input.color.a) * saturate(shoreDepth / _ShoreFadeDepth), foam);
                color = ApplyHeightFog(color, fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
