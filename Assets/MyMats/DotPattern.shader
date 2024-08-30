Shader "Custom/DotPatternTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DotSize ("Dot Size", Range(0.01, 1)) = 0.1
        _Tiling ("Tiling", Vector) = (10, 10, 1, 1)
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        // Enable blending for transparency
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        AlphaTest Greater 0.01

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _MainTex;
            float _DotSize;
            float4 _Tiling;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _Tiling.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 gridUV = frac(uv);
                float2 distToCenter = abs(gridUV - 0.5);
                float alpha = step(max(distToCenter.x, distToCenter.y), _DotSize);

                // Set the color with transparency based on alpha
                return float4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}