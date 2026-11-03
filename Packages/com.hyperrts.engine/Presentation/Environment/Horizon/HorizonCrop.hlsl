// HyperRTS/HorizonCrop's surface: the box's vertex stage and the canopy tile and wall it shows, faded to the crop's
// mean far off. The shader declares the textures and its UnityPerMaterial buffer before including this.
#ifndef HYPERRTS_HORIZON_CROP_INCLUDED
#define HYPERRTS_HORIZON_CROP_INCLUDED

#define CROP_MEAN_MIP 16.0

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 canopy : TEXCOORD0; // HorizonRaisedBox: x wall tiles along, y share up the wall, z 1 on walls
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float4 canopy : TEXCOORD2;
};

struct CropSurface
{
    half3 albedo;
    half3 normalWS;
    half coverage;   // 1 where a plant is hit, 0 on bare soil
    half leafHeight; // the tile's height share, which the shade follows
    half sky;        // the baked occlusion from the neighbours, on the ambient light alone
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.canopy = input.canopy;
    return output;
}

bool OnWall(Varyings input)
{
    return input.canopy.z > 0.5;
}

// The point in the horizon grid, x along the rows, as HorizonFields.ToGrid turns it.
float2 CropGrid(float2 positionXZ)
{
    float2 turn = _GridTurn.xy;
    return float2(positionXZ.x * turn.x + positionXZ.y * turn.y, -positionXZ.x * turn.y + positionXZ.y * turn.x);
}

// Share of the way to the crop's mean, from how much ground the pixel spans.
half CropFar(float3 positionWS)
{
    float2 footprint = fwidth(positionWS.xz);
    return (half)saturate(max(footprint.x, footprint.y) / _MeanFootprint);
}

// The wall's texel, cut out at its ragged top until it is too far to cut cleanly.
half4 WallTexel(Varyings input, half far)
{
    float2 uv = float2(input.canopy.x, saturate(input.canopy.y));
    half4 texel = SAMPLE_TEXTURE2D(_WallMap, sampler_TrilinearRepeatU_ClampV, uv);
    clip(texel.a - _Cutoff * (1.0 - far));
    return texel;
}

// The tile's x runs across the rows, the grid's y; its y along them, the grid's x.
CropSurface TopSurface(Varyings input)
{
    float2 grid = CropGrid(input.positionWS.xz);
    float2 uv = float2(grid.y * _CanopyTile.x, grid.x * _CanopyTile.y);
    half4 colour = SAMPLE_TEXTURE2D(_CanopyMap, sampler_TrilinearRepeat, uv);
    half4 mask = SAMPLE_TEXTURE2D(_CanopyMask, sampler_TrilinearRepeat, uv);
    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_CanopyNormal, sampler_TrilinearRepeat, uv));
    half3 up = normalize(input.normalWS);
    half3 along = half3(_GridTurn.x, 0.0, _GridTurn.y);
    half3 across = half3(-_GridTurn.y, 0.0, _GridTurn.x);

    CropSurface surface;
    surface.albedo = colour.rgb * _BaseColor.rgb;
    surface.coverage = mask.g;
    surface.leafHeight = colour.a;
    surface.sky = lerp(1.0, mask.r, _BakedOcclusion);
    half3 mapped = normalize(across * normalTS.x + along * normalTS.y + up * normalTS.z);
    surface.normalWS = normalize(lerp(mapped, up, 1.0 - surface.coverage));
    return surface;
}

CropSurface WallSurface(Varyings input, half far)
{
    half4 texel = WallTexel(input, far);
    float2 uv = float2(input.canopy.x, saturate(input.canopy.y));
    half3 outward = normalize(input.normalWS);
    half3 along = half3(-outward.z, 0.0, outward.x);
    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_WallNormal, sampler_TrilinearRepeatU_ClampV, uv));

    CropSurface surface;
    surface.albedo = texel.rgb * _BaseColor.rgb;
    surface.normalWS = normalize(along * normalTS.x + half3(0.0, 1.0, 0.0) * normalTS.y + outward * normalTS.z);
    surface.coverage = 1.0;
    surface.leafHeight = (half)saturate(input.canopy.y);
    surface.sky = 1.0;
    return surface;
}

// Far off, the crop's mean colour on the drawn surface's own normal.
CropSurface CropSurfaceAt(Varyings input)
{
    half far = CropFar(input.positionWS);
    CropSurface surface;
    UNITY_BRANCH
    if (OnWall(input))
    {
        surface = WallSurface(input, far);
    }
    else
    {
        surface = TopSurface(input);
    }

    half3 mean = SAMPLE_TEXTURE2D_LOD(_CanopyMap, sampler_TrilinearRepeat, float2(0.5, 0.5), CROP_MEAN_MIP).rgb;
    surface.albedo = lerp(surface.albedo, mean * _BaseColor.rgb, far);
    surface.normalWS = normalize(lerp(surface.normalWS, normalize(input.normalWS), far));
    return surface;
}

#endif
