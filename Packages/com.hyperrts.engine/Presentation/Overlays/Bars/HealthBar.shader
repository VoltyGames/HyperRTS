// Screen-aligned health bars of a fixed pixel size, with squad member and veterancy pips above and a squad's
// suppression strip below, drawn from
// _Instances by InstanceBuffer. Each sits above its anchor; sizes are reference pixels times _PixelScale.
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
        _Suppressed ("Suppressed", Color) = (0.98, 0.8, 0.25, 1)
        _Pinned ("Pinned", Color) = (0.9, 0.25, 0.2, 1)
        _Retreat ("Retreating", Color) = (0.45, 0.7, 1, 1)
        _HeavyCover ("Heavy Cover", Color) = (0.35, 0.85, 0.4, 1)
        _LightCover ("Light Cover", Color) = (0.95, 0.85, 0.25, 1)
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
            #pragma target 4.5
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PIP 4.0
            #define PIP_STEP 6.0
            #define PIP_ROW 7.0
            #define STRIP_ROW 3.0

            CBUFFER_START(UnityPerMaterial)
                float _PixelScale;
                float _Offset;
                half4 _Back;
                half4 _High;
                half4 _Mid;
                half4 _Low;
                half4 _RankPip;
                half4 _MemberPip;
                half4 _Suppressed;
                half4 _Pinned;
                half4 _Retreat;
                half4 _HeavyCover;
                half4 _LightCover;
            CBUFFER_END

            struct HealthBarInstance // HealthBarInstance.cs
            {
                float4 bar;  // health fraction, segments, rank, opacity
                float4 size; // width px, bar height px, squad members, members alive
                float4 squad; // suppression (-1 not a squad), level, cover kind, retreating
                float3 anchor;
            };

            StructuredBuffer<HealthBarInstance> _Instances;

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
                nointerpolation float4 bar : TEXCOORD1;
                nointerpolation float4 size : TEXCOORD2;
                nointerpolation float4 squad : TEXCOORD3;
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
                HealthBarInstance instance = _Instances[input.instanceID];
                Varyings output;
                float4 bar = instance.bar;
                float4 size = instance.size;
                bool pips = size.z > 0.0 || bar.z > 0.0;
                float strip = instance.squad.x >= 0.0 ? STRIP_ROW : 0.0;
                float2 box = float2(size.x + 2.0, size.y + 2.0 + strip + (pips ? PIP_ROW : 0.0));

                float2 pixel = input.uv * box;
                float2 offset = float2(pixel.x - box.x * 0.5, pixel.y + _Offset) * _PixelScale;
                float4 anchor = TransformWorldToHClip(instance.anchor);
                anchor.xy += offset * 2.0 / _ScreenParams.xy * anchor.w * float2(1.0, _ProjectionParams.x);

                output.positionCS = anchor;
                output.pixel = pixel;
                output.bar = bar;
                output.size = size;
                output.squad = instance.squad;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.pixel;
                float strip = input.squad.x >= 0.0 ? STRIP_ROW : 0.0;
                p.y -= strip;
                float width = input.size.x;
                float height = input.size.y;
                float fraction = input.bar.x;
                float segments = max(input.bar.y, 1.0);

                half4 color = half4(0, 0, 0, 0);
                if (p.y < 0.0)
                {
                    float retreating = input.squad.w;
                    float level = input.squad.y;
                    half4 tint = _MemberPip;
                    float amount = input.squad.x;
                    if (retreating > 0.5)
                    {
                        tint = _Retreat;
                        amount = 1.0;
                    }
                    else if (level > 1.5)
                    {
                        tint = _Pinned;
                    }
                    else if (level > 0.5)
                    {
                        tint = _Suppressed;
                    }
                    float filled = BoxMask(p, float2(1.0, -strip), float2(1.0 + width * amount, -1.0));
                    color = lerp(_Back, tint, filled);
                }
                else if (p.y <= height + 2.0)
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

                    float2 shield = float2(p.x - members * PIP_STEP - 2.0, row) - float2(1.0 + PIP * 0.5, 0.0);
                    float taper = lerp(1.0, PIP * 0.5, saturate((shield.y - 1.0) / 2.0));
                    float inShield = step(abs(shield.x), taper) * step(0.5, row) * step(row, 1.0 + PIP);
                    half4 coverTint = input.squad.z > 1.5 ? _HeavyCover : _LightCover;
                    color = lerp(color, coverTint, inShield * step(0.5, input.squad.z));

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
