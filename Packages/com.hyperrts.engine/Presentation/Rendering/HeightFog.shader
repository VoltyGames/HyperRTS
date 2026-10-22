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
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneDepth.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/HeightFog.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                half4 fog = HeightFogAt(ReconstructWorldPosition(uv, SampleSceneDepth(uv)));
                return half4(fog.rgb * fog.a, fog.a);
            }
            ENDHLSL
        }
    }
}
