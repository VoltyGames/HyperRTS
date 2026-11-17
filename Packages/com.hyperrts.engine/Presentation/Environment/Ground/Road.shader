// Road on a ribbon mesh over the terrain: U across (0..1), V along in metres, vertex alpha fading ends and junctions.
// No depth write, so decals, SSAO and the fog of war see the terrain underneath and cover the road too.
// Needs mesh normals and tangents. Follows craters and rims in the trample maps, breaks where a bowl is deep, and takes
// the scorch, ash and rubble stains the terrain does (TerrainStains.hlsl), so the _Stain* properties are set like the terrain's.
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
        [NoScaleOffset] _StainRubbleAlbedo ("Stain Rubble Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _StainRubbleNormal ("Stain Rubble Normal", 2D) = "bump" {}
        [NoScaleOffset] _StainRubbleMask ("Stain Rubble Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _StainRubbleTile ("Stain Rubble Tile (m)", Float) = 2
        _StainRubbleSmoothness ("Stain Rubble Smoothness (x mask A)", Range(0, 1)) = 0.5
        [NoScaleOffset] _StainAshAlbedo ("Stain Ash Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _StainAshNormal ("Stain Ash Normal", 2D) = "bump" {}
        [NoScaleOffset] _StainAshMask ("Stain Ash Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _StainAshTile ("Stain Ash Tile (m)", Float) = 3
        _StainAshSmoothness ("Stain Ash Smoothness (x mask A)", Range(0, 1)) = 0.3
        _StainCharColor ("Char Colour (scorch under blasts)", Color) = (0.03, 0.027, 0.024, 1)
        [NoScaleOffset] _StainMudAlbedo ("Stain Mud Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _StainMudNormal ("Stain Mud Normal", 2D) = "bump" {}
        [NoScaleOffset] _StainMudMask ("Stain Mud Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _StainMudTile ("Stain Mud Tile (m)", Float) = 3
        _StainMudSmoothness ("Stain Mud Smoothness (x mask A)", Range(0, 1)) = 0.5
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
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainStainInput.hlsl"
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
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/LightingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Craters/CraterSink.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainStains.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/BuildingFootprints.hlsl"

            // A bowl this deep breaks the road: it is gone by the far depth.
            #define ROAD_BREAK_NEAR 0.2
            #define ROAD_BREAK_FAR 0.4

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
                // The ribbon spans many craters, so each vertex follows the ground at its own spot.
                float3 positionWS = position.positionWS;
                positionWS.y -= CraterDepth(positionWS.xz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.opacity = input.color.a;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLighting = VertexLighting(position.positionWS, normals.normalWS);
                #endif
                return output;
            }

            // Noise decides coverage toward the sides, so gravel thins in patches instead of along a clean line. A road
            // never draws where a building stands, so it can't show through a yard, a pit or a silo.
            half Coverage(Varyings input, half albedoAlpha, float4 tracks)
            {
                half unbroken = 1.0 - smoothstep(ROAD_BREAK_NEAR, ROAD_BREAK_FAR, CraterDepth(input.positionWS.xz, tracks));
                half yard = 1.0 - smoothstep(0.3, 0.7, BuildingCover(input.positionWS.xz));
                half inside = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                half fade = saturate(inside / _EdgeWidth);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz / _NoiseSize).r;
                half crumble = saturate(fade * (1.0 + _EdgeNoise) - noise * _EdgeNoise);
                return smoothstep(0.0, 1.0, crumble) * albedoAlpha * input.opacity * yard * unbroken;
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
                half puddles = PuddleCoverage(PuddleCollect(input, st, dx, dy), _PuddleCoverage);
                Puddle(surface.albedo, surface.smoothness, surface.normalTS, puddles, _PuddleSmoothness);
            }

            // A hard road holds less ash than soil, so it still reads as a road through a burn.
            #define ROAD_ASH 0.45

            // The terrain's scorch and stains, over the road's own surface.
            void ApplyScars(inout SurfaceData surface, Varyings input, float4 tracks)
            {
                TerrainSurface ground = (TerrainSurface)0;
                ground.albedo = surface.albedo;
                ground.normalTS = surface.normalTS;
                ground.smoothness = surface.smoothness;
                ground.occlusion = surface.occlusion;

                TerrainPixel pixel = (TerrainPixel)0;
                pixel.positionWS = input.positionWS;
                pixel.xz = input.positionWS.xz;
                pixel.dx = ddx(pixel.xz);
                pixel.dy = ddy(pixel.xz);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                pixel.frame = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);

                ApplyScorch(ground, tracks.b);
                ApplyStains(ground, pixel, ROAD_ASH);
                surface.albedo = ground.albedo;
                surface.normalTS = ground.normalTS;
                surface.smoothness = ground.smoothness;
                surface.occlusion = ground.occlusion;
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
                float4 tracks = SampleTracks(input.positionWS.xz);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = Coverage(input, albedo.a, tracks);
                surface.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                surface.occlusion = mask.g;
                surface.smoothness = mask.a * _Smoothness;
                ApplyScars(surface, input, tracks);
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
            #pragma use_dxc
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
