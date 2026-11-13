// URP Lit (minus GBuffer, so Forward or Forward+; opaque or alpha tested, metallic workflow, no UV-mapped detail maps or
// parallax) with the albedo repainted in _TeamColor.
Shader "HyperRTS/LitTeam"
{
    Properties
    {
        [Header(Team Colour)]
        _TeamColor("Team Color", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _TeamMask("Team Mask (R)", 2D) = "white" {}
        _TeamStrip("Team Strip (bottom, top)", Vector) = (-10000, 10000, 0, 0)

        [Header(Vertex Animation)]
        [NoScaleOffset] _VATPositions("VAT Positions (rigid)", 2D) = "black" {}
        [NoScaleOffset] _VATRotations("VAT Rotations (rigid)", 2D) = "black" {}
        _VATClip("VAT Clip (start frame, frames, fps, start time)", Vector) = (0, 1, 1, 0)

        [Header(Bone Animation)]
        [NoScaleOffset] _BoneTex("Bone Matrices", 2D) = "black" {}
        _LowerClip("Lower Body Clip (start frame, frames, fps, start time)", Vector) = (0, 1, 1, 0)
        _UpperClip("Upper Body Clip (start frame, frames, fps, start time)", Vector) = (0, 1, 1, 0)
        _LowerFade("Lower Body Fade (frame, next frame, blend, switch time)", Vector) = (0, 0, 0, -100000)
        _UpperFade("Upper Body Fade (frame, next frame, blend, switch time)", Vector) = (0, 0, 0, -100000)
        _BoneFadeTime("Bone Fade Time (s)", Float) = 0.2
        [HideInInspector] _PoseSlot("Pose Slot (live pose; below 0 plays the clips)", Float) = -1

        [Header(Grime)]
        _Grime("Grime (dust, mud, wet, soot)", Vector) = (0, 0, 0, 0)
        _GrimeBand("Grime Band (ground y in object space, mud top, dust top above it, m)", Vector) = (0, 0.7, 1.6, 0)

        [Header(Detail)]
        [Toggle(_DETAIL)] _Detail("Detail Maps (world scale, fade with camera distance)", Float) = 0
        [NoScaleOffset] _DetailAlbedo("Detail Albedo (grey 0.5 is neutral)", 2D) = "grey" {}
        [NoScaleOffset] _DetailNormal("Detail Normal", 2D) = "bump" {}
        _DetailTiling("Detail Tiling (repeats per metre)", Float) = 3
        _DetailAlbedoStrength("Detail Albedo Strength", Range(0, 1)) = 0.5
        _DetailNormalStrength("Detail Normal Strength", Range(0, 2)) = 1
        _DetailFadeStart("Detail Fade Start (camera distance, m)", Float) = 12
        _DetailFadeEnd("Detail Fade End (camera distance, m)", Float) = 30

        [Header(Track)]
        _TrackLift0("Track Lift (stations 0 to 3, m)", Vector) = (0, 0, 0, 0)
        _TrackLift1("Track Lift (stations 4 to 7, m)", Vector) = (0, 0, 0, 0)
        _TrackSpan("Track Span (front z, back z, axle y, wheel radius)", Vector) = (0, 0, 0, 0)

        [Header(Ground)]
        _CraterSink("Crater Sink (1 drops into a crater under it, 0 for a model that brings its own dent)", Range(0, 1)) = 1

        [Header(Construction)]
        [Toggle(_BUILD)] _Build("Build Piece By Piece (UV3)", Float) = 0
        _BuildProgress("Build Progress", Range(0, 1)) = 1
        [HideInInspector] _Reveal("Reveal (fog fade)", Range(0, 1)) = 1
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

        [ToggleUI] _Transparent("Transparent (glass: base alpha fades the diffuse, reflections stay whole)", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
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

        HLSLINCLUDE
        #pragma use_dxc
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DisplacedLitVertex
            #pragma fragment LitTeamPassFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"

            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamForwardKeywords.hlsl"

            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamForwardPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamPasses.hlsl"
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
            #pragma vertex DisplacedLitVertex
            #pragma fragment ShadowPassFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #pragma shader_feature_local _ALPHATEST_ON
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            // Directional and punctual light shadows apply normal bias differently.
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamPasses.hlsl"
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
            #pragma vertex DisplacedLitVertex
            #pragma fragment DepthOnlyFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamPasses.hlsl"
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
            #pragma vertex DisplacedLitVertex
            #pragma fragment DepthNormalsFragment
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamDepthNormalsKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamPasses.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamSilhouettePass.hlsl"
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
            #define SILHOUETTE_SLACK_ON
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamSilhouettePass.hlsl"
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
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            // ObjectMotionVectors.hlsl's own pragmas, minus its vertex entry, which DisplacedLitVertex replaces.
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DisplacedLitVertex
            #pragma fragment frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVertexKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ObjectMotionVectors.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "HyperRTS.Editor.Rendering.LitTeamGUI"
}
