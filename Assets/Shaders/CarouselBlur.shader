Shader "Custom/UI_CarouselBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize        ("Blur Size", Range(0, 30)) = 6
        _Color           ("Tint", Color) = (1,1,1,1)

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask",         Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Blur"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _Color;
            float     _BlurSize;
            float4    _MainTex_TexelSize;
            float4    _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(o.worldPosition);
                o.texcoord      = v.texcoord;
                o.color         = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 s  = _BlurSize * _MainTex_TexelSize.xy;

                // 5x5 Gaussian kernel (pascal triangle row: 1 4 6 4 1, sum=256)
                fixed4 c = fixed4(0, 0, 0, 0);
                c += tex2D(_MainTex, uv + float2(-2,-2)*s) *  1.0;
                c += tex2D(_MainTex, uv + float2(-1,-2)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2( 0,-2)*s) *  6.0;
                c += tex2D(_MainTex, uv + float2( 1,-2)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2( 2,-2)*s) *  1.0;

                c += tex2D(_MainTex, uv + float2(-2,-1)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2(-1,-1)*s) * 16.0;
                c += tex2D(_MainTex, uv + float2( 0,-1)*s) * 24.0;
                c += tex2D(_MainTex, uv + float2( 1,-1)*s) * 16.0;
                c += tex2D(_MainTex, uv + float2( 2,-1)*s) *  4.0;

                c += tex2D(_MainTex, uv + float2(-2, 0)*s) *  6.0;
                c += tex2D(_MainTex, uv + float2(-1, 0)*s) * 24.0;
                c += tex2D(_MainTex, uv + float2( 0, 0)*s) * 36.0;
                c += tex2D(_MainTex, uv + float2( 1, 0)*s) * 24.0;
                c += tex2D(_MainTex, uv + float2( 2, 0)*s) *  6.0;

                c += tex2D(_MainTex, uv + float2(-2, 1)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2(-1, 1)*s) * 16.0;
                c += tex2D(_MainTex, uv + float2( 0, 1)*s) * 24.0;
                c += tex2D(_MainTex, uv + float2( 1, 1)*s) * 16.0;
                c += tex2D(_MainTex, uv + float2( 2, 1)*s) *  4.0;

                c += tex2D(_MainTex, uv + float2(-2, 2)*s) *  1.0;
                c += tex2D(_MainTex, uv + float2(-1, 2)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2( 0, 2)*s) *  6.0;
                c += tex2D(_MainTex, uv + float2( 1, 2)*s) *  4.0;
                c += tex2D(_MainTex, uv + float2( 2, 2)*s) *  1.0;

                c /= 256.0;
                c *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
