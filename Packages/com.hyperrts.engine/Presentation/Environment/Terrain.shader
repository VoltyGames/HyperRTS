// Map ground: up to 16 terrain layers in one pass from texture arrays, blended by alphamap weight and texel height,
// with hex tiling, a far sample, per-layer UV rotation, a per-map macro tint and wetness map and a world-noise
// brightness variation, lit by URP's PBR model like other lit objects. Use it as Terrain.materialTemplate (instanced
// terrain with per-pixel normals, holes); the map tools build its inputs:
//   _AlbedoArray  RGB albedo (sRGB), A height (linear, stretched to 0..1 per layer)
//   _NormalArray  normal maps, u east and v north before the layer's rotation
//   _MaskArray    R occlusion, G smoothness (linear)
//   _LayerParams  16 x 4 floats, point-sampled, one column per layer:
//                 row 0  cos, sin of the clockwise UV rotation; 1 / tile size along u, v (m)
//                 row 1  tint RGB; macro strength
//                 row 2  smoothness scale, offset; occlusion scale, offset
//                 row 3  height-blend weight; normal strength; hex rotation strength (0..1 of a full turn);
//                        0 for layers that follow rows (turned on by _RowMap, hex cells slide only along their lines), else 1
//   _Control0..3  the terrain's alphamaps, four layers each, for _LayerCount layers
//   _MacroMap     over the whole terrain, linear: RGB tint (0.5 neutral, doubled), A wetness
//   _RowMap       over the whole terrain, linear, bilinear, no mips: RG the crop rows' direction (x east, y north)
//                 remapped to 0..1, (0.5, 0.5) off the fields; row layers lay u along it
// Every layer is bound in this one pass, so the terrain needs no add-pass or basemap shaders. TerrainDetail sets
// _TERRAIN_SIMPLE (one sample per texture) or _TERRAIN_NO_FAR (hex, no far sample); neither is the full ground.
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
        [NoScaleOffset] _MacroMap ("Macro Tint (RGB) Wetness (A)", 2D) = "grey" {}
        [NoScaleOffset] _RowMap ("Row Direction (RG)", 2D) = "grey" {}
        [NoScaleOffset] _NoiseMap ("Noise", 2D) = "grey" {}
        _HeightBlendDepth ("Height Blend Depth", Range(0.02, 0.5)) = 0.12
        _FarTiling ("Far Tiling (x layer tiling)", Range(0.05, 1)) = 0.25
        _FarStart ("Far Start (m)", Float) = 40
        _FarEnd ("Far End (m)", Float) = 160
        _FarBlend ("Far Blend", Range(0, 1)) = 0.5
        _Variation ("Variation", Range(0, 0.5)) = 0.2
        _VariationSize ("Variation Noise Size (m)", Float) = 400
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.45
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.7
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _TERRAIN_SIMPLE _TERRAIN_NO_FAR
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
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
