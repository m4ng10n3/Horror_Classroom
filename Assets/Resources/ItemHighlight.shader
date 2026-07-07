// Overlay additivo per evidenziare gli oggetti interagibili.
// Non sostituisce il materiale dell'oggetto: viene disegnato SOPRA la mesh (queue
// Transparent, Blend additivo, ZWrite Off) come un alone. Combina un rim/fresnel
// (bordo luminoso) con delle "sparkle" animate che scintillano nel tempo.
// Applicato a runtime da ItemHighlight.cs. Sta in Resources per essere sempre
// incluso nella build (viene caricato via Resources.Load / Shader.Find).
Shader "Custom/ItemHighlight"
{
    Properties
    {
        [HDR] _Color ("Rim Color", Color) = (1, 0.78, 0.30, 1)
        _RimPower ("Rim Power (alto = bordo sottile, più texture visibile)", Range(0.5, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 6)) = 1.3

        [HDR] _SparkleColor ("Sparkle Color", Color) = (1, 0.95, 0.7, 1)
        _SparkleScale ("Sparkle Scale", Range(1, 200)) = 55
        _SparkleSpeed ("Sparkle Blink (accensioni/spegnimenti al sec)", Range(0, 12)) = 2
        _SparkleDensity ("Sparkle Density", Range(0.85, 1.0)) = 0.985
        _SparkleStrength ("Sparkle Strength", Range(0, 8)) = 2.2

        _Pulse ("Pulse Speed", Range(0, 12)) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ItemHighlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One        // additivo: schiarisce soltanto, come un alone luminoso
            ZWrite Off
            ZTest LEqual         // uguale = passa: si sovrappone alla mesh opaca senza z-fight
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _RimPower;
                float  _RimStrength;
                float4 _SparkleColor;
                float  _SparkleScale;
                float  _SparkleSpeed;
                float  _SparkleDensity;
                float  _SparkleStrength;
                float  _Pulse;
                float  _PulseAmount;
            CBUFFER_END

            // Hash 3D -> [0,1): dà a ogni "cella" di spazio un valore pseudo-casuale stabile.
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // Rim / fresnel: massimo sui bordi in silhouette, minimo di fronte alla camera.
                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;

                // Battito lento dell'intero alone (rim). La texture al centro resta visibile:
                // il fresnel è forte solo sui bordi.
                float pulse = 1.0 + sin(_Time.y * _Pulse) * _PulseAmount;

                // Sparkle a lampeggio GLOBALE: si accende e si spegne del tutto in sincrono.
                // saturate(sin) resta a zero per metà ciclo -> in quella fase nessuna aggiunta,
                // quindi si vede la texture originale pulita; poi i punti si accendono insieme.
                float3 cell   = floor(IN.positionWS * _SparkleScale);
                float  rnd    = Hash13(cell);
                float  points = step(_SparkleDensity, rnd);
                float  flash  = pow(saturate(sin(_Time.y * _SparkleSpeed)), 3.0);
                float  spark  = points * flash * _SparkleStrength;

                float3 rimCol   = _Color.rgb * fresnel * pulse;
                float3 sparkCol = _SparkleColor.rgb * spark;

                return half4(rimCol + sparkCol, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
