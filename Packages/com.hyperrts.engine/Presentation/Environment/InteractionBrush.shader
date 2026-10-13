// Draws InteractionBrush.cs stamps into the interaction maps: each stamp is a quad over the map rect, one instance per
// stamp from _InteractionStamps. Fade subtracts whole 8-bit steps from the whole map. Not for materials.
Shader "Hidden/HyperRTS/Interaction Brush"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Matches InteractionStamp.cs.
        struct InteractionStamp
        {
            float2 center;   // world XZ
            float2 forward;  // world XZ unit direction of travel
            float2 halfSize; // x across, y along forward, metres
            float4 value;    // trample: flatten, crop, direction weight, round; tracks: rut, soil, -, round
        };

        StructuredBuffer<InteractionStamp> _InteractionStamps;
        int _InteractionStampOffset;
        float4 _HyperRTSInteractionRect; // x min, z min, 1 / width, 1 / depth
        float4 _InteractionFade;

        struct StampVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 local : TEXCOORD0; // -1..1 across and along
            nointerpolation float4 value : TEXCOORD1;
            nointerpolation float2 forward : TEXCOORD2;
        };

        // Map UV to clip space; on top-origin APIs the texture's first row is clip y = +1.
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
            InteractionStamp stamp = _InteractionStamps[_InteractionStampOffset + instanceID];
            float2 corner = corners[vertexID];
            float2 right = float2(stamp.forward.y, -stamp.forward.x);
            float2 world = stamp.center + right * (corner.x * stamp.halfSize.x) + stamp.forward * (corner.y * stamp.halfSize.y);

            StampVaryings output;
            output.positionCS = MapClip((world - _HyperRTSInteractionRect.xy) * _HyperRTSInteractionRect.zw);
            output.local = corner;
            output.value = stamp.value;
            output.forward = stamp.forward;
            return output;
        }

        // 1 inside, falling off over the outer quarter (a disc when the stamp is round).
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

        // The rut is narrower than the churned soil around it.
        half4 TracksFragment(StampVaryings input) : SV_Target
        {
            float rut = 1.0 - smoothstep(0.55, 0.85, abs(input.local.x));
            return half4(input.value.x * rut * Footprint(input, 0.9), input.value.y * Footprint(input, 0.6), 0.0, 0.0);
        }

        float4 FadeVertex(uint vertexID : SV_VertexID) : SV_POSITION
        {
            float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
            return float4(uv * 2.0 - 1.0, 0.5, 1.0);
        }

        half4 FadeFragment() : SV_Target
        {
            return (half4)_InteractionFade;
        }
        ENDHLSL

        // Steers the travel direction (RG) towards the stamp's, by its direction weight.
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

        // Flatten (B) and crop damage (A) only ever grow while stamped.
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

        // dst - _InteractionFade over the whole map.
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
