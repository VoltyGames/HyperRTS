// Only CraterTerrain.cs draws with this, into a region of the terrain heightmap; a Resources shader, so systems load it.
Shader "Hidden/HyperRTS/Crater Height"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CraterHeight"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex FullScreenVertex
            #pragma fragment CraterHeightFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/CraterSink.hlsl"

            Texture2D<float> _OriginalHeights;
            float4 _HeightRegion;  // first heightmap texel of the region in x and z
            float4 _TerrainTexels; // terrain origin x and z, metres per heightmap texel in x and z
            float _DepthScale;     // heightmap units per metre

            float4 FullScreenVertex(uint vertexID : SV_VertexID) : SV_POSITION
            {
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                return float4(uv * 2.0 - 1.0, 0.5, 1.0);
            }

            // Pixel rows and heightmap rows both count from memory row 0, on every graphics API.
            float CraterHeightFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                int2 texel = (int2)_HeightRegion.xy + (int2)positionCS.xy;
                float2 positionXZ = _TerrainTexels.xy + texel * _TerrainTexels.zw;
                float original = _OriginalHeights.Load(int3(texel, 0));
                return max(original - CraterDepth(positionXZ) * _DepthScale, 0.0);
            }
            ENDHLSL
        }
    }
}
