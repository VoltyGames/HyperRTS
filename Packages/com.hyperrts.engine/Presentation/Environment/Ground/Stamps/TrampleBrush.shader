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

        // A ragged rim keeps hundreds of scars from reading as stamped circles. A scar without a bowl is a footprint.
        half4 ScarsFragment(StampVaryings input) : SV_Target
        {
            // A negative bowl radius is a trench: flat floor, steep sides, open ends so segments join.
            if (input.value.z < 0.0)
            {
                float wall = 1.0 - smoothstep(0.85, 1.0, abs(input.local.x));
                return half4(0.0, input.value.w * wall, 0.0, input.value.x * wall);
            }

            float angle = atan2(input.local.y, input.local.x);
            float ragged = 1.0 + 0.08 * sin(5.0 * angle + input.phase) + 0.05 * sin(3.0 * angle - 2.0 * input.phase);
            if (input.value.z <= 0.0)
            {
                float burnt = 1.0 - smoothstep(0.85, 1.0, max(abs(input.local.x), abs(input.local.y)) * ragged);
                return half4(0.0, input.value.w * burnt, input.value.y * burnt, 0.0);
            }

            float reach = length(input.local) * ragged;
            float bowl = saturate(1.0 - Sq(reach / input.value.z));
            float scorch = 1.0 - smoothstep(0.55, 1.0, reach);
            return half4(0.0, input.value.w * saturate(bowl * 3.0), input.value.y * scorch, input.value.x * bowl * bowl);
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

        // Ragged and soft, so overlapping remains build up unevenly.
        half4 RemainsFragment(StampVaryings input) : SV_Target
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
            Name "Wake"
            ColorMask R
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment WakeFragment
            ENDHLSL
        }

        // Remains only build up: they last the match.
        Pass
        {
            Name "Remains"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment RemainsFragment
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
