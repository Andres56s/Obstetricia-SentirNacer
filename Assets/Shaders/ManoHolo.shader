// Mano del tutorial: volumen suave propio (no depende de la luz de la sala) y borde luminoso.
Shader "MR/ManoHolo"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (0.62, 0.78, 0.95, 1)
        _Sombra ("Color en sombra", Color) = (0.16, 0.30, 0.52, 1)
        _Borde ("Borde", Color) = (0.55, 0.92, 1, 1)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 c : SV_POSITION; half3 n : TEXCOORD0; float3 w : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _Sombra, _Borde;
            CBUFFER_END
            V vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.w = TransformObjectToWorld(v.p.xyz);
                o.c = TransformWorldToHClip(o.w);
                o.n = TransformObjectToWorldNormal(v.n);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.n);
                half3 v = GetWorldSpaceNormalizeViewDir(i.w);
                // Luz principal desde arriba y a un lado de quien mira
                half3 up = half3(0, 1, 0), side = normalize(cross(v, up) + half3(0.0001, 0, 0));
                half3 l = normalize(up * 0.8 + v * 0.55 + side * 0.45);
                half d = saturate(dot(n, l) * 0.6 + 0.4);
                half3 col = lerp(_Sombra.rgb, _BaseColor.rgb, d * d * (3 - 2 * d));
                half rim = pow(1 - saturate(dot(n, v)), 2.5);
                return half4(col + _Borde.rgb * rim * 0.55, 1);
            }
            ENDHLSL
        }
    }
}
