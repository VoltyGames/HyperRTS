// HyperRTS/LitTeam detail maps: a tiling grey albedo and a normal projected on the mesh in object space from three
// sides, so grain has the same density on every island of an atlased UV set. They fade out with camera distance.
// Forward pass only; the depth-normals buffer keeps the base normal, which is all SSAO needs.
#ifndef HYPERRTS_LIT_TEAM_DETAIL_INCLUDED
#define HYPERRTS_LIT_TEAM_DETAIL_INCLUDED

TEXTURE2D(_DetailAlbedo);   SAMPLER(sampler_DetailAlbedo);
TEXTURE2D(_DetailNormal);   SAMPLER(sampler_DetailNormal);

// Gradients are passed in so the samples can sit behind the fade branch.
half3 SampleDetailAlbedo(float3 p, float3 dx, float3 dy, float3 blend)
{
    half3 x = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedo, sampler_DetailAlbedo, p.zy, dx.zy, dy.zy).rgb;
    half3 y = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedo, sampler_DetailAlbedo, p.xz, dx.xz, dy.xz).rgb;
    half3 z = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedo, sampler_DetailAlbedo, p.xy, dx.xy, dy.xy).rgb;
    return x * blend.x + y * blend.y + z * blend.z;
}

// The normal's slope in object space: each side's xy tilts the two axes lying in its plane.
float3 SampleDetailSlope(float3 p, float3 dx, float3 dy, float3 blend)
{
    float2 x = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_DetailNormal, sampler_DetailNormal, p.zy, dx.zy, dy.zy)).xy;
    float2 y = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_DetailNormal, sampler_DetailNormal, p.xz, dx.xz, dy.xz)).xy;
    float2 z = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_DetailNormal, sampler_DetailNormal, p.xy, dx.xy, dy.xy)).xy;
    return float3(0.0, x.y, x.x) * blend.x + float3(y.x, 0.0, y.y) * blend.y + float3(z.x, z.y, 0.0) * blend.z;
}

// tangentWS is only read with _NORMALMAP, which gives the vertex its tangent.
void ApplyDetail(float3 positionWS, float3 normalWS, float4 tangentWS, inout SurfaceData surface)
{
    float3 p = TransformWorldToObject(positionWS) * _DetailTiling;
    float3 dx = ddx(p);
    float3 dy = ddy(p);
    float fade = 1.0 - smoothstep(_DetailFadeStart, _DetailFadeEnd, distance(positionWS, _WorldSpaceCameraPos));
    if (fade <= 0.0)
    {
        return;
    }

    float3 sides = abs(TransformWorldToObjectNormal(normalWS));
    float3 blend = sides * sides;
    blend *= blend;
    blend /= blend.x + blend.y + blend.z;

    // Overlay around grey: 0.5 as authored leaves the albedo as it is. The map imports as sRGB colour, so undo the decode.
    half3 grain = LinearToSRGB(SampleDetailAlbedo(p, dx, dy, blend));
    surface.albedo *= lerp(half3(1.0, 1.0, 1.0), grain * 2.0, _DetailAlbedoStrength * fade);

#if defined(_NORMALMAP)
    float3 slopeWS = mul((float3x3)GetObjectToWorldMatrix(), SampleDetailSlope(p, dx, dy, blend));
    float3 tangent = tangentWS.xyz;
    float3 bitangent = tangentWS.w * cross(normalWS, tangent);
    float2 tilt = float2(dot(slopeWS, tangent), dot(slopeWS, bitangent)) * (_DetailNormalStrength * fade);
    surface.normalTS = normalize(float3(surface.normalTS.xy + tilt, surface.normalTS.z));
#endif
}

#endif
