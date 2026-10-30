// URP Unlit particles with the engine's height fog and fog of war on transparents.
Shader "HyperRTS/ParticlesUnlit"
{
    // The URP particle inspector reads every property, so the block matches Particles/Unlit.
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0
        _DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 0.5
        _DistortionStrength("Distortion Strength", Float) = 1.0

        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _ReceiveFog("__receiveFog", Float) = 0.0
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

        [Toggle(_FLIPBOOK_MOTION)] _FlipbookMotion("Blend Frames Along Motion", Float) = 0.0
        [NoScaleOffset] _MotionMap("Motion Map (RG move to the next frame)", 2D) = "gray" {}
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

            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vertParticleUnlit
            #pragma fragment ParticlesFogFragment

            #pragma shader_feature_local _SOFTPARTICLES_ON
            #pragma shader_feature_local _FADING_ON
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/FlipbookMotionKeywords.hlsl"
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON

            #pragma multi_compile_fragment _ _EXPOSURE
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DEPTH_AS_INPUT_ATTACHMENT _DEPTH_AS_INPUT_ATTACHMENT_MSAA
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC
            // URP's particle forward pass then fogs transparent particles itself.
            #define _TRANSPARENT_RECEIVE_FOG 1
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitForwardPass.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/ParticlesFog.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/FlipbookMotion.hlsl"

            half4 ParticlesFogFragment(VaryingsParticle input) : SV_Target
            {
                #if defined(_FLIPBOOK_MOTION) && defined(_FLIPBOOKBLENDING_ON)
                    float2 current = input.texcoord;
                    float2 next = input.texcoord2AndBlend.xy;
                    FlipbookMotionWarp(current, next, input.texcoord2AndBlend.z);
                    input.texcoord = current;
                    input.texcoord2AndBlend.xy = next;
                #endif
                return FogParticle(fragParticleUnlit(input), input.positionWS.xyz);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Particles/Unlit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Particles/Unlit/SCENESELECTIONPASS"
        UsePass "Universal Render Pipeline/Particles/Unlit/SCENEPICKINGPASS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.ParticlesUnlitShader"
}
