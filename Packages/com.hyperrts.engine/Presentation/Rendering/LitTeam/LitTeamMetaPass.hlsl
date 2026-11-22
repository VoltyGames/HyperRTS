// Meta pass (the albedo Surface Cache GI bounces): URP Lit's, carrying the object-space position and normal so
// weathering darkens the bounce light as it darkens the surface.
#ifndef HYPERRTS_LIT_TEAM_META_PASS_INCLUDED
#define HYPERRTS_LIT_TEAM_META_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamWeathering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

struct LitTeamMetaAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
};

struct LitTeamMetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv0 : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    float3 normalOS : TEXCOORD2;
#ifdef EDITOR_VISUALIZATION
    float2 vizUV : TEXCOORD3;
    float4 lightCoord : TEXCOORD4;
#endif
};

LitTeamMetaVaryings LitTeamMetaVertex(LitTeamMetaAttributes input)
{
    LitTeamMetaVaryings output = (LitTeamMetaVaryings)0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
    output.uv0 = input.uv0;
    output.positionOS = input.positionOS.xyz;
    output.normalOS = input.normalOS;
#ifdef EDITOR_VISUALIZATION
    UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.vizUV, output.lightCoord);
#endif
    return output;
}

half4 LitTeamMetaFragment(LitTeamMetaVaryings input) : SV_Target
{
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(TRANSFORM_TEX(input.uv0, _BaseMap), surfaceData);
#if defined(_WEATHERING)
    ApplyWeathering(input.positionOS, normalize(input.normalOS), TeamMask(input.uv0, input.positionOS.y), surfaceData);
#endif

    BRDFData brdfData = InitializeBRDFData(surfaceData, IsSpecularSetup(), UseAlphaPremultiply());
    UnityMetaInput metaInput = (UnityMetaInput)0;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.Emission = surfaceData.emission;
#ifdef EDITOR_VISUALIZATION
    metaInput.VizUV = input.vizUV;
    metaInput.LightCoord = input.lightCoord;
#endif
    return UnityMetaFragment(metaInput);
}

#endif
