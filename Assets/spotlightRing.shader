Shader "Tutorial/UIHoleMask"
{
    Properties
    {
        _OverlayColor ("Overlay Color",      Color)  = (0.0, 0.0, 0.0, 0.75)
        _RingColor    ("Ring Color",          Color)  = (0.2, 0.8, 1.0, 1.0)
        _GlowColor    ("Glow Color",          Color)  = (1.0, 1.0, 1.0, 1.0)

        _HalfW        ("Hole Half Width",     Float)  = 0.28
        _HalfH        ("Hole Half Height",    Float)  = 0.18
        _CornerRadius ("Corner Radius",       Float)  = 0.04

        _Softness     ("Edge Softness",       Float)  = 0.012
        _RingWidth    ("Ring Width",          Float)  = 0.06
        _PulseSpeed   ("Pulse Speed",         Float)  = 2.5
        _Intensity    ("Intensity",           Float)  = 1.3
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

            fixed4 _OverlayColor;
            fixed4 _RingColor;
            fixed4 _GlowColor;

            float  _HalfW;
            float  _HalfH;
            float  _CornerRadius;
            float  _Softness;
            float  _RingWidth;
            float  _PulseSpeed;
            float  _Intensity;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f    { float4 pos:SV_POSITION;  float2 uv:TEXCOORD0; };

            // ── Signed Distance Field: hình chữ nhật bo góc ──────────────
            // < 0 : bên trong  |  = 0 : đúng cạnh  |  > 0 : bên ngoài
            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv - 0.5;   // tâm (0,0) ở giữa
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // SDF hình chữ nhật bo góc
                float2 halfSize = float2(_HalfW, _HalfH);
                float  d        = sdRoundRect(uv, halfSize, _CornerRadius);
                // d < 0 → bên trong lỗ
                // d = 0 → trên cạnh
                // d > 0 → bên ngoài (vùng overlay tối)

                // ── 1. Overlay tối bên ngoài ────────────────────────────
                // d > 0  → overlay hiện ra
                // d < 0  → trong suốt (lỗ)
                float overlayAlpha = smoothstep(-_Softness, _Softness, d);
                fixed4 overlay     = _OverlayColor;
                overlay.a         *= overlayAlpha;

                // ── 2. Ring nhấp nháy sát cạnh ──────────────────────────
                float ringMask = 1.0 - saturate(abs(d) / _RingWidth);
                ringMask       = smoothstep(0.0, 1.0, ringMask);

                // Pulse theo thời gian
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);

                // Blend 2 màu ring theo pulse
                fixed4 ringCol = lerp(_RingColor, _GlowColor, pulse);
                ringCol       *= _Intensity;
                ringCol.a     *= ringMask;

                // ── 3. Kết hợp: overlay + ring ───────────────────────────
                fixed4 result;
                result.rgb = lerp(ringCol.rgb, overlay.rgb, overlay.a * (1.0 - ringMask));
                result.a   = max(overlay.a, ringCol.a);

                return result;
            }
            ENDCG
        }
    }
}