Shader "Tutorial/UIOverlayMask"
{
    Properties
    {
        _Color("Color", Color) = (0,0,0,0.8)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }

        // Bước 1: Đọc Stencil
        // Nếu pixel có Stencil = 1 (do ring ghi vào) → KHÔNG vẽ
        Stencil
        {
            Ref 1
            Comp NotEqual   // Vẽ chỗ KHÔNG có stencil = 1
            Pass Keep
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 pos:SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}