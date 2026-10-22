// HyperRTS/LitTeam forward pass: URP's LitForwardPass (vertex, input data, GI) with a fragment that mirrors
// LitPassFragment and tints the albedo before decals and lighting.
#ifndef HYPERRTS_LIT_TEAM_FORWARD_PASS_INCLUDED
#define HYPERRTS_LIT_TEAM_FORWARD_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"
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

    // The mask follows the model's own UVs, not the base map's tiling.
    float2 uv0 = (input.uv - _BaseMap_ST.zw) / _BaseMap_ST.xy;

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    surfaceData.albedo = ApplyTeamColor(surfaceData.albedo, uv0, input.positionWS);

    LODFadeCrossFade(input.positionCS);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    if (!IsSurfaceTypeTransparent())
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData, IsSpecularSetup());
#endif

    InitializeBakedGIData(input, inputData);

    URP_LIGHT_ACCUM4 color = UniversalFragmentPBR(inputData, surfaceData,
        IsSpecularSetup(), UseSpecularHighlights(), UseAlphaPremultiply(),
        UseClearCoat() || UseClearCoatMap(), ReceiveShadows(), IsSurfaceTypeTransparent(), UseEnvironmentReflections(), input.normalWS.xyz);
    color.rgb = ClampExposed(inputData.preExposureMultiplier * BlendDistanceFog(color.rgb, input.positionCS));
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
