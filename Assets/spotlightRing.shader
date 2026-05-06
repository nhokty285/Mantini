Shader "Tutorial/UIHoleMask"
{
    Properties
    {
        _RingColor  ("Ring Color",    Color)  = (0.2, 0.8, 1.0, 1.0)
        _GlowColor  ("Glow Color",    Color)  = (1.0, 1.0, 1.0, 1.0)
        _Radius     ("Hole Radius",   Float)  = 0.35
        _Softness   ("Edge Softness", Float)  = 0.04
        _RingWidth  ("Ring Width",    Float)  = 0.08
        _FresnelPow ("Fresnel Power", Float)  = 3.0
        // ↑ Power càng cao → glow càng tập trung vào rìa
        _PulseSpeed ("Pulse Speed",   Float)  = 2.0
        _Intensity  ("Intensity",     Float)  = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }

        Stencil { Ref 1  Comp Always  Pass Replace }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _RingColor;
            fixed4 _GlowColor;
            float  _Radius;
            float  _Softness;
            float  _RingWidth;
            float  _FresnelPow;
            float  _PulseSpeed;
            float  _Intensity;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f    { float4 pos:SV_POSITION;  float2 uv:TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv - 0.5; // Center (0,0) tại tâm
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = length(i.uv);

                // ── Lỗ trong suốt ────────────────────────────────────
                float innerEdge = _Radius - _Softness;
                float holeAlpha = smoothstep(innerEdge, innerEdge + _Softness * 2, dist);
                // dist < innerEdge → alpha = 0 (trong suốt = lỗ)

                // ── Fresnel: tính "góc nhìn" từ tâm ra rìa ───────────
                // Với UI 2D, dùng dist làm proxy cho fresnel
                float fresnelMask = 1.0 - saturate((dist - _Radius + _RingWidth) / _RingWidth);
                float fresnel     = pow(1.0 - fresnelMask, _FresnelPow);
                // fresnelPow = 2 → glow đều
                // fresnelPow = 5 → glow chỉ tập trung ở rìa ngoài

                // ── Ring boundary ─────────────────────────────────────
                float outerEdge  = _Radius + _RingWidth * 0.5;
                float ringFade   = 1.0 - smoothstep(outerEdge - _Softness, outerEdge + _Softness, dist);

                // ── Pulse theo thời gian ───────────────────────────────
                float pulse      = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);

                // ── Kết hợp màu ───────────────────────────────────────
                // Lerp từ RingColor (rìa trong) → GlowColor (rìa ngoài)
                fixed4 col       = lerp(_RingColor, _GlowColor, fresnel);
                col              *= _Intensity * pulse;
                col.a            = holeAlpha * ringFade * col.a;

                return col;
            }
            ENDCG
        }
    }
}