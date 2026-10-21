// Selection and hover markers draped on the terrain heightmap: a crisp outline or corner brackets with a soft inner
// fill. The instance matrix scales a unit grid to the footprint and its height is the lowest the marker may sit.
Shader "HyperRTS/Ground Marker"
{
    Properties
    {
        _Lift ("Lift", Float) = 0.06
        _CameraPull ("Camera Pull", Float) = 0.25
        _MinLinePixels ("Min Line Pixels", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundMarker"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Room past the outline for its antialiased edge.
            #define EDGE_PAD 0.3

            CBUFFER_START(UnityPerMaterial)
                float _Lift;
                float _CameraPull;
                float _MinLinePixels;
            CBUFFER_END

            #include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainHeight.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Marker) // line width m, fade m, fill opacity, corner radius m (negative for brackets)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
                nointerpolation float2 halfSize : TEXCOORD1;
                nointerpolation float4 tint : TEXCOORD2;
                nointerpolation float4 marker : TEXCOORD3;
            };

            float RoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float4x4 m = UNITY_MATRIX_M;
                float2 size = float2(length(m._m00_m10_m20), length(m._m02_m12_m22));
                float2 grown = size + 2.0 * EDGE_PAD;
                float3 positionOS = float3(input.positionOS.x * grown.x / size.x, 0.0, input.positionOS.z * grown.y / size.y);

                float3 world = TransformObjectToWorld(positionOS);
                world.y = max(GroundHeight(world.xz), world.y) + _Lift;
                // Terrain LODs sag above the heightmap at range, so nudge toward the camera instead of floating higher.
                world += normalize(_WorldSpaceCameraPos - world) * _CameraPull;

                output.positionCS = TransformWorldToHClip(world);
                output.local = input.positionOS.xz * grown;
                output.halfSize = size * 0.5;
                output.tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                output.marker = UNITY_ACCESS_INSTANCED_PROP(Props, _Marker);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.local;
                float2 halfSize = input.halfSize;
                float4 marker = input.marker;
                bool brackets = marker.w < 0.0;
                float d = RoundedBox(p, halfSize, abs(marker.w));

                float pixel = max(length(fwidth(p)), 1e-4);
                float width = max(marker.x, _MinLinePixels * pixel);
                float edge = saturate(-d / pixel + 0.5) * saturate((d + width) / pixel + 0.5);

                float inside = max(-d - width, 0.0);
                float falloff = saturate(1.0 - inside / max(marker.y, 1e-3));
                float fill = marker.z * falloff * falloff * step(d, 0.0);

                if (brackets)
                {
                    float arm = clamp(min(halfSize.x, halfSize.y) * 0.3, 1.0, 4.0);
                    float2 corner = saturate((abs(p) - (halfSize - arm)) / pixel + 0.5);
                    edge *= corner.x * corner.y;
                    fill *= 0.5;
                }

                float alpha = max(edge, fill) * input.tint.a;
                return half4(input.tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
