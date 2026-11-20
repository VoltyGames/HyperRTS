// Meta pass (the pass that bakes albedo for Surface Cache GI) for hand-written shaders. The including pass defines
// half3 MetaAlbedo(float2 uv0); it draws over a unit quad in UV0 space, so no wind or world-space terms. A cutout
// shader also defines META_ALPHA_CLIP and half MetaAlpha(float2 uv0), with _Cutoff declared first, so its holes
// stay out.
#ifndef HYPERRTS_META_PASS_INCLUDED
#define HYPERRTS_META_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

half3 MetaAlbedo(float2 uv0);
#if defined(META_ALPHA_CLIP)
half MetaAlpha(float2 uv0);
#endif

struct MetaAttributes
{
    float4 positionOS : POSITION;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
};

struct MetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

MetaVaryings MetaPassVertex(MetaAttributes input)
{
    MetaVaryings output;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
    output.uv = input.uv0;
    return output;
}

half4 MetaPassFragment(MetaVaryings input) : SV_Target
{
#if defined(META_ALPHA_CLIP)
    clip(MetaAlpha(input.uv) - _Cutoff);
#endif
    UnityMetaInput meta = (UnityMetaInput)0;
    meta.Albedo = MetaAlbedo(input.uv);
    return UnityMetaFragment(meta);
}

#endif
