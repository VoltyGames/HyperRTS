Shader "Hidden/HyperRTS/DebugCells"
{
    Properties
    {
        _MainTex ("Cells", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(float4 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(position);
                output.uv = uv;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                return tex2D(_MainTex, input.uv);
            }
            ENDCG
        }
    }
}
