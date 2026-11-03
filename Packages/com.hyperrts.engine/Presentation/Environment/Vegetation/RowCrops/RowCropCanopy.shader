// The raised field surface RowCropCanopy draws past a crop's plants: the canopy tile laid on each field's rows, walls
// along the outline, lit like HyperRTS/RowCrop. Not for ordinary renderers: the canopy meshes lie on the ground and
// RowCropCanopy sets the crop's height, tile, fade and fields per draw.
Shader "HyperRTS/RowCropCanopy"
{
    Properties
    {
        [NoScaleOffset] _CanopyMap ("Canopy (RGB albedo, A height)", 2D) = "white" {}
        [NoScaleOffset] [Normal] _CanopyNormal ("Canopy Normal (X across the rows, Y along)", 2D) = "bump" {}
        [NoScaleOffset] _CanopyMask ("Canopy Mask (R occlusion, G coverage)", 2D) = "white" {}
        [NoScaleOffset] _WallMap ("Wall (RGBA, tiles along the edge)", 2D) = "white" {}
        [NoScaleOffset] [Normal] _WallNormal ("Wall Normal", 2D) = "bump" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Wall Alpha Cutoff", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _Translucency ("Translucency", Range(0, 1)) = 0.45
        _HueVariation ("Hue Variation", Range(0, 0.5)) = 0.1
        _ScreenOcclusion ("Screen Occlusion", Range(0, 1)) = 0.2
        _WindSheen ("Wind Sheen (gusts brighten the tops)", Range(0, 0.5)) = 0
        _ParallaxSteps ("Parallax Steps At Grazing Views", Range(8, 32)) = 20
        _CanopyShade ("Canopy Shade At The Roots (as the plants' material)", Range(0, 1)) = 0.6
        _BakedOcclusion ("Baked Occlusion Strength", Range(0, 1)) = 0.3
        _SunDepth ("Sun Fall-Off Into The Crop", Range(0, 8)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #pragma target 4.5
        ENDHLSL

        Pass
        {
            Name "RowCropCanopy"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex CanopyVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_fragment _ _CANOPY_PARALLAX
            #pragma multi_compile_fragment _ _CANOPY_SELF_SHADOW
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"
            #define CANOPY_FORWARD
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyPasses.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #define CANOPY_PREPASSES
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex CanopyVert
            #pragma fragment DepthFrag
            #pragma multi_compile_fragment _ _CANOPY_PARALLAX
            #define CANOPY_PREPASSES
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex CanopyVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _CANOPY_PARALLAX
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #define CANOPY_PREPASSES
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyPasses.hlsl"
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
            #define CANOPY_PREPASSES
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyPasses.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyInput.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // The canopy meshes carry no albedo UVs; the tile's mean colour stands for the whole field.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D_LOD(_CanopyMap, sampler_TrilinearRepeat, float2(0.5, 0.5), 16).rgb * _BaseColor.rgb;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
