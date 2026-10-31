// Base colour and normal map accessors that URP's Lit pass files call by name, shared by HyperRTS/LitTeam and
// HyperRTS/Bark. Include it after the shader's UnityPerMaterial CBUFFER, which declares _BaseMap_ST,
// _BaseMap_TexelSize, _BaseColor, _Cutoff and _BumpScale (and any DOTS-instanced macros for them).
#ifndef HYPERRTS_SURFACE_MAPS_INCLUDED
#define HYPERRTS_SURFACE_MAPS_INCLUDED

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_BaseMap);

half4 GetBaseColor()            { return _BaseColor; }
half GetCutoff()                { return _Cutoff; }
half GetBumpScale()             { return _BumpScale; }
half4 SampleBaseMap(float2 uv)  { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv); }
half4 SampleBumpMap(float2 uv)  { return SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv); }

// No detail map: LitDepthNormalsPass's calls behind UseDetailMap() compile away, so these need no bodies.
half4 SampleDetailMask(float2 uv);
float4 GetDetailAlbedoMapST();

#endif
