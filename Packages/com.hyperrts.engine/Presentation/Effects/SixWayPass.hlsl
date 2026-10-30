// Vertex and fragment programs of ParticlesSixWay.shader. Needs SixWayInput.hlsl, FlipbookMotion.hlsl,
// SixWayLighting.hlsl and EyeDepth.hlsl first.
#ifndef HYPERRTS_SIX_WAY_PASS_INCLUDED
#define HYPERRTS_SIX_WAY_PASS_INCLUDED

SixWayVaryings SixWayVert(SixWayAttributes input)
{
    SixWayVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.frames = input.frames;
    output.color = input.color;
    output.blendAndEmission = input.blendAndEmission;
    output.tangentWS = normals.tangentWS;
    output.bitangentWS = normals.bitangentWS;
    output.normalWS = normals.normalWS;
    SixWayAmbientColors(output.ambientUp, output.ambientDown, output.ambientSide);
    return output;
}

// Fades the card where it cuts into the ground and as the camera flies into it.
half DepthFade(SixWayVaryings input, float2 screenUV)
{
    float eyeDepth = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());
    half soft = saturate((EyeDepth(SampleSceneDepth(screenUV)) - eyeDepth) / max(_SoftDistance, 0.001));
    half near = saturate((eyeDepth - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 0.001));
    return soft * near;
}

half4 SixWayFrag(SixWayVaryings input) : SV_Target
{
    float2 current = input.frames.xy;
    float2 next = input.frames.zw;
    half frameBlend = input.blendAndEmission.x;
    #if defined(_FLIPBOOK_MOTION)
        FlipbookMotionWarp(TEXTURE2D_ARGS(_MotionMap, sampler_MotionMap), current, next, frameBlend);
    #endif
    half opacity;
    half emissionMap;
    SixWayMaps maps = SampleSixWayMaps(TEXTURE2D_ARGS(_PositiveMap, sampler_PositiveMap),
        TEXTURE2D_ARGS(_NegativeMap, sampler_NegativeMap), current, next, frameBlend, opacity, emissionMap);

    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    half fade = input.color.a * DepthFade(input, screenUV);
    half alpha = saturate(opacity * _Density) * fade;

    // Rows are the particle basis: X right, Y up, Z toward the camera.
    half3x3 basis = half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
    half3 ambient = SixWayAmbient(input.ambientUp, input.ambientDown, input.ambientSide, basis, maps) * _AmbientStrength;
    half3 glow = SixWayGlow(TEXTURE2D_ARGS(_EmissionGradient, sampler_LinearClamp), _EmissionColor.rgb, emissionMap,
        input.blendAndEmission.y) * fade;
    SceneFog fog = SceneFogAt(input.positionWS, input.positionCS);
    return SixWayShade(maps, input.positionWS, screenUV, basis, ambient, input.color.rgb, alpha, glow, fog);
}

#endif
