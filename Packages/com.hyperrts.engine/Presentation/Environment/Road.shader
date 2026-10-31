// Road on a ribbon mesh over the terrain: U across (0..1), V along in metres, vertex alpha fading ends and junctions.
// No depth write, so decals, SSAO and the fog of war see the terrain underneath and cover the road too.
// Needs mesh normals and tangents.
Shader "HyperRTS/Road"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Albedo (A = coverage)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Normal] [NoScaleOffset] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _MaskMap ("Mask (G = occlusion, A = smoothness)", 2D) = "white" {}
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 1
        _TileLength ("Tile Length (m along the road)", Float) = 8
        _EdgeWidth ("Edge Fade (of the half width)", Range(0.01, 1)) = 0.3
        _EdgeNoise ("Edge Breakup", Range(0, 1)) = 0.6
        [NoScaleOffset] _NoiseMap ("Edge Noise", 2D) = "gray" {}
        _NoiseSize ("Edge Noise Size (m)", Float) = 3
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.45
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.55
        _PuddleCoverage ("Puddle Coverage (at full wetness)", Range(0, 1)) = 0.6
        _PuddleSmoothness ("Puddle Smoothness", Range(0, 1)) = 0.95
        _PuddleNoiseSize ("Puddle Noise Size (m)", Float) = 5
        _RutOffset ("Rut Offset (U from the middle)", Range(0, 0.5)) = 0.2
        _RutWidth ("Rut Width (U)", Range(0.01, 0.25)) = 0.06
        _RutPuddles ("Rut Puddles", Range(0, 1)) = 0.6
        _EdgePuddles ("Edge Puddles", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // Opaque queue so renderer sortingOrder (the road class rank) stacks overlaps. MapRoadMeshTests pins it.
            "Queue" = "Geometry+10"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        TEXTURE2D(_NoiseMap);
        SAMPLER(sampler_NoiseMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _BumpScale;
            half _Smoothness;
            float _TileLength;
            half _EdgeWidth;
            half _EdgeNoise;
            float _NoiseSize;
            half _WetDarkening;
            half _WetSmoothness;
            half _PuddleCoverage;
            half _PuddleSmoothness;
            float _PuddleNoiseSize;
            half _RutOffset;
            half _RutWidth;
            half _RutPuddles;
            half _EdgePuddles;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Road"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/LightingKeywords.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Wetness.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half opacity : TEXCOORD4;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    half3 vertexLighting : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.opacity = input.color.a;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLighting = VertexLighting(position.positionWS, normals.normalWS);
                #endif
                return output;
            }

            // Noise decides coverage toward the sides, so gravel thins in patches instead of along a clean line.
            half Coverage(Varyings input, half albedoAlpha)
            {
                half inside = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                half fade = saturate(inside / _EdgeWidth);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz / _NoiseSize).r;
                half crumble = saturate(fade * (1.0 + _EdgeNoise) - noise * _EdgeNoise);
                return smoothstep(0.0, 1.0, crumble) * albedoAlpha * input.opacity;
            }

            // How readily water stands here: in the wheel ruts and along the edges, broken up by world noise.
            // Explicit gradients: it runs inside the wetness branch.
            half PuddleCollect(Varyings input, float2 st, float2 dx, float2 dy)
            {
                half fromMiddle = abs(input.uv.x - 0.5);
                half rut = _RutPuddles * (1.0 - smoothstep(0.0, _RutWidth, abs(fromMiddle - _RutOffset)));
                half edge = _EdgePuddles * saturate((fromMiddle - 0.35) / 0.15);
                half noise = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_NoiseMap, st, dx, dy).r;
                return max(rut, edge) + (noise - 0.5) * 1.2;
            }

            void ApplyRain(inout SurfaceData surface, Varyings input)
            {
                float2 st = input.positionWS.xz / _PuddleNoiseSize;
                float2 dx = ddx(st);
                float2 dy = ddy(st);
                UNITY_BRANCH
                if (!RainWet())
                {
                    return;
                }

                Wet(surface.albedo, surface.smoothness, surface.normalTS, _HyperRTSWetness, _WetDarkening, _WetSmoothness);
                half cover = PuddleCover(PuddleCollect(input, st, dx, dy), _PuddleCoverage);
                Puddle(surface.albedo, surface.smoothness, surface.normalTS, cover, _PuddleSmoothness);
            }

            InputData Inputs(Varyings input, half3 normalTS)
            {
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                data.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                data.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, data.tangentToWorld));
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.bakedGI = SampleSH(data.normalWS);
                data.shadowMask = half4(1.0, 1.0, 1.0, 1.0);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    data.vertexLighting = input.vertexLighting;
                #endif
                return data;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = float2(input.uv.x, input.uv.y / _TileLength);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _Tint;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = Coverage(input, albedo.a);
                surface.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                surface.occlusion = mask.g;
                surface.smoothness = mask.a * _Smoothness;
                ApplyRain(surface, input);

                InputData inputData = Inputs(input, surface.normalTS);
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surface, inputData);
                #endif
                half4 color = UniversalFragmentPBR(inputData, surface);
                return half4(color.rgb, surface.alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // One tile across the quad: the road's V runs in metres, so GI sees the tile's mean colour.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv0).rgb * _Tint.rgb;
            }
            ENDHLSL
        }
    }
}
