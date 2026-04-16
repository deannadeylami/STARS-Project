Shader "Custom/StarGPU"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Star
            {
                float3 position;
                float size;
                float alpha;
            };

            StructuredBuffer<Star> _StarBuffer;

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float alpha : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                Star star = _StarBuffer[v.instanceID];

                float3 worldPos = star.position;

                // Billboard toward camera
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                float3 offset =
                    right * v.vertex.x * star.size +
                    up * v.vertex.y * star.size;

                float3 finalPos = worldPos + offset;

                v2f o;
                o.pos = UnityObjectToClipPos(float4(finalPos, 1.0));
                o.alpha = star.alpha;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(1,1,1,i.alpha);
            }
            ENDCG
        }
    }
}