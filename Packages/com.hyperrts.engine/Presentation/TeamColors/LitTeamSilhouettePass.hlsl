// HyperRTS/LitTeam unit silhouette passes, drawn by UnitSilhouetteFeature after the opaques: the mask pass marks the
// unit's visible pixels in the stencil, then the silhouette pass paints the team colour where the unit is hidden.
#ifndef HYPERRTS_LIT_TEAM_SILHOUETTE_PASS_INCLUDED
#define HYPERRTS_LIT_TEAM_SILHOUETTE_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamVAT.hlsl"

#define SILHOUETTE_FILL 0.12
#define SILHOUETTE_RIM_POWER 2.0

struct SilhouetteAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SilhouetteVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Same transform as the forward pass so the mask's ZTest Equal matches its depth exactly.
SilhouetteVaryings SilhouetteVertex(SilhouetteAttributes input VAT_INDEX_INPUT BUILD_ORDER_INPUT)
{
    SilhouetteVaryings output = (SilhouetteVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
#ifdef _VAT
    ApplyVAT(vatIndex, input.positionOS.xyz, input.normalOS);
#endif
#ifdef _BUILD
    ApplyBuild(buildOrder, input.positionOS.xyz, input.normalOS);
#endif
    input.positionOS.xyz = SinkObjectIntoCrater(input.positionOS.xyz);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 UnitMaskFragment(SilhouetteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}

// Flat team-colour fill that brightens to a full rim at grazing angles; units with no owner draw none.
half4 SilhouetteFragment(SilhouetteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    clip(_TeamColor.a - 0.001);
    half3 normalWS = normalize(input.normalWS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half rim = pow(1.0 - saturate(abs(dot(normalWS, viewDirWS))), SILHOUETTE_RIM_POWER);
    return half4(_TeamColor.rgb, lerp(SILHOUETTE_FILL, 1.0, rim));
}

#endif
