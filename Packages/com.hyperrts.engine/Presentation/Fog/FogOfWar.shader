// Fog of war over the opaques, rebuilding world position from depth so hills, trees and buildings fog
// where they stand. Transparents apply FogOfWar.hlsl themselves.
Shader "Hidden/HyperRTS/FogOfWar"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FogOfWar"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    clip(depth - 1e-6);
                #else
                    clip(1.0 - 1e-6 - depth);
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                return FogOfWarAt(ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP));
            }
            ENDHLSL
        }
    }
}
