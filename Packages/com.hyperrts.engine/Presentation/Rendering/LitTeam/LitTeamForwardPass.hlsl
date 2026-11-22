// Forward pass: URP's LitForwardPass with a fragment that tints the albedo in the owner's colour before lighting.
#ifndef HYPERRTS_LIT_TEAM_FORWARD_PASS_INCLUDED
#define HYPERRTS_LIT_TEAM_FORWARD_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamGrime.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamDetail.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamWeathering.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInterior.hlsl"
#if defined(_INTERIOR)
    #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR 1
#endif
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

void LitTeamPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ClipReveal(input.positionCS);

    // The mask follows the model's own UVs, not the base map's tiling.
    float2 uv0 = (input.uv - _BaseMap_ST.zw) / _BaseMap_ST.xy;

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
#if defined(_DETAIL)
    #if defined(_NORMALMAP)
    float4 tangentWS = input.tangentWS;
    #else
    float4 tangentWS = float4(1.0, 0.0, 0.0, 1.0);
    #endif
    ApplyDetail(input.positionWS, input.normalWS.xyz, tangentWS, surfaceData);
#endif
#if defined(_INTERIOR)
    float3 bitangentWS = input.tangentWS.w * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 viewDirTS = float3(dot(viewWS, input.tangentWS.xyz), dot(viewWS, bitangentWS), dot(viewWS, input.normalWS.xyz));
    surfaceData.emission = InteriorEmission(input.uv, viewDirTS);
#endif
    float3 positionOS = TransformWorldToObject(input.positionWS);
    half teamMask = TeamMask(uv0, positionOS.y);
    surfaceData.albedo = ApplyTeamColor(surfaceData.albedo, teamMask);
#if defined(_WEATHERING)
    float3 normalOS = normalize(TransformWorldToObjectNormal(input.normalWS.xyz));
    ApplyWeathering(positionOS, normalOS, teamMask, surfaceData);
#endif
    ApplyGrime(input.positionWS, surfaceData);
    ApplyRain(input.normalWS.xyz, surfaceData);

    LODFadeCrossFade(input.positionCS);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    if (!IsSurfaceTypeTransparent())
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData, IsSpecularSetup());
#endif

    InitializeBakedGIData(input, inputData);

    bool transparent = IsSurfaceTypeTransparent();
    bool clearCoat = UseClearCoat() || UseClearCoatMap();
    URP_LIGHT_ACCUM4 color = UniversalFragmentPBR(inputData, surfaceData,
        IsSpecularSetup(),
        UseSpecularHighlights(),
        UseAlphaPremultiply(),
        clearCoat,
        ReceiveShadows(),
        transparent,
        UseEnvironmentReflections());
    color.rgb = ClampExposed(inputData.preExposureMultiplier * BlendDistanceFog(color.rgb, input.positionCS));
#if defined(_TRANSPARENT_RECEIVE_FOG)
    // Glass always blends premultiplied, so the blend mode argument is a constant.
    color.rgb = MixVolumetricFog(color.rgb, color.a, 1.0, true, input.positionCS);
#endif
    color.a = OutputAlpha(color.a, transparent);

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
