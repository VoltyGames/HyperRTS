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
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment CraterHeightFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Craters/CraterSink.hlsl"

            Texture2D<float> _OriginalHeights;
            float4 _HeightRegion;  // first heightmap texel of the region in x and z
            float4 _TerrainTexels; // terrain origin x and z, metres per heightmap texel in x and z
            float _DepthScale;     // heightmap units per metre
            float _TerrainY;       // world height of heightmap 0

            // Matches BuildingLevel.cs: a footprint the ground is levelled to a height under, easing out over a skirt.
            struct BuildingLevel
            {
                float2 center;
                float2 right;
                float2 half;
                float target; // world height
                float padding;
            };
            StructuredBuffer<BuildingLevel> _BuildingLevels;
            int _BuildingLevelCount;
            float _LevelSkirt;

            float LevelWeight(BuildingLevel level, float2 positionXZ)
            {
                float2 offset = positionXZ - level.center;
                float2 forward = float2(-level.right.y, level.right.x);
                float2 local = float2(dot(offset, level.right), dot(offset, forward));
                float gap = length(max(abs(local) - level.half, 0.0));
                return smoothstep(0.0, 1.0, 1.0 - gap / _LevelSkirt);
            }

            // Pixel rows and heightmap rows both count from memory row 0, on every graphics API.
            float CraterHeightFragment(Varyings input) : SV_Target
            {
                int2 texel = (int2)_HeightRegion.xy + (int2)input.positionCS.xy;
                float2 positionXZ = _TerrainTexels.xy + texel * _TerrainTexels.zw;
                float original = _OriginalHeights.Load(int3(texel, 0));
                float levelled = original;
                for (int i = 0; i < _BuildingLevelCount; i++)
                {
                    BuildingLevel level = _BuildingLevels[i];
                    levelled = lerp(levelled, (level.target - _TerrainY) * _DepthScale, LevelWeight(level, positionXZ));
                }

                return max(levelled - CraterDepth(positionXZ) * _DepthScale, 0.0);
            }
            ENDHLSL
        }
    }
}
