// HyperRTS/Flag's forward pass: lit cloth that lets light through from behind.
#ifndef HYPERRTS_FLAG_FORWARD_INCLUDED
#define HYPERRTS_FLAG_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    Cloth cloth = FlagCloth(input.positionOS.xyz, input.uv, _TimeParameters.x);
    output.positionWS = cloth.positionWS;
    output.positionCS = TransformWorldToHClip(cloth.positionWS);
    output.uv = input.uv;
    output.normalWS = cloth.normalWS;
    return output;
}

// The lit side, plus light through the cloth on the far side.
half FlagFacing(half3 normal, half3 toLight)
{
    half facing = dot(normal, toLight);
    return saturate(facing) + saturate(-facing) * _Translucency;
}

// Unshadowed point and spot lights, so fires and explosion flashes light the flags round them.
half3 FlagAdditionalLights(Varyings input, half3 normal)
{
    half3 lighting = half3(0.0, 0.0, 0.0);
    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData = LightLoopInput(input.positionWS, GetNormalizedScreenSpaceUV(input.positionCS));
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, input.positionWS);
            lighting += light.color * (light.distanceAttenuation * FlagFacing(normal, light.direction));
        LIGHT_LOOP_END
    #endif
    return lighting;
}

half3 FlagAmbient(Varyings input, half3 normal)
{
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 ambient = SurfaceCacheAmbient(input.positionWS, normal, view, input.positionCS.xy);

    #if defined(_SCREEN_SPACE_OCCLUSION)
        float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
        ambient *= GetScreenSpaceAmbientOcclusion(screenUV).indirectAmbientOcclusion;
    #endif
    return ambient;
}

half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half3 albedo = FlagAlbedo(input.uv);
    half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light sun = GetMainLight(shadowCoord, input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
    half3 lit = sun.color * (sun.shadowAttenuation * FlagFacing(normal, sun.direction));
    lit += FlagAdditionalLights(input, normal);
    return half4(albedo * (lit + FlagAmbient(input, normal)), 1.0);
}

#endif
