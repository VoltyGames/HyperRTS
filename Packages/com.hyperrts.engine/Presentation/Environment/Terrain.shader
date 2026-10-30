// Map ground: up to 16 terrain layers in one pass, lit by URP PBR. Use it as Terrain.materialTemplate (instanced,
// per-pixel normals, holes). The map tools build its inputs:
//   _AlbedoArray  RGB albedo (sRGB), A height (linear, stretched to 0..1 per layer)
//   _NormalArray  normal maps, u east and v north before the layer's rotation
//   _MaskArray    R occlusion, G smoothness (linear)
//   _LayerParams  16 x 5 floats, point-sampled, one column per layer:
//                 row 0  cos, sin of the clockwise UV rotation; 1 / tile size along u, v (m)
//                 row 1  tint RGB; tint strength
//                 row 2  smoothness scale, offset; occlusion scale, offset
//                 row 3  height-blend weight; normal strength; hex rotation strength (0..1 of a full turn);
//                        0 for row layers (hex cells slide only along their lines), else 1
//                 row 4  grass cover 0..1 (sheds rain); 1 for the slope layer, triplanar on steep ground; z, w unused
//   _Control0..3  the terrain's alphamaps, four layers each, for _LayerCount layers
//   _GroundTintMap over the whole terrain, linear: RGB tint (0.5 neutral, doubled), A wetness (damp hollows)
//   _RowMap       over the whole terrain, linear, bilinear, no mips: RG the crop rows' direction (x east, y north)
//                 remapped to 0..1, (0.5, 0.5) off the fields; row layers lay u along it
//   _MicroMap     TerrainMicroTexture's tileable soil grain, linear: RG normal xy, B occlusion, A albedo variation
//   _Remains*     rubble, ash and wet mud tiled over what _HyperRTSRemains covers (TerrainRemains.hlsl); fuel is untextured
// One pass binds every layer, so the terrain needs no add-pass or basemap shaders.
// TerrainDetail sets _TERRAIN_SIMPLE (one sample per texture), _TERRAIN_NO_FAR (hex, no far sample) or
// _TERRAIN_NO_PARALLAX (everything but parallax).
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
        [NoScaleOffset] _RowMap ("Row Direction (RG)", 2D) = "grey" {}
        [NoScaleOffset] _NoiseMap ("Noise", 2D) = "grey" {}
        [NoScaleOffset] _MicroMap ("Micro Detail", 2D) = "bump" {}
        _HeightBlendDepth ("Height Blend Depth", Range(0.02, 0.5)) = 0.06
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
        _MicroNormal ("Micro Detail Normal", Range(0, 2)) = 1.4
        _MicroOcclusion ("Micro Detail Occlusion", Range(0, 1)) = 0.9
        _MicroAlbedo ("Micro Detail Albedo", Range(0, 2)) = 1.3
        _MicroStart ("Micro Detail Full Until (m)", Float) = 24
        _MicroEnd ("Micro Detail End (m)", Float) = 42
        _TriplanarStart ("Triplanar Start (degrees)", Range(0, 90)) = 30
        _TriplanarFull ("Triplanar Full (degrees)", Range(0, 90)) = 42
        _ParallaxDepth ("Parallax Depth (m)", Range(0, 0.3)) = 0.1
        _ParallaxEnd ("Parallax End (m)", Float) = 38
        [NoScaleOffset] _RemainsRubbleAlbedo ("Remains Rubble Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _RemainsRubbleNormal ("Remains Rubble Normal", 2D) = "bump" {}
        [NoScaleOffset] _RemainsRubbleMask ("Remains Rubble Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _RemainsRubbleTile ("Remains Rubble Tile (m)", Float) = 2
        _RemainsRubbleSmoothness ("Remains Rubble Smoothness (x mask A)", Range(0, 1)) = 0.5
        [NoScaleOffset] _RemainsAshAlbedo ("Remains Ash Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _RemainsAshNormal ("Remains Ash Normal", 2D) = "bump" {}
        [NoScaleOffset] _RemainsAshMask ("Remains Ash Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _RemainsAshTile ("Remains Ash Tile (m)", Float) = 3
        _RemainsAshSmoothness ("Remains Ash Smoothness (x mask A)", Range(0, 1)) = 0.3
        _RemainsCharColor ("Remains Char Colour (heavy ash)", Color) = (0.03, 0.027, 0.024, 1)
        _RemainsCharStreaks ("Remains Char Ash Smudges (threshold below 1)", Range(0, 1)) = 0.38
        [NoScaleOffset] _RemainsMudAlbedo ("Remains Mud Albedo", 2D) = "grey" {}
        [NoScaleOffset][Normal] _RemainsMudNormal ("Remains Mud Normal", 2D) = "bump" {}
        [NoScaleOffset] _RemainsMudMask ("Remains Mud Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _RemainsMudTile ("Remains Mud Tile (m)", Float) = 3
        _RemainsMudSmoothness ("Remains Mud Smoothness (x mask A)", Range(0, 1)) = 0.5
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ForwardLightingKeywords.hlsl"
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE _TERRAIN_NO_FAR _TERRAIN_NO_PARALLAX
            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainForwardPass.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainDepthPasses.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainDepthPasses.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainDepthPasses.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainDepthPasses.hlsl"
            ENDHLSL
        }

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
