Shader "Custom/StarGPU"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha One   // Additive glow

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
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
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
                    up    * v.vertex.y * star.size;

                float3 finalPos = worldPos + offset;

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(finalPos, 1.0));
                o.uv = v.uv;
                o.alpha = star.alpha;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Center UV (0,0 at center)
                float2 uv = i.uv - 0.5;

                // Distance from center
                float dist = length(uv);

                // Soft circular falloff
                float glow = smoothstep(0.5, 0.0, dist);

                return fixed4(1, 1, 1, glow * i.alpha);
            }

            ENDCG
        }
    }
}