// Silhouettes of units hidden behind other things, drawn by two URP Render Objects features: the mask pass marks
// visible pixels in the stencil, then the silhouette pass paints the team colour where the unit is hidden. The team
// outline passes draw the owner's colour into TeamOutlineFeature's mask.
#ifndef HYPERRTS_LIT_TEAM_SILHOUETTE_PASS_INCLUDED
#define HYPERRTS_LIT_TEAM_SILHOUETTE_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamDeform.hlsl"

#define SILHOUETTE_FILL 0.12
#define SILHOUETTE_RIM_POWER 2.0
// Metres a part may sit behind what hides it and still count as seen, so feet and tracks touching the ground, or a
// sole a little under it, draw no silhouette.
#define SILHOUETTE_SLACK 0.3

// 1 while footage hides the silhouettes (UnitSilhouettes.Hide).
float _HyperRTSHideSilhouettes;

struct SilhouetteAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SilhouetteVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Same transform as the forward pass so the mask's ZTest Equal matches its depth exactly; the silhouette and building
// outline passes add SILHOUETTE_SLACK_ON.
SilhouetteVaryings SilhouetteVertex(SilhouetteAttributes input VAT_INDEX_INPUT BONE_INPUT BUILD_ORDER_INPUT)
{
    SilhouetteVaryings output = (SilhouetteVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    LIT_TEAM_DEFORM(vatIndex, input.positionOS.xyz, input.normalOS)

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
#ifdef SILHOUETTE_SLACK_ON
    // Drawn nearer the camera, so only parts hidden by more than the slack pass the silhouette's ZTest Greater, and
    // parts hidden by less pass the building outline's ZTest LEqual.
    output.positionCS = TransformWorldToHClip(vertexInput.positionWS + GetWorldSpaceNormalizeViewDir(vertexInput.positionWS) * SILHOUETTE_SLACK);
#endif
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

// The pixels the forward pass draws: cutout holes and the fog fade's dither stay out of both passes.
void ClipSilhouette(SilhouetteVaryings input)
{
    AlphaDiscard(SampleBaseMap(input.uv).a * GetBaseColor().a, GetCutoff());
    ClipReveal(input.positionCS);
}

half4 UnitMaskFragment(SilhouetteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ClipSilhouette(input);
    return 0;
}

// Flat team-colour fill that brightens to a full rim at grazing angles; units with no owner draw none.
half4 SilhouetteFragment(SilhouetteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ClipSilhouette(input);

    clip(_TeamColor.a - 0.001);
    clip(0.5 - _HyperRTSHideSilhouettes);
    half3 normalWS = normalize(input.normalWS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half rim = pow(1.0 - saturate(abs(dot(normalWS, viewDirWS))), SILHOUETTE_RIM_POWER);
    return half4(_TeamColor.rgb, lerp(SILHOUETTE_FILL, 1.0, rim));
}

// The owner's colour, at alpha 1 for a unit and 0.75 for a building, whose outline bridges its own gaps; neutral
// entities draw none.
half4 TeamOutlineFragment(SilhouetteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ClipSilhouette(input);

    clip(_TeamColor.a - 0.001);
#ifdef BUILDING_OUTLINE_ON
    return half4(_TeamColor.rgb, 0.75);
#else
    return half4(_TeamColor.rgb, 1.0);
#endif
}

#endif
