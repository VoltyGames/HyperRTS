// A building's flag from a texture array, with a team band at the hoist, waving downwind as cloth (FlagCloth.hlsl).
// FlagSystem sets _FlagState and _FlagColor per entity (DOTS instancing).
Shader "HyperRTS/Flag"
{
    Properties
    {
        _FlagArray ("Flags (layer = side)", 2DArray) = "" {}
        [HideInInspector] _FlagState ("State (layer, hoist, shown, drop)", Vector) = (0, 1, 0, 0)
        [HideInInspector] _FlagColor ("Band Colour", Color) = (0, 0, 0, 0)
        _BandWidth ("Band Width (share of the length)", Range(0, 0.5)) = 0.12
        _Translucency ("Light Through", Range(0, 1)) = 0.35
        _WaveLength ("Wave Length (flag heights)", Float) = 1.1
        _WaveSpeed ("Wave Speed", Float) = 7
        _WaveAmplitude ("Wave Amplitude (flag heights)", Range(0, 0.3)) = 0.09
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "AlwaysRenderMotionVectors" = "true"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #pragma use_dxc
        #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagCloth.hlsl"
        ENDHLSL

        Pass
        {
            Name "Flag"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagForward.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagPasses.hlsl"
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
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Flags/FlagPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // GI bakes per material, so it sees the material's flag and band, not each entity's.
            half3 MetaAlbedo(float2 uv0)
            {
                return FlagAlbedo(uv0);
            }
            ENDHLSL
        }
    }
}
