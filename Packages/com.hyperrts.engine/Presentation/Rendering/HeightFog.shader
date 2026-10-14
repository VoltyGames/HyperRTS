// Fullscreen height fog over the opaques and sky, blended onto the camera colour (HeightFogPass).
Shader "Hidden/HyperRTS/HeightFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "HeightFog"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/HeightFog.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                half4 fog = HeightFogAt(ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP));
                return half4(fog.rgb * fog.a, fog.a);
            }
            ENDHLSL
        }
    }
}
