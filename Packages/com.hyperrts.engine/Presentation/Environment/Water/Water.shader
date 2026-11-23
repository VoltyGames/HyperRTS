// River and lake surface: three scales of spectrum ripples flowing along V (metres), calmed in slicks and roughened
// by gusts, over a body that deepens in colour with the bed. Vertex A is how shallow a ford is, G its fast water and
// B still water (ponds).
// Needs the URP asset's Depth and Opaque Textures.
Shader "HyperRTS/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color (albedo over a near bed, lit by sun and sky)", Color) = (0.22, 0.25, 0.15, 1)
        _DeepColor ("Deep Color (albedo of the water body, lit by sun and sky)", Color) = (0.13, 0.21, 0.17, 1)
        _DeepDepth ("Deep Depth (m to the full deep colour)", Float) = 2
        _Absorption ("Absorption per m (RGB)", Vector) = (0.7, 0.5, 0.6, 0)
        _BedDarkening ("Bed Brightness Under Water", Range(0, 1)) = 0.5
        _Caustics ("Caustics In Shallows", Range(0, 2)) = 0.6
        [Normal] _NormalMap ("Ripples", 2D) = "bump" {}
        _RippleSize ("Ripple Size (m)", Float) = 5
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 0.55
        _RippleFadeStart ("Ripple Fade Start (m)", Float) = 60
        _RippleFadeEnd ("Ripple Fade End (m)", Float) = 250
        _RippleStrengthFar ("Ripple Strength Far (fraction)", Range(0, 1)) = 0.4
        _FlowSpeed ("Current (m/s)", Float) = 0.35
        _SlickWidth ("Slick Width (m per noise tile)", Float) = 9
        _SlickLength ("Slick Length (m per noise tile)", Float) = 80
        _SlickCalm ("Slick Calm (ripples smoothed in slicks)", Range(0, 1)) = 0.7
        _GustSize ("Gust Size (m per noise tile)", Float) = 60
        _GustSpeed ("Gust Speed (m/s)", Float) = 1.5
        _GustStrength ("Gust Strength", Range(0, 1)) = 0.4
        _Smoothness ("Smoothness Near", Range(0, 1)) = 0.93
        _SmoothnessFar ("Smoothness Far", Range(0, 1)) = 0.7
        _SmoothnessFarDistance ("Smoothness Far Distance (m)", Float) = 250
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _GlintStrength ("Glint Strength", Float) = 12
        _GlintPeak ("Glint Peak (HDR clamp, a little past the bloom threshold)", Range(0, 4)) = 1.3
        _GlintFadeStart ("Glint Fade Start (m)", Float) = 80
        _GlintFadeEnd ("Glint Fade End (m)", Float) = 300
        _ShoreFadeDepth ("Shore Fade Depth (m, surface and wet bed fade in)", Float) = 0.3
        _FordAlpha ("Water Alpha Mid-Ford", Range(0, 1)) = 0.25
        _FoamColor ("Foam Color (albedo)", Color) = (0.85, 0.86, 0.82, 1)
        [NoScaleOffset] _FoamMap ("Foam (R lace, G clumps, B broad blobs)", 2D) = "black" {}
        _FoamSize ("Foam Size (m per lace tile)", Float) = 6
        _FordChop ("Ford Chop (extra ripples over fast water)", Range(0, 2)) = 1
        _FordFoam ("Ford Foam Flecks", Range(0, 1)) = 0.25
        _BankFoamDepth ("Bank Foam Depth (m of water it reaches)", Float) = 0.5
        _BankFoamAmount ("Bank Foam Amount", Range(0, 1)) = 0.35
        _WakeFoam ("Wake Foam", Range(0, 1)) = 0.8
        _WakeRipple ("Wake Ripple Strength", Range(0, 2)) = 0.5
        _PierWakeLength ("Pier Wake Length (m)", Float) = 12
        _PierWakeFoam ("Pier Wake Foam", Range(0, 1)) = 0.8
        _PierChop ("Pier Chop (extra ripples round a wake)", Range(0, 2)) = 1
        _PierCushionWidth ("Pier Cushion Width (m)", Float) = 0.5
        _ReflectionFloor ("Reflection Looking Straight Down (water is 0.02)", Range(0.02, 0.5)) = 0.02
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

        HLSLINCLUDE
        #pragma use_dxc
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterInput.hlsl"
        ENDHLSL

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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/MainLightKeywords.hlsl"
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE
            #pragma multi_compile_fragment _ _WATER_NO_CAUSTICS
            #pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/SceneLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/SceneDepth.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/Trample.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterSurface.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterFoam.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/SceneFog.hlsl"

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

            // Slicks smooth the ripples; gusts, a ford's fast water and the water round piers roughen them.
            half3 SurfaceNormal(Varyings input, half3 bitangent, float drift, half pierChop, float cameraDistance,
                out half3 ripple)
            {
                half still = input.color.b;
                half calm = smoothstep(0.5, 0.75, Slick(input.uv, drift)) * _SlickCalm * (1.0 - still);
                half gust = 1.0 + (Gust(input.positionWS) * 2.0 - 1.0) * _GustStrength;
                half rough = 1.0 + pierChop + input.color.g * _FordChop;
                half distant = lerp(_RippleStrengthFar, 1.0, Fade(cameraDistance, _RippleFadeStart, _RippleFadeEnd));
                float travel = lerp(drift, _Time.y * WATER_STILL_DRIFT, still);
                ripple = Ripples(input.uv, _RippleStrength * distant * (1.0 - calm) * gust * rough, travel, cameraDistance);
                return normalize(TransformTangentToWorld(ripple, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
            }

            // The opaque texture already holds URP's fog to the bottom; take it out so the water fogs it once.
            half3 RefractedBed(float2 screenUV, half3 ripple, float depth, float surface, float rawDepth, out float3 bed)
            {
                float2 refracted = screenUV + ripple.xy * _Refraction * saturate(depth);
                float refractedDepth = SampleSceneDepth(refracted);
                if (EyeDepth(refractedDepth) < surface)
                {
                    refracted = screenUV;
                    refractedDepth = rawDepth;
                }

                bed = ComputeWorldSpacePosition(refracted, refractedDepth, UNITY_MATRIX_I_VP);
                half4 bedFog = AtmosphereFogAt(refracted, bed, EyeDepth(refractedDepth));
                return RemoveAtmosphereFog(SampleSceneColor(refracted), bedFog);
            }

            half3 Body(float3 positionWS, half3 bottom, float3 bed, float depth, Light sun, out half3 light)
            {
                bottom *= 1.0 + Caustics(bed.xz, depth) * sun.shadowAttenuation;

                // A wet bed is darker than the dry ground; it darkens over the depth the surface fades in, so the
                // waterline stays soft.
                bottom *= lerp(1.0, _BedDarkening, saturate(depth / _ShoreFadeDepth));
                // Light through the water fades the bed into the colour its silt scatters.
                half3 transmitted = exp(-_Absorption.rgb * depth);
                // URP's sun colour holds the whole cloud cookie, so the bare light is taken here.
                half3 sunlight = _MainLightColor.rgb * saturate(sun.direction.y) * sun.shadowAttenuation;
                light = sunlight * CloudShade(positionWS) + SampleSH(half3(0.0, 1.0, 0.0));
                half3 albedo = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, _DeepDepth, depth));
                return bottom * transmitted + albedo * light * (1.0 - transmitted);
            }

            // Foam fades in the last centimetres so it never paints the dry bank. A ford is shallow all over, so it
            // takes no bank foam; ground detail Low drops calm-water foam.
            half FoamAmount(Varyings input, float depth, float drift, half pierFoam, half wakeFoam)
            {
                half amount = max(FordFoam(input.uv, drift, input.color.g), wakeFoam);
                #if !defined(_TERRAIN_SIMPLE)
                    amount = max(amount, max(BankFoam(depth) * (1.0 - input.color.a), pierFoam));
                #endif
                return amount * saturate(depth / (_ShoreFadeDepth * 0.5));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float cameraDistance = length(_WorldSpaceCameraPos - input.positionWS);
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float surface = EyeDepth(input.positionCS.z);
                float rawDepth = SampleSceneDepth(screenUV);
                float depth = max(0.0, EyeDepth(rawDepth) - surface);

                // Metres the current has carried the surface: one value over a river, none on still water (vertex B).
                float drift = _Time.y * _FlowSpeed * (1.0 - input.color.b);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half2 pier = half2(0.0, 0.0);
                #if !defined(_TERRAIN_SIMPLE)
                    [branch] if (_HyperRTSWaterPierCount > 0.5)
                    {
                        pier = PierWake(input.positionWS, normalize(bitangent.xz + 1e-5), normalize(input.tangentWS.xz + 1e-5));
                    }
                #endif

                half3 ripple;
                half3 normalWS = SurfaceNormal(input, bitangent, drift, pier.y, cameraDistance, ripple);
                // A wake tilts the surface down its slope, as if the foam were a height.
                WakeSample wake = SampleWakeFoam(input.positionWS);
                normalWS = normalize(normalWS - half3(wake.slope.x, 0.0, wake.slope.y) * _WakeRipple);

                float3 bed;
                half3 bottom = RefractedBed(screenUV, ripple, depth, surface, rawDepth, bed);
                // The cookie variant tints the sun's colour with the cloud cover, shading glint and foam.
                Light sun = SunLight(input.positionWS, input.normalWS);
                half3 light;
                half3 body = Body(input.positionWS, bottom, bed, depth, sun, light);

                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half grazing = pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half fresnel = _ReflectionFloor + (1.0 - _ReflectionFloor) * grazing;
                half smoothness = DistantSmoothness(cameraDistance);
                const bool useEnvironmentReflections = true;
                half3 sky = GlossyEnvironmentReflection(reflect(-view, normalWS), 1.0 - smoothness, 1.0, useEnvironmentReflections);
                half3 color = lerp(body, sky, fresnel);
                color += Glint(sun, normalWS, view, color, smoothness, cameraDistance);

                half foam = FoamCover(FoamAmount(input, depth, drift, pier.x, wake.foam), input.uv, drift);
                color = lerp(color, _FoamColor.rgb * light, foam);
                half alpha = max(lerp(1.0h, _FordAlpha, input.color.a) * saturate(depth / _ShoreFadeDepth), foam);
                return half4(ApplySceneFog(color, SceneFogAt(input.positionWS, input.positionCS)), alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/MetaPass.hlsl"

            // Surface Cache GI asks every renderer for albedo, transparent or not; deep water is what bounces light.
            half3 MetaAlbedo(float2 uv0)
            {
                return _DeepColor.rgb;
            }
            ENDHLSL
        }
    }
}
