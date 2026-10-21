// HyperRTS/Foliage drawn from Grass.compute's instances: GrassRenderer copies each plant's Foliage material onto it.
// Not for ordinary renderers: without _GrassInstances there is nothing to draw.
Shader "Hidden/HyperRTS/FoliageGrass"
{
    Properties
    {
        _BaseMap ("Albedo (A = coverage)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _Translucency ("Translucency", Range(0, 1)) = 0.45
        _HueVariation ("Hue Variation", Range(0, 0.5)) = 0.12
        [Toggle] _TwoSidedLeaves ("Two-Sided Leaves (modelled plants)", Float) = 0
        _WindScale ("Wind Scale (0 = still)", Range(0, 1)) = 1
        _WindStrength ("Wind Sway (m per m of height)", Range(0, 0.2)) = 0.03
        _WindSpeed ("Wind Speed", Float) = 1.2
        _Flutter ("Plant Flutter (m)", Range(0, 0.2)) = 0.03
        _GroundBlend ("Ground Colour At Roots", Range(0, 1)) = 0
        _GroundBlendHeight ("Ground Colour Height (UV)", Range(0.01, 1)) = 0.4
        _VertexOcclusion ("Crown Occlusion (vertex R)", Range(0, 1)) = 0
        [Enum(Off, 0, Grass, 1, Crop, 2)] _Trample ("Trample", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "AlwaysRenderMotionVectors" = "true"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #define _GRASS_INSTANCES 1
        ENDHLSL

        Pass
        {
            Name "Foliage"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex CutoutVert
            #pragma fragment DepthFrag
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex CutoutVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MotionVert
            #pragma fragment MotionFrag
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }
    }
}
