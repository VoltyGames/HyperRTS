// Team outlines (TeamOutlinePass). The mask holds the owner's colour at alpha 1 where a unit or building shows, and
// alpha 0.5 where a unit is hidden behind something; the outline paints the nearest owner's colour on empty pixels
// within the width of a shown one, so no line is drawn across a hidden part.
Shader "Hidden/HyperRTS/TeamOutline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // Marks the silhouette pixels (stencil bit 32) in the mask.
        Pass
        {
            Name "Hidden"
            ZTest Always
            ZWrite Off
            Cull Off
            Stencil { Ref 32 ReadMask 32 Comp Equal }

            HLSLPROGRAM
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0.5);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Width in screen pixels, its whole-pixel search reach, opacity, unused.
            float4 _TeamOutline;
            // 1 / the screen's width and height.
            float4 _TeamOutlinePixel;

            half4 Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                if (Mask(uv).a > 0.25)
                {
                    discard;
                }

                float width = _TeamOutline.x;
                int reach = (int)_TeamOutline.y;
                float nearest = width + 1.0;
                half3 color = 0;
                [loop]
                for (int y = -reach; y <= reach; y++)
                {
                    [loop]
                    for (int x = -reach; x <= reach; x++)
                    {
                        float distance = length(float2(x, y));
                        half4 mask = Mask(uv + float2(x, y) * _TeamOutlinePixel.xy);
                        if (mask.a > 0.75 && distance < nearest)
                        {
                            nearest = distance;
                            color = mask.rgb;
                        }
                    }
                }

                // Half a pixel of fade at the outer edge keeps the line smooth.
                half alpha = _TeamOutline.z * saturate(width + 0.5 - nearest);
                if (alpha <= 0)
                {
                    discard;
                }

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
