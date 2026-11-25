// Muros, piso y techo de la sala virtual: textura (puede ser una foto real) iluminada por la luz indirecta precalculada
// del atardecer más el sol en tiempo real con sombras. Más barato que URP/Lit, pensado para Quest.
Shader "MR/MuroSala"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura o foto", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _LuzMap ("Luz indirecta", 2D) = "gray" {}
        _LuzFuerza ("Fuerza de la luz indirecta", Range(0, 3)) = 1.5
        _Brillo ("Brillo (piso pulido)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LuzMap); SAMPLER(sampler_LuzMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _LuzFuerza;
                half _Brillo;
            CBUFFER_END

            // Ventanal en el espacio de la sala (lo fija AmbienteAtardecer)
            float4x4 _SalaLocal;
            float4 _SalaSol;      // xyz hacia el sol, w = z del muro del ventanal
            float4 _SalaVentana;  // x0, x1, y0, y1
            float4 _SalaMarcos;   // x de los tres montantes, y del travesaño

            half Barra(float d, float medio, float pen)
            {
                return saturate((medio - abs(d)) / pen + 0.5) * min(1, 2 * medio / pen);
            }

            // Sol que pasa por el ventanal, con penumbra que crece con la distancia (bordes suaves sin depender del mapa de sombras)
            half SolPorVentanal(float3 positionWS)
            {
                if (_SalaSol.z >= -0.01) return 1;
                float3 p = mul(_SalaLocal, float4(positionWS, 1)).xyz;
                float t = max(0, (_SalaSol.w - p.z) / _SalaSol.z);
                float2 q = p.xy + _SalaSol.xy * t;
                float pen = 0.01 + 0.016 * t;
                float4 e = float4(q.x - _SalaVentana.x, _SalaVentana.y - q.x, q.y - _SalaVentana.z, _SalaVentana.w - q.y) - 0.03;
                e = smoothstep(0, 1, saturate(e / pen + 0.5));
                half m = e.x * e.y * e.z * e.w;
                m *= 1 - Barra(q.x - _SalaMarcos.x, 0.03, pen);
                m *= 1 - Barra(q.x - _SalaMarcos.y, 0.03, pen);
                m *= 1 - Barra(q.x - _SalaMarcos.z, 0.03, pen);
                return m * (1 - Barra(q.y - _SalaMarcos.w, 0.0225, pen));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = float4(v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw, v.uv);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy).rgb * _BaseColor.rgb;
                half3 luz = SAMPLE_TEXTURE2D(_LuzMap, sampler_LuzMap, i.uv.zw).rgb * _LuzFuerza;
                half3 n = normalize(i.normalWS);
                Light sol = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sombra = sol.shadowAttenuation * SolPorVentanal(i.positionWS);
                half3 directa = sol.color * (saturate(dot(n, sol.direction)) * sombra);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 h = normalize(sol.direction + v);
                half3 reflejo = sol.color * (pow(saturate(dot(n, h)), 60) * sombra * _Brillo);
                half fresnel = pow(1 - saturate(dot(n, v)), 4) * _Brillo;
                return half4(albedo * (luz + directa) + reflejo + luz * fresnel * 0.5, 1);
            }
            ENDHLSL
        }
    }
}
