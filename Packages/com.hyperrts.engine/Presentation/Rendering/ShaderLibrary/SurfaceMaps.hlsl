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

// No detail map: LitDepthNormalsPass only calls these behind UseDetailMap(), but DXC wants bodies before it drops them.
half4 SampleDetailMask(float2 uv)      { return 0; }
half4 SampleDetailAlbedoMap(float2 uv) { return 0; }
half4 SampleDetailNormalMap(float2 uv) { return half4(0.5, 0.5, 1, 1); }
float4 GetDetailAlbedoMapST()          { return float4(1, 1, 0, 0); }
half GetDetailAlbedoMapScale()         { return 0; }
half GetDetailNormalMapScale()         { return 0; }

#endif
