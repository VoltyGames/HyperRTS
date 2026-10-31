// URP Lit (minus GBuffer, so Forward or Forward+; opaque or alpha tested, metallic workflow, no detail maps or
// parallax) with the albedo repainted in _TeamColor.
//   _TeamColor  linear RGB, A how much shows (0 untinted, so neutral needs no special case)
//   _TeamMask   R where team colour goes, on UV0
//   _TeamStrip  x bottom, y top of the band (object-space metres) the mask applies in
//   _VAT        keyword: vertices come from _VATPositions and _VATNormals at _VATClip, texel in UV1 (LitTeamVAT.hlsl)
//   _BUILD      keyword (structures only): pieces appear in their UV3 build order up to _BuildProgress (LitTeamBuild.hlsl)
//   _LightsOn   emission multiplier, 0 dark to 1 lit (structures go dark while offline)
// TeamColorSystem writes _TeamColor and _TeamStrip per entity (DOTS instancing), BuildRevealSystem writes _BuildProgress,
// BuildingLightsSystem writes _LightsOn, game code writes VATClip.
Shader "HyperRTS/LitTeam"
{
    Properties
    {
        [Header(Team Colour)]
        _TeamColor("Team Color", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _TeamMask("Team Mask (R)", 2D) = "white" {}
        _TeamStrip("Team Strip (bottom, top)", Vector) = (-10000, 10000, 0, 0)

        [Header(Vertex Animation)]
        [NoScaleOffset] _VATPositions("VAT Positions", 2D) = "black" {}
        [NoScaleOffset] _VATNormals("VAT Normals", 2D) = "black" {}
        _VATClip("VAT Clip (start frame, frames, fps, start time)", Vector) = (0, 1, 1, 0)
        _VATRowsPerFrame("VAT Rows Per Frame", Float) = 1

        [Header(Construction)]
        [Toggle(_BUILD)] _Build("Build Piece By Piece (UV3)", Float) = 0
        _BuildProgress("Build Progress", Range(0, 1)) = 1
        _BuildDropHeight("Build Drop Height (m)", Float) = 0.5
        _BuildDropSlice("Build Drop Slice (of progress)", Range(0.01, 1)) = 0.05
        _BuildDropFrom("Build Drop From (order; earlier pieces appear in place)", Range(0, 1)) = 0

        [Header(Surface)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic (R) Smoothness (A) Map", 2D) = "white" {}

        _BumpScale("Normal Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}

        [Header(Emission)]
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        _LightsOn("Lights On", Range(0, 1)) = 1
        _EmissionMap("Emission Map", 2D) = "white" {}

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2.0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitTeamForwardVertex
            #pragma fragment LitTeamPassFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP

            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ForwardLightingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ObjectLightingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/LightmapKeywords.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamForwardPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitTeamShadowVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            // Directional and punctual light shadows apply normal bias differently.
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitTeamDepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitTeamDepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }

        // UnitSilhouetteFeature: marks stencil bit 128 where the unit itself is visible.
        Pass
        {
            Name "UnitMask"
            Tags { "LightMode" = "HyperRTSUnitMask" }

            ZTest Equal
            ZWrite Off
            ColorMask 0
            Cull[_Cull]
            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SilhouetteVertex
            #pragma fragment UnitMaskFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamSilhouettePass.hlsl"
            ENDHLSL
        }

        // UnitSilhouetteFeature: team colour where something hides the unit, skipping FoliageGrass's ground cover (bit
        // 64). Inverting the clear bit 128 sets it, so each pixel blends once.
        Pass
        {
            Name "UnitSilhouette"
            Tags { "LightMode" = "HyperRTSSilhouette" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Greater
            ZWrite Off
            Cull[_Cull]
            Stencil
            {
                Ref 0
                ReadMask 192
                WriteMask 128
                Comp Equal
                Pass Invert
            }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SilhouetteVertex
            #pragma fragment SilhouetteFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamSilhouettePass.hlsl"
            ENDHLSL
        }

        // Lightmap baking only.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            // ObjectMotionVectors.hlsl's own pragmas, minus its vertex entry, so the VAT wrapper can replace it.
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitTeamMotionVectorsVertex
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ObjectMotionVectors.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "HyperRTS.Editor.Rendering.LitTeamGUI"
}
