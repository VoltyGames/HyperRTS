// Round owner-coloured pips with a dark rim, a fixed pixel size above each unit's top, drawn from _Instances by
// InstanceBuffer. ScreenOverlayFeature draws them into the finished frame, like the health bars.
Shader "HyperRTS/UnitPip"
{
    Properties
    {
        _PixelScale ("Pixel Scale", Float) = 1
        _Size ("Size Pixels", Float) = 6
        _Offset ("Offset Pixels", Float) = 2
        _Back ("Rim", Color) = (0, 0, 0, 0.7)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+19"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UnitPip"
            Tags { "LightMode" = "HyperRTSScreenOverlay" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Overlays/OverlayInstance.cs.hlsl"

            #define RIM 1.0

            CBUFFER_START(UnityPerMaterial)
                float _PixelScale;
                float _Size;
                float _Offset;
                half4 _Back;
            CBUFFER_END

            StructuredBuffer<OverlayInstance> _Instances;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 pixel : TEXCOORD0;
                nointerpolation half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                OverlayInstance instance = _Instances[input.instanceID];
                Varyings output;
                float box = _Size + RIM * 2.0;
                float2 pixel = (input.uv - 0.5) * box;
                float2 offset = float2(pixel.x, pixel.y + _Offset + box * 0.5) * _PixelScale;

                // The instance matrix only places the pip: its translation is the unit's top.
                float3 top = instance.ObjectToWorld._m03_m13_m23;
                float4 anchor = TransformWorldToHClip(top);
                anchor.xy += offset * 2.0 / _ScreenParams.xy * anchor.w * float2(1.0, _ProjectionParams.x);

                output.positionCS = anchor;
                output.pixel = pixel;
                output.color = instance.Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float distance = length(input.pixel);
                float smooth = fwidth(distance);
                float radius = _Size * 0.5;
                float inner = 1.0 - smoothstep(radius - smooth, radius, distance);
                float outer = 1.0 - smoothstep(radius + RIM - smooth, radius + RIM, distance);
                half4 color = lerp(_Back, input.color, inner);
                color.a *= outer;
                return color;
            }
            ENDHLSL
        }
    }
}
