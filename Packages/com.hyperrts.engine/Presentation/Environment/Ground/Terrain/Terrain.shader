// Map ground: up to 16 terrain layers blended in one pass, lit by URP PBR. Use it as Terrain.materialTemplate.
// TerrainDetail picks a cheaper variant (compile-time switch): _TERRAIN_SIMPLE, _TERRAIN_NO_FAR, _TERRAIN_NO_PARALLAX.
Shader "HyperRTS/Terrain"
{
    Properties
    {
        [NoScaleOffset] _AlbedoArray ("Albedo (RGB) Height (A)", 2DArray) = "" {}
        [NoScaleOffset] _NormalArray ("Normals", 2DArray) = "" {}
        [NoScaleOffset] _MaskArray ("Occlusion (R) Smoothness (G)", 2DArray) = "" {}
        [NoScaleOffset] _LayerParams ("Layer Parameters", 2D) = "black" {}
        [IntRange] _LayerCount ("Layer Count", Range(1, 16)) = 1
        [NoScaleOffset] _Control0 ("Alphamap 0", 2D) = "red" {}
        [NoScaleOffset] _Control1 ("Alphamap 1", 2D) = "black" {}
        [NoScaleOffset] _Control2 ("Alphamap 2", 2D) = "black" {}
        [NoScaleOffset] _Control3 ("Alphamap 3", 2D) = "black" {}
        [NoScaleOffset] _GroundTintMap ("Ground Tint (RGB) Wetness (A)", 2D) = "grey" {}
        [NoScaleOffset] _GroundColorMap ("Ground Colour (GI albedo)", 2D) = "grey" {}
        [NoScaleOffset] _RowMap ("Row Direction (RG)", 2D) = "grey" {}
        [NoScaleOffset] _NoiseMap ("Noise", 2D) = "grey" {}
        [NoScaleOffset] _MicroMap ("Micro Detail", 2D) = "bump" {}
        _HeightBlendDepth ("Height Blend Depth", Range(0.02, 0.5)) = 0.15
        _ControlWarp ("Layer Edge Warp (alphamap texels)", Range(0, 2)) = 0.8
        _ControlWarpSize ("Layer Edge Warp Noise Size (m)", Float) = 3
        _ReliefStart ("Relief Full Until (m)", Float) = 20
        _ReliefEnd ("Relief Faded At (m)", Float) = 60
        _FarRelief ("Far Relief (share of layer normals kept)", Range(0, 1)) = 0.4
        _FarTiling ("Far Tiling (x layer tiling)", Range(0.05, 1)) = 0.25
        _FarStart ("Far Start (m)", Float) = 40
        _FarEnd ("Far End (m)", Float) = 160
        _FarBlend ("Far Blend", Range(0, 1)) = 0.5
        _Variation ("Variation", Range(0, 0.5)) = 0.2
        _VariationSize ("Variation Noise Size (m)", Float) = 400
        _GrassPatches ("Grass Patch Variation (dry and green)", Range(0, 0.4)) = 0.15
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.45
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.7
        _RainDarkening ("Rain Darkening", Range(0, 1)) = 0.5
        _RainSmoothness ("Rain Smoothness", Range(0, 1)) = 0.45
        _GrassWetness ("Grass Wetness (of bare soil's)", Range(0, 1)) = 0.5
        _PuddleCoverage ("Puddle Coverage (at full wetness)", Range(0, 1)) = 0.6
        _PuddleSmoothness ("Puddle Smoothness", Range(0, 1)) = 0.95
        _PuddleNoiseSize ("Puddle Noise Size (m)", Float) = 8
        _MicroTiling ("Micro Detail Tile (m)", Float) = 0.5
        _MicroNormal ("Micro Detail Normal", Range(0, 2)) = 0.8
        _MicroOcclusion ("Micro Detail Occlusion", Range(0, 1)) = 0.9
        _MicroAlbedo ("Micro Detail Albedo", Range(0, 2)) = 1.3
        _MicroStart ("Micro Detail Full Until (m)", Float) = 24
        _MicroEnd ("Micro Detail End (m)", Float) = 42
        _TriplanarStart ("Triplanar Start (degrees)", Range(0, 90)) = 30
        _TriplanarFull ("Triplanar Full (degrees)", Range(0, 90)) = 42
        _ParallaxDepth ("Parallax Depth (m)", Range(0, 0.3)) = 0.07
        _ParallaxEnd ("Parallax End (m)", Float) = 22
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
        [HideInInspector] _TerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-100"
            "UniversalMaterialType" = "Lit"
            "TerrainCompatible" = "True"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #pragma use_dxc
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
        #pragma multi_compile_fragment _ _ALPHATEST_ON
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex TerrainForwardVertex
            #pragma fragment TerrainForwardFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/ForwardLightingKeywords.hlsl"
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE _TERRAIN_NO_FAR _TERRAIN_NO_PARALLAX
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex TerrainShadowVertex
            #pragma fragment TerrainShadowFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex TerrainDepthVertex
            #pragma fragment TerrainDepthFragment
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex TerrainDepthVertex
            #pragma fragment TerrainDepthNormalsFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Keywords/DepthNormalsKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }

            HLSLPROGRAM
            #pragma vertex TerrainDepthVertex
            #pragma fragment TerrainDepthFragment
            #define SCENESELECTIONPASS
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainDepthPasses.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainInput.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/MetaPass.hlsl"

            TEXTURE2D(_GroundColorMap);
            SAMPLER(sampler_GroundColorMap);

            // UV0 spans the whole terrain, as the ground colour picture does.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D(_GroundColorMap, sampler_GroundColorMap, uv0).rgb;
            }
            ENDHLSL
        }

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
