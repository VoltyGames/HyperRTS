// Vertex and fragment programs of ParticlesSixWay.shader. Needs SixWayInput.hlsl, SixWayLighting.hlsl and
// HeightFog.hlsl first.
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

SixWayMaps SampleSixWayMaps(float4 frames, half frameBlend, out half opacity, out half emission)
{
    half4 positive = lerp(SAMPLE_TEXTURE2D(_PositiveMap, sampler_PositiveMap, frames.xy),
        SAMPLE_TEXTURE2D(_PositiveMap, sampler_PositiveMap, frames.zw), frameBlend);
    half4 negative = lerp(SAMPLE_TEXTURE2D(_NegativeMap, sampler_NegativeMap, frames.xy),
        SAMPLE_TEXTURE2D(_NegativeMap, sampler_NegativeMap, frames.zw), frameBlend);
    SixWayMaps maps;
    maps.positive = positive.rgb;
    maps.negative = negative.rgb;
    opacity = positive.a;
    emission = negative.a;
    return maps;
}

// Eye-space depth of the opaque scene behind the pixel, for perspective and orthographic cameras.
float SceneEyeDepth(float2 screenUV)
{
    float rawDepth = SampleSceneDepth(screenUV);
    return unity_OrthoParams.w == 0 ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
}

// Fades the card where it cuts into the ground and as the camera flies into it.
half DepthFade(SixWayVaryings input, float2 screenUV)
{
    float eyeDepth = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());
    half soft = saturate((SceneEyeDepth(screenUV) - eyeDepth) / max(_SoftDistance, 0.001));
    half near = saturate((eyeDepth - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 0.001));
    return soft * near;
}

// Hot cores ramp toward the gradient's right end (white-yellow), cooling edges toward its left (red).
half3 SixWayEmission(half emissionMap, half emissionScale)
{
    half emission = saturate(emissionMap * emissionScale);
    half3 ramp = SAMPLE_TEXTURE2D_LOD(_EmissionGradient, sampler_LinearClamp, float2(emission, 0.5), 0).rgb;
    return _EmissionColor.rgb * ramp * emission;
}

// Premultiplied alpha: the lit smoke is fogged and scaled by its opacity, the emission adds on top.
half4 SixWayFrag(SixWayVaryings input) : SV_Target
{
    half opacity;
    half emissionMap;
    SixWayMaps maps = SampleSixWayMaps(input.frames, input.blendAndEmission.x, opacity, emissionMap);

    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    half fade = input.color.a * DepthFade(input, screenUV);
    half alpha = saturate(opacity * _Density) * fade;

    half3x3 basis = SixWayBasis(input);
    half3 light = SixWayMainLight(input.positionWS, basis, maps)
        + SixWayAdditionalLights(input.positionWS, screenUV, basis, maps)
        + SixWayAmbient(input, basis, maps);

    half4 fog = HeightFogAt(input.positionWS);
    half3 smoke = ApplyHeightFog(input.color.rgb * light, fog) * alpha;
    half3 glow = SixWayEmission(emissionMap, input.blendAndEmission.y) * (fade * (1.0h - fog.a));
    return half4(smoke + glow, alpha);
}

#endif
