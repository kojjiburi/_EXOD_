// 들켰을 때 색수차 (ScreenAberration.cs)
// 가운데는 거의 그대로, 가장자리로 갈수록 빨강은 바깥으로 파랑은 안쪽으로 크게 갈라지고 번집니다.
Shader "EXOD/ChromaticAberrationUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Strength", Float) = 0.15
        _Jitter ("Jitter", Vector) = (0, 0, 0, 0)
        // 1: 찍은 화면(감마 값 그대로) / 0: 일반 그림(스프라이트, 컷신 이미지) — 2026-11-03 컷신 이미지에도 쓰려고 추가
        _IsScreenshot ("Is Screenshot", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float _Strength;
            float4 _Jitter;
            float _IsScreenshot;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float edge = saturate(dot(d, d) * 2.0);           // 가운데 0, 모서리 1
                float2 offset = d * (_Strength * edge) + _Jitter.xy * edge;

                float r = 0;
                float b = 0;
                [unroll]
                for (int k = 1; k <= 6; k++)
                {
                    float f = k / 6.0;
                    r += tex2D(_MainTex, i.uv - offset * f).r;
                    b += tex2D(_MainTex, i.uv + offset * f).b;
                }
                r /= 6.0;
                b /= 6.0;
                fixed4 center = tex2D(_MainTex, i.uv + _Jitter.xy * edge * 0.3);
                float g = center.g;

                fixed3 col = fixed3(r, g, b);
            #ifndef UNITY_COLORSPACE_GAMMA
                // 찍은 화면은 감마 값 그대로라 리니어 프로젝트에서는 바꿔 줘야 밝기가 맞음 (일반 그림은 이미 리니어)
                if (_IsScreenshot > 0.5)
                    col = GammaToLinearSpace(col);
            #endif
                float a = _IsScreenshot > 0.5 ? i.color.a : center.a * i.color.a;
                return fixed4(col * (_IsScreenshot > 0.5 ? 1 : i.color.rgb), a);
            }
            ENDCG
        }
    }
}
