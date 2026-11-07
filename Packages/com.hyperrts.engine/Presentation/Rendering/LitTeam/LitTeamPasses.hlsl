// HyperRTS/LitTeam's URP passes with posed, built and deformed vertices (LitTeamVAT.hlsl). Include after the pass file.
#ifndef HYPERRTS_LIT_TEAM_PASSES_INCLUDED
#define HYPERRTS_LIT_TEAM_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamVAT.hlsl"

// The forward pass reads UV1 as URP's static lightmap UV, which rigid VAT and bone renderers never use; other passes take it apart.
#if defined(UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
    #define DISPLACED_LIT_INPUTS BONE_INPUT BUILD_ORDER_INPUT
    #define DisplaceLit(input, positionOS, normalOS) LIT_TEAM_DEFORM(input.staticLightmapUV, positionOS, normalOS)
#else
    #define DISPLACED_LIT_INPUTS VAT_INDEX_INPUT BONE_INPUT BUILD_ORDER_INPUT
    #define DisplaceLit(input, positionOS, normalOS) LIT_TEAM_DEFORM(vatIndex, positionOS, normalOS)
#endif

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedLitPasses.hlsl"

#endif
