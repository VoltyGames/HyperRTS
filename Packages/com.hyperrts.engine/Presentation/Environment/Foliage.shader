// Grass, leaves and other alpha-cut vegetation: two-sided, soft wrap lighting with light shining through from
// behind, per-plant hue variation and wind sway that grows with height above the mesh origin. Grass takes the
// ground's colour at its roots, crowns darken inside by vertex colour R, and plants can shrink away before the
// terrain's detail distance. Instanced for terrain details and trees, and for Entities Graphics (DOTS instancing).
Shader "HyperRTS/Foliage"
{
    Properties
    {
        _BaseMap ("Albedo (A = coverage)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _Translucency ("Translucency", Range(0, 1)) = 0.45
        _HueVariation ("Hue Variation", Range(0, 0.5)) = 0.12
        _WindStrength ("Wind Sway (m per m of height)", Range(0, 0.2)) = 0.03
        _WindSpeed ("Wind Speed", Float) = 1.2
        _Flutter ("Leaf Flutter (m)", Range(0, 0.2)) = 0.03
        _WindDirection ("Wind Direction (XZ)", Vector) = (0.8, 0, 0.6, 0)
        _GroundBlend ("Ground Colour At Roots", Range(0, 1)) = 0
        _GroundBlendHeight ("Ground Colour Height (UV)", Range(0.01, 1)) = 0.4
        _VertexOcclusion ("Crown Occlusion (vertex R)", Range(0, 1)) = 0
        _FadeStart ("Shrink Start (m from camera)", Float) = 10000
        _FadeEnd ("Shrink End (m from camera)", Float) = 10001
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            half _HueVariation;
            float _WindStrength;
            float _WindSpeed;
            float _Flutter;
            float4 _WindDirection;
            half _GroundBlend;
            half _GroundBlendHeight;
            half _VertexOcclusion;
            float _FadeStart;
            float _FadeEnd;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // World position of a vertex: the plant shrinks to its root between the fade distances (so terrain details
        // are gone before the terrain culls them), then leans with gusts while its leaves flutter on top of that.
        float3 PlantPosition(float3 positionOS, out float3 origin)
        {
            origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float3 world = TransformObjectToWorld(positionOS);
            float fade = saturate((_FadeEnd - distance(origin, _WorldSpaceCameraPos)) / max(_FadeEnd - _FadeStart, 1e-3));
            world = origin + (world - origin) * fade;

            float height = max(0.0, world.y - origin.y);
            float phase = _Time.y * _WindSpeed + dot(origin.xz, float2(0.11, 0.07));
            float gust = sin(phase) * 0.6 + sin(phase * 2.3 + 1.7) * 0.4;
            float2 direction = normalize(_WindDirection.xz + 1e-4);
            world.xz += direction * gust * _WindStrength * height;
            float flutter = sin(_Time.y * 7.0 + dot(world, float3(1.9, 2.3, 1.7)));
            world += float3(flutter, flutter * 0.5, -flutter) * _Flutter * saturate(height * 0.5);
            return world;
        }

        half Coverage(float2 uv)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            clip(alpha - _Cutoff);
            return alpha;
        }
        ENDHLSL

        Pass
        {
            Name "Foliage"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // The map's top-down ground colour and the world rectangle it covers (x min, z min, 1 / width, 1 / depth),
            // published by the scene's GroundColor. Outside a map nothing publishes them: the rect stays zero, which
            // turns root blending off whatever texture is bound.
            TEXTURE2D(_HyperRTSGroundColor);
            SAMPLER(sampler_HyperRTSGroundColor);
            float4 _HyperRTSGroundRect;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0; // xy albedo, z height up the card (the root is 0)
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tint : TEXCOORD3; // rgb per-plant shade, a crown occlusion
                half4 ground : TEXCOORD4; // rgb ground colour over the card's mean brightness, a root blend
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // The ground under the plant's root, divided by the card's mean brightness so the root keeps the card's
            // own light and dark detail; the blend weight is zero outside a map.
            half4 RootGround(float3 origin)
            {
                float2 uv = (origin.xz - _HyperRTSGroundRect.xy) * _HyperRTSGroundRect.zw;
                half3 ground = SAMPLE_TEXTURE2D_LOD(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, uv, 0).rgb;
                half mean = Luminance(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 16).rgb);
                half weight = _HyperRTSGroundRect.z > 0.0 ? _GroundBlend : 0.0;
                return half4(ground / max(mean, 0.02), weight);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 origin;
                output.positionWS = PlantPosition(input.positionOS.xyz, origin);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = float3(TRANSFORM_TEX(input.uv, _BaseMap), input.uv.y);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Each plant gets its own slightly warmer or cooler, lighter or darker shade.
                float random = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                half3 shade = lerp(half3(1.0, 1.0, 1.0), half3(1.12, 1.02, 0.72), random * _HueVariation * 4.0)
                    * (1.0 - _HueVariation + random * _HueVariation * 2.0);
                output.tint = half4(shade, lerp(1.0, input.color.r, _VertexOcclusion));
                output.ground = RootGround(origin);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                half4 albedo = texel * _BaseColor;
                clip(albedo.a - _Cutoff);
                albedo.rgb *= input.tint.rgb;

                // Toward the root the card turns into the ground it grows from, in colour and in light (plain
                // Lambert on the ground's up normal, nothing shining through), so grass rises out of the ground.
                half root = input.ground.a * (1.0 - smoothstep(0.0, _GroundBlendHeight, input.uv.z));
                albedo.rgb = lerp(albedo.rgb, input.ground.rgb * Luminance(texel.rgb), root);

                // Not flipped on back faces: foliage normals point up and out of the plant, so both sides of a
                // card light like the plant as a whole.
                half3 normal = normalize(lerp(input.normalWS, half3(0.0, 1.0, 0.0), root));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                // Shadow and cookie only: the sun's culling attenuation isn't set for the terrain's instanced detail draws.
                half shadow = sun.shadowAttenuation;
                half wrapping = 0.5 * (1.0 - root);
                half wrap = saturate((dot(normal, sun.direction) + wrapping) / (1.0 + wrapping));
                half crown = input.tint.a;
                half through = pow(saturate(dot(-sun.direction, view)), 4.0) * _Translucency * crown * (1.0 - root);
                half3 halfway = normalize(sun.direction + view);
                half glint = pow(saturate(dot(normal, halfway)), exp2(_Smoothness * 10.0 + 1.0)) * _Smoothness;

                half occlusion = crown;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    occlusion *= GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
                #endif

                half3 ambient = SampleSH(normal) * occlusion;
                half3 direct = sun.color * shadow;
                half3 color = albedo.rgb * (direct * (wrap * crown + through) + ambient) + glint * crown * direct;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 origin;
                float3 world = PlantPosition(input.positionOS.xyz, origin);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 toLight = normalize(_LightPosition - world);
                #else
                    float3 toLight = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(world, normal, toLight));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Coverage(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 origin;
                output.positionCS = TransformWorldToHClip(PlantPosition(input.positionOS.xyz, origin));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Coverage(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 origin;
                output.positionCS = TransformWorldToHClip(PlantPosition(input.positionOS.xyz, origin));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Coverage(input.uv);
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octahedral = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
                #else
                    return half4(normal, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
