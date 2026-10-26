// Alpha-cut vegetation: two-sided wrap lighting with translucency and wind (Wind.hlsl).
// Tree Wind reads the mesh's tree wind data. Without it plants sway with height and lean where trampled.
Shader "HyperRTS/Foliage"
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
        [Toggle(_TREE_WIND)] _TreeWind ("Tree Wind (mesh has wind data)", Float) = 0
        _WindScale ("Wind Scale (0 = still)", Range(0, 1)) = 1
        _WindStrength ("Wind Sway (m per m of height)", Range(0, 0.2)) = 0.03
        _WindSpeed ("Wind Speed", Float) = 1.2
        _Flutter ("Plant Flutter (m)", Range(0, 0.2)) = 0.03
        _BranchSway ("Tree Branch Sway (rad)", Range(0, 0.5)) = 0.12
        _CardFlutter ("Tree Card Flutter (rad)", Range(0, 1)) = 0.25
        _TreeHeight ("Tree Height (object m)", Float) = 10
        _GroundBlend ("Ground Colour At Roots", Range(0, 1)) = 0
        _GroundBlendHeight ("Ground Colour Height (UV)", Range(0.01, 1)) = 0.4
        _VertexOcclusion ("Canopy Occlusion (vertex R)", Range(0, 1)) = 0
        [Enum(Off, 0, Grass, 1, Crop, 2)] _Trample ("Trample (Tree Wind off)", Float) = 1
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
        ENDHLSL

        Pass
        {
            Name "Foliage"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag
            #pragma shader_feature_local _TREE_WIND
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
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
            #pragma shader_feature_local _TREE_WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
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
            #pragma shader_feature_local _TREE_WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
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
            #pragma shader_feature_local _TREE_WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
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
            #pragma shader_feature_local _TREE_WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliagePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Environment/FoliageInput.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // Per-plant tint and root ground colour are per vertex, so only the texture and material tint reach GI.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv0, _BaseMap)).rgb * _BaseColor.rgb;
            }
            ENDHLSL
        }
    }
}
