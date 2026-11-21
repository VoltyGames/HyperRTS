// Team outlines (TeamOutlinePass). The mask holds the owner's colour at alpha 1 where a unit shows, 0.75 where a
// building shows and 0.5 where a unit is hidden behind something; the outline paints the nearest owner's colour on
// empty pixels within the width of a shown one, so no line is drawn across a hidden part.
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

            // Width in screen pixels, its whole-pixel search reach, opacity, widest building gap bridged in pixels.
            float4 _TeamOutline;
            // 1 / the screen's width and height.
            float4 _TeamOutlinePixel;

            static const float UnitAlpha = 0.875;
            static const float ShownAlpha = 0.625;
            static const float CoveredAlpha = 0.25;

            half4 Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            }

            bool SameBuilding(half4 mask, half3 color)
            {
                bool building = mask.a > ShownAlpha && mask.a <= UnitAlpha;
                return building && all(abs(mask.rgb - color) < 0.02);
            }

            // Whether a building of this colour lies within the gap along the direction.
            bool BuildingAlong(float2 uv, float2 step, half3 color)
            {
                int gap = (int)_TeamOutline.w;
                [loop]
                for (int i = 1; i <= gap; i++)
                {
                    if (SameBuilding(Mask(uv + step * i), color))
                    {
                        return true;
                    }
                }

                return false;
            }

            // An empty pixel with the same building on both sides along some direction lies in a gap of it (a
            // lattice, fence, rail or a sliver at a depth step), which the building's one outline bridges.
            bool InBuildingGap(float2 uv, half3 color)
            {
                float2 pixel = _TeamOutlinePixel.xy;
                float2 steps[4] = { float2(pixel.x, 0), float2(0, pixel.y), pixel, float2(pixel.x, -pixel.y) };
                [unroll]
                for (int d = 0; d < 4; d++)
                {
                    if (BuildingAlong(uv, steps[d], color) && BuildingAlong(uv, -steps[d], color))
                    {
                        return true;
                    }
                }

                return false;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                if (Mask(uv).a > CoveredAlpha)
                {
                    discard;
                }

                float width = _TeamOutline.x;
                int reach = (int)_TeamOutline.y;
                float nearest = width + 1.0;
                half4 found = 0;
                [loop]
                for (int y = -reach; y <= reach; y++)
                {
                    [loop]
                    for (int x = -reach; x <= reach; x++)
                    {
                        float distance = length(float2(x, y));
                        half4 mask = Mask(uv + float2(x, y) * _TeamOutlinePixel.xy);
                        if (mask.a > ShownAlpha && distance < nearest)
                        {
                            nearest = distance;
                            found = mask;
                        }
                    }
                }

                // Half a pixel of fade at the outer edge keeps the line smooth.
                half alpha = _TeamOutline.z * saturate(width + 0.5 - nearest);
                if (alpha <= 0)
                {
                    discard;
                }

                if (found.a <= UnitAlpha && InBuildingGap(uv, found.rgb))
                {
                    discard;
                }

                return half4(found.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
