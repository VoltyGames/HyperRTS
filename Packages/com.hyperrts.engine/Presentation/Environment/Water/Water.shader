// River and lake surface: three scales of spectrum ripples flowing along V (metres), faster mid-channel. Vertex A is
// how shallow a ford is, G fast water, B still water (ponds).
// Needs the URP asset's Depth and Opaque Textures.
Shader "HyperRTS/Water"
{
    Properties
    {
        _ScatterColor ("Scatter Color (deep water)", Color) = (0.07, 0.15, 0.14, 1)
        _Absorption ("Absorption per m (RGB)", Vector) = (0.9, 0.5, 0.55, 0)
        _BedDarkening ("Bed Brightness Under Water", Range(0, 1)) = 0.5
        _Caustics ("Caustics In Shallows", Range(0, 2)) = 0.6
        [Normal] _NormalMap ("Ripples", 2D) = "bump" {}
        _RippleSize ("Ripple Size (m)", Float) = 5
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 0.55
        _RippleFadeStart ("Ripple Fade Start (m)", Float) = 60
        _RippleFadeEnd ("Ripple Fade End (m)", Float) = 250
        _RippleStrengthFar ("Ripple Strength Far (fraction)", Range(0, 1)) = 0.4
        _FlowSpeed ("Current (m/s)", Float) = 0.35
        _Smoothness ("Smoothness Near", Range(0, 1)) = 0.93
        _SmoothnessFar ("Smoothness Far", Range(0, 1)) = 0.7
        _SmoothnessFarDistance ("Smoothness Far Distance (m)", Float) = 250
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _GlintStrength ("Glint Strength", Float) = 12
        _GlintPeak ("Glint Peak (HDR clamp, a little past the bloom threshold)", Range(0, 4)) = 1.3
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/MainLightKeywords.hlsl"
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE
            #pragma multi_compile_fragment _ _WATER_NO_CAUSTICS
            #pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/EyeDepth.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/Trample.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterFoam.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterReflection.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Water/WaterSurface.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneFog.hlsl"

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

            half4 Frag(Varyings input) : SV_Target
            {
                float cameraDistance = length(_WorldSpaceCameraPos - input.positionWS);
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float surface = EyeDepth(input.positionCS.z);
                float rawDepth = SampleSceneDepth(screenUV);
                float depth = max(0.0, EyeDepth(rawDepth) - surface);
                float shoreDistance = ShoreDistance(depth, input.positionWS);
                float shoreDepth = depth + ShoreLap(input.uv);

                // The current runs fastest mid-channel and drags along the banks; vertex B marks still water (ponds).
                float current = _FlowSpeed * lerp(WATER_BANK_CURRENT, 1.0, smoothstep(0.5, 5.0, shoreDistance)) * (1.0 - input.color.b);
                half rippleScale = lerp(_RippleStrengthFar, 1.0, Fade(cameraDistance, _RippleFadeStart, _RippleFadeEnd));
                half3 ripple = Ripples(input.uv, _RippleStrength * rippleScale, current, cameraDistance);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(ripple, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                // A wake tilts the surface down its slope, as if the foam were a height.
                WakeSample wake = SampleWakeFoam(input.positionWS, input.uv);
                normalWS = normalize(normalWS - half3(wake.slope.x, 0.0, wake.slope.y) * _WakeRipple);

                float2 refracted = screenUV + ripple.xy * _Refraction * saturate(depth);
                float refractedDepth = SampleSceneDepth(refracted);
                if (EyeDepth(refractedDepth) < surface)
                {
                    refracted = screenUV;
                    refractedDepth = rawDepth;
                }

                // The opaque texture already holds URP's fog to the bottom; take it out so the water fogs it once.
                SceneFog fog = SceneFogAt(input.positionWS, input.positionCS);
                float3 bed = ComputeWorldSpacePosition(refracted, refractedDepth, UNITY_MATRIX_I_VP);
                half4 bedFog = AtmosphereFogAt(refracted, bed, EyeDepth(refractedDepth));
                half3 bottom = RemoveAtmosphereFog(SampleSceneColor(refracted), bedFog);

                // The cookie variant tints the sun's colour with the cloud cover, shading glint and foam.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                bottom *= 1.0 + Caustics(bed.xz, depth) * sun.shadowAttenuation;

                // Light through the water loses red first, so the bed fades into the silt-green the water scatters.
                // A wet bed is darker than the dry ground the opaque texture shows, from the first centimetres.
                bottom *= lerp(1.0, _BedDarkening, saturate(depth / 0.1));
                half3 transmitted = exp(-_Absorption.rgb * depth);
                half3 body = bottom * transmitted + _ScatterColor.rgb * CloudShade(input.positionWS) * (1.0 - transmitted);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half grazing = pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half fresnel = _ReflectionFloor + (1.0 - _ReflectionFloor) * grazing;
                half smoothness = DistantSmoothness(cameraDistance);
                half3 sky = GlossyEnvironmentReflection(reflect(-view, normalWS), 1.0 - smoothness, 1.0);
                // Ripples only partly bend the scene reflection, so banks and hulls stay readable in it.
                half3 mirror = normalize(lerp(input.normalWS, normalWS, _ReflectionDistortion));
                half4 scene = half4(0.0, 0.0, 0.0, 0.0);
                // The sharp march can't blur, so a rougher surface hands the reflection over to the blurred sky.
                half sharpness = saturate((smoothness - _SmoothnessFar) / max(_Smoothness - _SmoothnessFar, 0.001));
                [branch] if (_ReflectionStrength > 0.0 && sharpness > 0.0)
                {
                    scene = ScreenReflection(input.positionWS, reflect(-view, mirror), input.positionCS.xy);
                    scene.a *= sharpness;
                }

                half3 reflection = lerp(sky, RemoveAtmosphereFog(scene.rgb, fog.atmosphere), scene.a * _ReflectionStrength);
                half3 color = lerp(body, reflection, fresnel);
                color += Glint(sun, normalWS, view, color, smoothness, cameraDistance);

                // Calm and wake foam thin out on the last few centimetres so they never paint over the dry bank.
                half rapids = RapidsFoam(input.uv, input.color.g) * 0.5;
                half shallowFade = saturate(shoreDepth / (_ShoreFadeDepth * 0.5));
                half calm = max(CalmFoam(input.uv, shoreDistance, input.color.r), wake.foam) * shallowFade;
                half foam = max(rapids, calm);
                color = lerp(color, _FoamColor.rgb * (sun.color * sun.shadowAttenuation * 0.6 + 0.4), foam);
                half alpha = max(lerp(1.0h, _FordAlpha, input.color.a) * saturate(shoreDepth / _ShoreFadeDepth), foam);
                color = ApplySceneFog(color, fog);
                return half4(color, alpha);
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
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // Surface Cache GI asks every renderer for albedo, transparent or not; deep water is what bounces light.
            half3 MetaAlbedo(float2 uv0)
            {
                return _ScatterColor.rgb;
            }
            ENDHLSL
        }
    }
}
