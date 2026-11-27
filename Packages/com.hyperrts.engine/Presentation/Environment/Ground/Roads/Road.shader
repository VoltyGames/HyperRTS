// Road on a ribbon mesh over the terrain: U across (0..1), V along in metres, vertex alpha fading ends and junctions.
// No depth write, so decals, SSAO and fog of war still see the terrain under it. Needs mesh normals and tangents.
// Follows craters and takes the terrain's stains (TerrainStains.hlsl), so its _Stain* properties are set alike.
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
        _PatchShade ("Patch Shade (broad light and dark patches that break the repeat)", Range(0, 0.3)) = 0.12
        _EdgeGround ("Edge Ground (how much of the ground's colour the worn edge takes)", Range(0, 1)) = 0.5
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
        [NoScaleOffset] _StainCraterAlbedo ("Stain Crater Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _StainCraterNormal ("Stain Crater Normal", 2D) = "bump" {}
        [NoScaleOffset] _StainCraterMask ("Stain Crater Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _StainCraterTile ("Stain Crater Tile (m)", Float) = 4
        _StainCraterSmoothness ("Stain Crater Smoothness (x mask A)", Range(0, 1)) = 0.3
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
            half _PatchShade;
            half _EdgeGround;
            half _WetDarkening;
            half _WetSmoothness;
            half _PuddleCoverage;
            half _PuddleSmoothness;
            float _PuddleNoiseSize;
            half _RutOffset;
            half _RutWidth;
            half _RutPuddles;
            half _EdgePuddles;
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainStainInput.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/LightingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/InstancingKeywords.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Roads/RoadForward.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/MetaPass.hlsl"

            // One tile across the quad: the road's V runs in metres, so GI sees the tile's mean colour.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv0).rgb * _Tint.rgb;
            }
            ENDHLSL
        }
    }
}
