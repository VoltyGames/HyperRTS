// Six-way lit Shuriken smoke and fire. Streams: Position, Normal, Tangent, Color, UV, UV2, AnimBlend, Custom1.x
Shader "HyperRTS/ParticlesSixWay"
{
    Properties
    {
        [NoScaleOffset] _PositiveMap ("Positive Map (R right, G top, B back, A opacity)", 2D) = "black" {}
        [NoScaleOffset] _NegativeMap ("Negative Map (R left, G bottom, B front, A emission)", 2D) = "black" {}
        _Density ("Density", Range(0, 4)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        [HDR] _EmissionColor ("Emission Color", Color) = (4, 2.2, 1, 1)
        [NoScaleOffset] _EmissionGradient ("Emission Gradient (cool left, hot right)", 2D) = "white" {}
        _SoftDistance ("Soft Particle Distance (m)", Float) = 1.5
        _NearFadeStart ("Camera Fade Start (m)", Float) = 1
        _NearFadeEnd ("Camera Fade End (m)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ParticlesSixWay"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SixWayVert
            #pragma fragment SixWayFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Samples the cascades at the pixel even when opaques use screen-space shadows.
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/SixWayInput.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/SixWayLighting.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/HeightFog.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Effects/SixWayPass.hlsl"
            ENDHLSL
        }
    }
}
