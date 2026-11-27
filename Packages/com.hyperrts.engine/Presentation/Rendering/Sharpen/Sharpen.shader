// AMD's contrast-adaptive sharpening on the finished frame (SharpenPass): each pixel pushed away from its four
// neighbours by an amount that shrinks where the neighbourhood is already contrasty, so edges firm up without halos.
Shader "Hidden/HyperRTS/Sharpen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Sharpen"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half _SharpenStrength;

            half3 Neighbour(int2 pixel, int2 offset)
            {
                int2 size = int2(_BlitTexture_TexelSize.zw) - 1;
                return LOAD_TEXTURE2D_X(_BlitTexture, clamp(pixel + offset, int2(0, 0), size)).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);
                half4 centre = LOAD_TEXTURE2D_X(_BlitTexture, pixel);
                half3 up = Neighbour(pixel, int2(0, -1));
                half3 left = Neighbour(pixel, int2(-1, 0));
                half3 right = Neighbour(pixel, int2(1, 0));
                half3 down = Neighbour(pixel, int2(0, 1));

                // Colours past 1 (HDR output) would read as no headroom, so the amount works on clamped copies.
                half3 low = saturate(min(centre.rgb, min(min(up, left), min(right, down))));
                half3 high = saturate(max(centre.rgb, max(max(up, left), max(right, down))));
                half3 amount = sqrt(saturate(min(low, 1.0h - high) / max(high, 1e-4h)));
                // Full strength is CAS at its sharpest (a fifth of each neighbour); 0 leaves the frame as it is.
                half3 weight = -amount * 0.2h * _SharpenStrength;
                half3 sharpened = (centre.rgb + weight * (up + left + right + down)) / (1.0h + 4.0h * weight);
                return half4(max(sharpened, 0.0h), centre.a);
            }
            ENDHLSL
        }
    }
}
