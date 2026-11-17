// Only TrampleBrush.cs draws with this, never a material.
Shader "Hidden/HyperRTS/Trample Brush"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #pragma use_dxc
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/VegetationMath.hlsl"
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/TrampleGlobals.hlsl"
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/TrampleStamp.cs.hlsl"


        StructuredBuffer<TrampleStamp> _TrampleStamps;
        int _TrampleStampOffset;
        float4 _TrampleFade;

        struct StampVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 local : TEXCOORD0;
            nointerpolation float4 value : TEXCOORD1;
            nointerpolation float2 forward : TEXCOORD2;
            nointerpolation float phase : TEXCOORD3;
            nointerpolation float rim : TEXCOORD4;
        };

        // On top-origin APIs the texture's first row is clip y = +1.
        float4 MapClip(float2 uv)
        {
            float2 clip = uv * 2.0 - 1.0;
            #if UNITY_UV_STARTS_AT_TOP
                clip.y = -clip.y;
            #endif
            return float4(clip, 0.5, 1.0);
        }

        StampVaryings StampVertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
        {
            static const float2 corners[6] =
            {
                float2(-1.0, -1.0), float2(1.0, -1.0), float2(1.0, 1.0),
                float2(-1.0, -1.0), float2(1.0, 1.0), float2(-1.0, 1.0),
            };
            TrampleStamp stamp = _TrampleStamps[_TrampleStampOffset + instanceID];
            float2 corner = corners[vertexID];
            float2 right = float2(stamp.Forward.y, -stamp.Forward.x);
            float2 world = stamp.Center + right * (corner.x * stamp.HalfSize.x) + stamp.Forward * (corner.y * stamp.HalfSize.y);

            StampVaryings output;
            output.positionCS = MapClip(TrampleUV(world));
            output.local = corner;
            output.value = stamp.Value;
            output.forward = stamp.Forward;
            output.phase = Random(float3(stamp.Center, 0.0)) * TWO_PI;
            output.rim = stamp.Rim;
            return output;
        }

        float Footprint(StampVaryings input, float inner)
        {
            float2 reach = abs(input.local);
            float edge = input.value.w > 0.5 ? length(input.local) : max(reach.x, reach.y);
            return 1.0 - smoothstep(inner, 1.0, edge);
        }

        half4 TrampleDirectionFragment(StampVaryings input) : SV_Target
        {
            float weight = Footprint(input, 0.75) * input.value.z;
            return half4(input.forward * 0.5 + 0.5, 0.0, weight);
        }

        half4 TrampleAmountFragment(StampVaryings input) : SV_Target
        {
            float weight = Footprint(input, 0.75);
            return half4(0.0, 0.0, input.value.x * weight, input.value.y * weight);
        }

        // The rut is a ramp that falls linearly to the tyre's edge, so the reader can place a crisp edge between texels
        // (Trample.hlsl RutCoverage). The churned soil around it stays soft.
        half4 TracksFragment(StampVaryings input) : SV_Target
        {
            bool round = input.value.w > 0.5;
            float across = round ? length(input.local) : abs(input.local.x);
            float ends = round ? 1.0 : 1.0 - smoothstep(0.9, 1.0, abs(input.local.y));
            float ramp = saturate((1.0 - across) * RUT_RAMP) * ends;
            return half4(input.value.x * ramp, input.value.y * Footprint(input, 0.6), 0.0, 0.0);
        }

        // The bowl's floor is flat out to this share of its radius, then the wall climbs steeply to the ground.
        #define BOWL_FLOOR 0.2
        // The lip rises from the bowl's edge, peaks just outside it and is gone by RIM_END, in bowl radii.
        #define RIM_START 0.95
        #define RIM_PEAK 1.1
        #define RIM_END 1.3

        // Wobble round the edge, so hundreds of scars don't read as stamped circles.
        float Ragged(StampVaryings input)
        {
            float angle = atan2(input.local.y, input.local.x);
            return 1.0 + 0.08 * sin(5.0 * angle + input.phase) + 0.05 * sin(3.0 * angle - 2.0 * input.phase);
        }

        // Distance from the centre in bowl radii, ragged like the scorch.
        float BowlReach(StampVaryings input)
        {
            return length(input.local) * Ragged(input) / input.value.z;
        }

        // Smooth lumps from -1 to 1 over the bowl, so a floor and a lip are never machined.
        float Lumps(StampVaryings input, float perRadius)
        {
            float2 p = input.local / input.value.z * perRadius + input.phase;
            return sin(p.x * 1.7 + sin(p.y * 2.3)) * sin(p.y * 1.9 + sin(p.x * 2.9));
        }

        // A scar without a bowl is a footprint. Alpha is the ground level: the bias plus the depth, so Max keeps the deepest.
        half4 ScarsFragment(StampVaryings input) : SV_Target
        {
            // A negative bowl radius is a trench: flat floor, steep sides, open ends so segments join.
            if (input.value.z < 0.0)
            {
                float wall = 1.0 - smoothstep(0.85, 1.0, abs(input.local.x));
                return half4(0.0, input.value.w * wall, 0.0, HYPERRTS_GROUND_LEVEL_BIAS + input.value.x * wall);
            }

            float ragged = Ragged(input);
            if (input.value.z <= 0.0)
            {
                float burnt = 1.0 - smoothstep(0.85, 1.0, max(abs(input.local.x), abs(input.local.y)) * ragged);
                return half4(0.0, input.value.w * burnt, input.value.y * burnt, 0.0);
            }

            float floorShare = 1.0 - smoothstep(BOWL_FLOOR, 1.0, BowlReach(input));
            float scorch = 1.0 - smoothstep(0.55, 1.0, length(input.local) * ragged);
            return half4(0.0, input.value.w * saturate(floorShare * 3.0), input.value.y * scorch,
                HYPERRTS_GROUND_LEVEL_BIAS + input.value.x * floorShare * (1.0 + 0.2 * Lumps(input, 5.0) + 0.1 * Lumps(input, 13.0)));
        }

        // The lip of the same scars, drawn with Min so it also buries part of an older bowl under it.
        half4 ScarRimsFragment(StampVaryings input) : SV_Target
        {
            if (input.value.z <= 0.0 || input.rim <= 0.0)
            {
                discard;
            }

            float reach = BowlReach(input);
            float lip = smoothstep(RIM_START, RIM_PEAK, reach) * (1.0 - smoothstep(RIM_PEAK, RIM_END, reach));
            if (lip <= 0.0)
            {
                discard;
            }

            // Thrown earth piles higher on some sides than others.
            float pile = saturate(0.7 + 0.3 * Lumps(input, 2.5) + 0.2 * Lumps(input, 9.0));
            return half4(0.0, 0.0, 0.0, HYPERRTS_GROUND_LEVEL_BIAS - input.rim * lip * pile);
        }

        // How level a building's footprint makes the ground here: 1 inside, easing to 0 over the skirt.
        float FillWeight(StampVaryings input)
        {
            float2 offset = input.local * float2(input.value.x + input.value.z, input.value.y + input.value.z);
            float gap = length(max(abs(offset) - input.value.xy, 0.0));
            float t = saturate(1.0 - gap / input.value.z);
            return t * t * (3.0 - 2.0 * t);
        }

        // Min caps a bowl at the level; the cap rises to the top of the range at the skirt's outer edge.
        half4 FillCapFragment(StampVaryings input) : SV_Target
        {
            return half4(0.0, 0.0, 0.0, 1.0 - FillWeight(input) * (1.0 - HYPERRTS_GROUND_LEVEL_BIAS));
        }

        // Max raises a rim to the level.
        half4 FillFloorFragment(StampVaryings input) : SV_Target
        {
            return half4(0.0, 0.0, 0.0, FillWeight(input) * HYPERRTS_GROUND_LEVEL_BIAS);
        }

        // Brightest along the edges, where the bow wave breaks.
        half4 WakeFragment(StampVaryings input) : SV_Target
        {
            float across = abs(input.local.x);
            float shape = (1.0 - smoothstep(0.75, 1.0, across)) * (1.0 - smoothstep(0.85, 1.0, abs(input.local.y)));
            float edge = lerp(0.6, 1.0, smoothstep(0.3, 0.8, across));
            float along = lerp(0.5, 1.0, input.local.y * 0.5 + 0.5);
            return half4(input.value.x * shape * edge * along, 0.0, 0.0, 0.0);
        }

        // Ragged and soft, so overlapping stains build up unevenly.
        half4 StainFragment(StampVaryings input) : SV_Target
        {
            float angle = atan2(input.local.y, input.local.x);
            float ragged = 1.0 + 0.12 * sin(4.0 * angle + input.phase) + 0.07 * sin(7.0 * angle - 1.7 * input.phase);
            float cover = 1.0 - smoothstep(0.45, 1.0, length(input.local) * ragged);
            return half4(input.value * cover);
        }

        float4 FadeVertex(uint vertexID : SV_VertexID) : SV_POSITION
        {
            float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
            return float4(uv * 2.0 - 1.0, 0.5, 1.0);
        }

        half4 FadeFragment() : SV_Target
        {
            return (half4)_TrampleFade;
        }
        ENDHLSL

        Pass
        {
            Name "TrampleDirection"
            ColorMask RG
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TrampleDirectionFragment
            ENDHLSL
        }

        // Flatten (B) and crop damage (A) only grow while stamped.
        Pass
        {
            Name "TrampleAmount"
            ColorMask BA
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TrampleAmountFragment
            ENDHLSL
        }

        Pass
        {
            Name "Tracks"
            ColorMask RG
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TracksFragment
            ENDHLSL
        }

        // Scars only deepen: they last the match.
        Pass
        {
            Name "Scars"
            ColorMask GBA
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment ScarsFragment
            ENDHLSL
        }

        Pass
        {
            Name "ScarRims"
            ColorMask A
            BlendOp Min
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment ScarRimsFragment
            ENDHLSL
        }

        Pass
        {
            Name "FillCap"
            ColorMask A
            BlendOp Min
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment FillCapFragment
            ENDHLSL
        }

        Pass
        {
            Name "FillFloor"
            ColorMask A
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment FillFloorFragment
            ENDHLSL
        }

        Pass
        {
            Name "Wake"
            ColorMask R
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment WakeFragment
            ENDHLSL
        }

        // Stains only build up: they last the match.
        Pass
        {
            Name "Stains"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment StainFragment
            ENDHLSL
        }

        Pass
        {
            Name "Fade"
            BlendOp RevSub
            Blend One One
            HLSLPROGRAM
            #pragma vertex FadeVertex
            #pragma fragment FadeFragment
            ENDHLSL
        }
    }
}
