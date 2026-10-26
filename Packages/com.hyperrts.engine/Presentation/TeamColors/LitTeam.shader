// URP Lit (minus GBuffer, so Forward or Forward+) with the albedo repainted in _TeamColor.
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

        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _LightsOn("Lights On", Range(0, 1)) = 1
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

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

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitTeamForwardVertex
            #pragma fragment LitTeamPassFragment
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #pragma target 3.5 _VAT

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ForwardLightingKeywords.hlsl"
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_FALLOFF_QUADRATIC
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _EXPOSURE
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

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
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
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
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
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
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONSCONTRIBUTETRANSPARENT_OFF
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
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
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
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #pragma shader_feature_local_vertex _VAT
            #pragma shader_feature_local_vertex _BUILD
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ObjectMotionVectors.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
