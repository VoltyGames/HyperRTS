// Only CloudShadows.cs draws with this, into the sun's cookie; a Resources shader, so the component loads it.
Shader "Hidden/HyperRTS/Cloud Cookie"
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

        #define SMOKE_CAPACITY 32

        TEXTURE2D(_Clouds);
        float4 _CloudPlace;  // drift x and y, picture size, cookie area, in metres along the light's right and up
        float4 _CookieFocus; // camera x and y in light space, texels per side
        float4x4 _CookieWorldToLight;
        float4 _HyperRTSSmokeStarts[SMOKE_CAPACITY]; // column foot in world space, radius
        float4 _HyperRTSSmokeEnds[SMOKE_CAPACITY];   // column top in world space, opacity
        ENDHLSL

        Pass
        {
            Name "Clouds"

            HLSLPROGRAM
            #pragma vertex FullScreenVertex
            #pragma fragment CloudsFragment

            float4 FullScreenVertex(uint vertexID : SV_VertexID) : SV_POSITION
            {
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                return float4(uv * 2.0 - 1.0, 0.5, 1.0);
            }

            float CloudsFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                float area = _CloudPlace.w;
                float2 tile = (positionCS.xy / _CookieFocus.z - 0.5) * area;
                // The repeat of this texel nearest the camera, so the clouds stay seamless where the camera looks.
                float2 positionLS = tile + area * round((_CookieFocus.xy - tile) / area);
                float2 cloudUV = (positionLS - _CloudPlace.xy) / _CloudPlace.z + 0.5;
                return SAMPLE_TEXTURE2D_LOD(_Clouds, sampler_LinearRepeat, cloudUV, 0).r;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Smoke"

            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex SmokeVertex
            #pragma fragment SmokeFragment

            struct SmokeVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 positionLS : TEXCOORD0;
                nointerpolation uint column : TEXCOORD1;
            };

            float2 LightSpace(float3 positionWS)
            {
                return mul(_CookieWorldToLight, float4(positionWS, 1.0)).xy;
            }

            // Four copies per column, shifted a whole cookie toward its middle, so a quad over the wrap edge draws on both sides.
            SmokeVaryings SmokeVertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                uint column = instanceID >> 2;
                float2 foot = LightSpace(_HyperRTSSmokeStarts[column].xyz);
                float2 top = LightSpace(_HyperRTSSmokeEnds[column].xyz);
                float reach = _HyperRTSSmokeStarts[column].w * 2.0;
                uint corner = vertexID % 6;
                float2 quad = float2(corner == 1 || corner == 2 || corner == 4 ? 1.0 : 0.0,
                    corner == 2 || corner == 4 || corner == 5 ? 1.0 : 0.0);

                SmokeVaryings output;
                output.positionLS = lerp(min(foot, top) - reach, max(foot, top) + reach, quad);
                output.column = column;
                float area = _CloudPlace.w;
                float2 footUV = frac(foot / area + 0.5);
                float2 copy = float2(instanceID & 1, (instanceID >> 1) & 1) * (1.0 - 2.0 * step(0.5, footUV));
                float2 uv = output.positionLS / area + 0.5 - floor(foot / area + 0.5) + copy;
                #if UNITY_UV_STARTS_AT_TOP
                output.positionCS = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.5, 1.0);
                #else
                output.positionCS = float4(uv * 2.0 - 1.0, 0.5, 1.0);
                #endif
                return output;
            }

            // The column's foot-to-top segment seen from the sun, widening and thinning as it rises, soft to its rim.
            float SmokeFragment(SmokeVaryings input) : SV_Target
            {
                float2 foot = LightSpace(_HyperRTSSmokeStarts[input.column].xyz);
                float2 along = LightSpace(_HyperRTSSmokeEnds[input.column].xyz) - foot;
                float2 offset = input.positionLS - foot;
                float t = saturate(dot(offset, along) / max(dot(along, along), 1e-4));
                float radius = _HyperRTSSmokeStarts[input.column].w * (1.0 + t);
                float rim = smoothstep(radius, 0.0, length(offset - along * t));
                float body = (0.6 + 0.4 * smoothstep(0.0, 0.3, t)) * (1.0 - 0.6 * t);
                return 1.0 - _HyperRTSSmokeEnds[input.column].w * body * rim;
            }
            ENDHLSL
        }
    }
}
