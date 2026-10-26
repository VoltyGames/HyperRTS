// Tree bark: URP Lit that bends with the same tree wind as HyperRTS/Foliage leaves (Wind.hlsl).
// Not lightmapped: the wind data sits in Lit's lightmap UV channels.
Shader "HyperRTS/Bark"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        [Toggle(_MASKMAP)] _UseMaskMap ("Use Mask Map", Float) = 0
        [NoScaleOffset] _MaskMap ("Mask (R metallic, G occlusion, A smoothness)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _WindScale ("Wind Scale (0 = still)", Range(0, 1)) = 1
        _WindStrength ("Trunk Sway (m per m of height)", Range(0, 0.2)) = 0.03
        _WindSpeed ("Wind Speed", Float) = 1.2
        _BranchSway ("Branch Sway (rad)", Range(0, 0.5)) = 0.12
        _TreeHeight ("Tree Height (object m)", Float) = 10
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "AlwaysRenderMotionVectors" = "true"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BarkForwardVertex
            #pragma fragment LitPassFragment
            #define BARK_PASS_FORWARD

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MASKMAP

            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ForwardLightingKeywords.hlsl"
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_FALLOFF_QUADRATIC
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _EXPOSURE
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BarkShadowVertex
            #pragma fragment ShadowPassFragment
            #define BARK_PASS_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BarkDepthVertex
            #pragma fragment DepthOnlyFragment
            #define BARK_PASS_DEPTH
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BarkDepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #define BARK_PASS_DEPTH_NORMALS
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BarkMotionVertex
            #pragma fragment BarkMotionFragment
            #define BARK_PASS_MOTION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkPasses.hlsl"
            ENDHLSL
        }

        // Albedo for Surface Cache GI, which bakes it over a UV quad, so without wind.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local_fragment _MASKMAP
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.hyperrts.engine/Presentation/Environment/BarkInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
