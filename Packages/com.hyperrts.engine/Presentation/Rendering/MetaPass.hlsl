// Meta pass for hand-written shaders without URP Lit's inputs. The including pass defines half3 MetaAlbedo(float2 uv0).
// Surface Cache GI draws it over a unit quad to bake the material's albedo in UV0 space, so no wind or world-space terms.
#ifndef HYPERRTS_META_PASS_INCLUDED
#define HYPERRTS_META_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

half3 MetaAlbedo(float2 uv0);

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
    UnityMetaInput meta = (UnityMetaInput)0;
    meta.Albedo = MetaAlbedo(input.uv);
    return UnityMetaFragment(meta);
}

#endif
