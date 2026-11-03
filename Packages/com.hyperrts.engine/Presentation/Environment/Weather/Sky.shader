// A cubemap sky, as Unity's Skybox/Cubemap draws it, whose ground below the horizon gives way to the sky's own
// horizon colour: a photographed sky's ground is close-up, so where a long view runs out of land the sky carries on
// as haze instead. The haze holds down to half the horizon band and fades back to the photographed ground by its end,
// so ambient light from straight below keeps the ground's colour.
Shader "HyperRTS/Sky"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
        _HorizonBand ("Horizon Band (degrees below)", Range(1, 60)) = 20
        _HorizonBlur ("Horizon Blur (mip)", Range(0, 8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/SkyHorizon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                float _Rotation;
                float4 _Tex_HDR;
                float _HorizonBand;
                float _HorizonBlur;
            CBUFFER_END

            TEXTURECUBE(_Tex);
            SAMPLER(sampler_Tex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                float angle = _Rotation * PI / 180.0;
                float2 turn = float2(cos(angle), sin(angle));
                float3 position = input.positionOS.xyz;
                float3 turned = float3(position.x * turn.x - position.z * turn.y, position.y,
                    position.x * turn.y + position.z * turn.x);
                Varyings output;
                output.positionCS = TransformObjectToHClip(turned);
                output.direction = position;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half3 sky = SkyColor(TEXTURECUBE_ARGS(_Tex, sampler_Tex), _Tex_HDR, _Tint.rgb, _Exposure, direction, 0.0);
                [branch] if (direction.y < 0.0)
                {
                    // Straight down has no bearing; any will do, as the ground there is as photographed.
                    float2 bearing = dot(direction.xz, direction.xz) > 1e-8 ? normalize(direction.xz) : float2(1.0, 0.0);
                    half3 horizon = SkyHorizonColor(TEXTURECUBE_ARGS(_Tex, sampler_Tex), _Tex_HDR, _Tint.rgb, _Exposure,
                        bearing, _HorizonBlur);
                    half below = smoothstep(0.5, 1.0, -direction.y / sin(_HorizonBand * PI / 180.0));
                    sky = lerp(horizon, sky, below);
                }

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
