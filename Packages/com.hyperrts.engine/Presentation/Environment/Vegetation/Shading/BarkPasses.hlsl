// HyperRTS/Bark's URP Lit passes with windblown vertices. Include BarkInput.hlsl and URP's pass file first.
#ifndef HYPERRTS_BARK_PASSES_INCLUDED
#define HYPERRTS_BARK_PASSES_INCLUDED

#if defined(UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
    // The forward pass already reads TEXCOORD2, as a dynamic lightmap UV bark never has.
    #define DISPLACED_LIT_INPUTS , float2 uv3 : TEXCOORD3, float2 uv4 : TEXCOORD4, half4 color : COLOR
    #define DisplaceLit(input, positionOS, normalOS) \
        positionOS = BarkWindPosition(positionOS, input.dynamicLightmapUV, uv3, uv4, color, WindThisFrame());
#else
    #define DISPLACED_LIT_INPUTS , float2 uv2 : TEXCOORD2, float2 uv3 : TEXCOORD3, float2 uv4 : TEXCOORD4, half4 color : COLOR
    #define DisplaceLit(input, positionOS, normalOS) \
        positionOS = BarkWindPosition(positionOS, uv2, uv3, uv4, color, WindThisFrame());
#endif

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedLitPasses.hlsl"

#endif
