// URP Simple Lit particles with the engine's height fog and fog of war on transparents.
Shader "HyperRTS/Particles"
{
    // The URP particle inspector reads every property, so the block matches Particles/Simple Lit.
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _SpecGlossMap("Specular", 2D) = "white" {}
        _SpecColor("Specular", Color) = (1.0, 1.0, 1.0)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _SmoothnessSource("Smoothness Source", Float) = 0.0
        _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0
        _DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 0.5
        _DistortionStrength("Distortion Strength", Float) = 1.0

        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _ReceiveFog("__receiveFog", Float) = 1.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        _ColorMode("_ColorMode", Float) = 0.0
        [HideInInspector] _BaseColorAddSubDiff("_ColorMode", Vector) = (0,0,0,0)
        [ToggleOff] _FlipbookBlending("__flipbookblending", Float) = 0.0
        [ToggleUI] _SoftParticlesEnabled("__softparticlesenabled", Float) = 0.0
        [ToggleUI] _CameraFadingEnabled("__camerafadingenabled", Float) = 0.0
        [ToggleUI] _DistortionEnabled("__distortionenabled", Float) = 0.0
        [HideInInspector] _SoftParticleFadeParams("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadeParams("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionStrengthScaled("Distortion Strength Scaled", Float) = 0.1
        _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "PerformanceChecks" = "False"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ParticlesLitVertex
            #pragma fragment ParticlesFogFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #pragma shader_feature_local _FADING_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_FALLOFF_QUADRATIC
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DEPTH_AS_INPUT_ATTACHMENT _DEPTH_AS_INPUT_ATTACHMENT_MSAA
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _EXPOSURE
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define BUMP_SCALE_NOT_SUPPORTED 1
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesSimpleLitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesSimpleLitForwardPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/ParticlesFog.hlsl"

            half4 ParticlesFogFragment(VaryingsParticle input) : SV_Target
            {
                return FogParticle(ParticlesLitFragment(input), input.positionWS.xyz);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Particles/Simple Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Particles/Simple Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Particles/Simple Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Particles/Simple Lit/SCENESELECTIONPASS"
        UsePass "Universal Render Pipeline/Particles/Simple Lit/SCENEPICKINGPASS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.ParticlesSimpleLitShader"
}
