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

// The fog fade's dither in the prepasses too, so SSAO and contact shadows thin out with the unit. Shadows stay whole.
#if defined(UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED)
half LitTeamDepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipReveal(input.positionCS);
    return DepthOnlyFragment(input);
}
#elif defined(UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED)
void LitTeamDepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipReveal(input.positionCS);
    DepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#elif defined(UNIVERSAL_OBJECT_MOTION_VECTORS_INCLUDED)
// URP's vertex takes last frame's position from the posed mesh, which reports the object's motion but not its
// animation's: pose the vertex at last frame's clip time too.
Varyings LitTeamMotionVertex(Attributes input DISPLACED_LIT_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 last = input.position.xyz;
    float3 normalOS = float3(0.0, 1.0, 0.0);
    LIT_TEAM_DEFORM_AT(vatIndex, last, normalOS, _LastClipTime)
    LIT_TEAM_DEFORM(vatIndex, input.position.xyz, normalOS)
    Varyings output = vert(input);
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, float4(last, 1.0)));
    return output;
}

float4 LitTeamMotionFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipReveal(input.positionCS);
    return frag(input);
}
#endif

#endif
