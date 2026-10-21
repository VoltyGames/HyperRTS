// Screen-aligned health bars of a fixed pixel size, with squad member and veterancy pips above. The instance matrix
// only places the anchor at the top of the model; sizes are reference pixels times _PixelScale.
Shader "HyperRTS/Health Bar"
{
    Properties
    {
        _PixelScale ("Pixel Scale", Float) = 1
        _Offset ("Offset Pixels", Float) = 10
        _Back ("Backing", Color) = (0, 0, 0, 0.8)
        _High ("Healthy", Color) = (0.5, 0.8, 0.35, 1)
        _Mid ("Wounded", Color) = (0.95, 0.75, 0.2, 1)
        _Low ("Critical", Color) = (0.9, 0.25, 0.2, 1)
        _RankPip ("Rank Pip", Color) = (1, 0.8, 0.4, 1)
        _MemberPip ("Member Pip", Color) = (0.92, 0.92, 0.88, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HealthBar"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PIP 4.0
            #define PIP_STEP 6.0
            #define PIP_ROW 7.0

            CBUFFER_START(UnityPerMaterial)
                float _PixelScale;
                float _Offset;
                half4 _Back;
                half4 _High;
                half4 _Mid;
                half4 _Low;
                half4 _RankPip;
                half4 _MemberPip;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Bar)  // health fraction, segments, rank, opacity
                UNITY_DEFINE_INSTANCED_PROP(float4, _Size) // width px, bar height px, squad members, members alive
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 pixel : TEXCOORD0;
                nointerpolation float4 bar : TEXCOORD1;
                nointerpolation float4 size : TEXCOORD2;
            };

            float BoxMask(float2 p, float2 low, float2 high)
            {
                float2 inside = step(low, p) * step(p, high);
                return inside.x * inside.y;
            }

            half4 FillColor(float fraction)
            {
                return fraction < 0.5 ? lerp(_Low, _Mid, fraction * 2.0) : lerp(_Mid, _High, fraction * 2.0 - 1.0);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float4 bar = UNITY_ACCESS_INSTANCED_PROP(Props, _Bar);
                float4 size = UNITY_ACCESS_INSTANCED_PROP(Props, _Size);
                bool pips = size.z > 0.0 || bar.z > 0.0;
                float2 box = float2(size.x + 2.0, size.y + 2.0 + (pips ? PIP_ROW : 0.0));

                float2 pixel = input.uv * box;
                float2 offset = float2(pixel.x - box.x * 0.5, pixel.y + _Offset) * _PixelScale;
                float4 anchor = TransformWorldToHClip(UNITY_MATRIX_M._m03_m13_m23);
                anchor.xy += offset * 2.0 / _ScreenParams.xy * anchor.w * float2(1.0, _ProjectionParams.x);

                output.positionCS = anchor;
                output.pixel = pixel;
                output.bar = bar;
                output.size = size;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.pixel;
                float width = input.size.x;
                float height = input.size.y;
                float fraction = input.bar.x;
                float segments = max(input.bar.y, 1.0);

                half4 color = half4(0, 0, 0, 0);
                if (p.y <= height + 2.0)
                {
                    color = _Back;
                    float x = p.x - 1.0;
                    float filled = BoxMask(p, float2(1.0, 1.0), float2(1.0 + width * fraction, height + 1.0));
                    float gap = step(frac(x * segments / width), segments / width) * step(1.0, x);
                    half4 fill = FillColor(fraction);
                    fill.rgb *= p.y > height ? 1.2 : 1.0;
                    color = lerp(color, fill, filled * (1.0 - gap));
                }
                else
                {
                    float row = p.y - height - 3.0;
                    float members = input.size.z;
                    float member = floor(p.x / PIP_STEP);
                    float2 cell = float2(p.x - member * PIP_STEP, row);
                    float inPip = BoxMask(cell, float2(1.0, 1.0), float2(1.0 + PIP, 1.0 + PIP));
                    float inBack = BoxMask(cell, float2(0.0, 0.0), float2(2.0 + PIP, 2.0 + PIP));
                    if (member < members)
                    {
                        half4 pip = _MemberPip;
                        pip.a *= member < input.size.w ? 1.0 : 0.12;
                        color = lerp(_Back * inBack, pip, inPip);
                    }

                    float rank = floor((width + 2.0 - p.x) / PIP_STEP);
                    float2 rankCell = float2(width + 2.0 - p.x - rank * PIP_STEP, row) - (1.0 + PIP * 0.5);
                    float diamond = step(abs(rankCell.x) + abs(rankCell.y), PIP * 0.6);
                    float diamondBack = step(abs(rankCell.x) + abs(rankCell.y), PIP * 0.6 + 1.2);
                    if (rank < input.bar.z)
                    {
                        color = lerp(_Back * diamondBack, _RankPip, diamond);
                    }
                }

                color.a *= input.bar.w;
                return color;
            }
            ENDHLSL
        }
    }
}
