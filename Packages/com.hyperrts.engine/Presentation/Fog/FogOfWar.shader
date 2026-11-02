// Fog of war over the opaques, rebuilding world position from depth so hills, trees and buildings fog where they
// stand: it copies the camera colour, draining colour from fogged ground and darkening it. Transparents apply
// FogOfWar.hlsl themselves.
Shader "Hidden/HyperRTS/FogOfWar"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FogOfWar"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneDepth.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                // The sky keeps its colour.
                #if UNITY_REVERSED_Z
                    bool sky = depth <= 1e-6;
                #else
                    bool sky = depth >= 1.0 - 1e-6;
                #endif
                if (sky)
                {
                    return color;
                }

                float3 positionWS = ReconstructWorldPosition(uv, depth);
                half3 fogged = ApplyFogOfWar(color.rgb, FogOfWarAt(positionWS));
                if (_HyperRTSSightShapeColor.a > 0.0)
                {
                    fogged = SightShapeOutline(fogged, positionWS);
                }

                return half4(fogged, color.a);
            }
            ENDHLSL
        }
    }
}
