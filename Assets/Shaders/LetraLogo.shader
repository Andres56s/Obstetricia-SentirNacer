// Letras 3D del logo: blanco con volumen propio, destello que las recorre y recorte contra el borde del disco
// (así, al posarse, quedan exactamente como en el logo).
Shader "MR/LetraLogo"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Lado ("Color de los costados", Color) = (0.62, 0.72, 0.95, 1)
        _Barrido ("Posición del destello", Float) = -9
        _Brillo ("Brillo extra", Range(0, 1)) = 0
        _Corte ("Radio de recorte (m)", Float) = 99
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _Lado;
            float _Barrido, _Corte;
            half _Brillo;
            float4x4 _Disco; // mundo -> disco
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 c : SV_POSITION; half3 n : TEXCOORD0; float3 d : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 w = TransformObjectToWorld(v.p.xyz);
                o.c = TransformWorldToHClip(w);
                o.n = TransformObjectToWorldNormal(v.n);
                o.d = mul(_Disco, float4(w, 1)).xyz;
                return o;
            }
            half4 frag(V i, bool cara : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(_Corte - length(i.d.xz));
                half3 n = normalize(i.n);
                half k = cara ? saturate(abs(n.y) * 1.15) : 0.55;
                half3 col = lerp(_Lado.rgb, _BaseColor.rgb, k);
                half b = exp(-pow((i.d.x + i.d.z * 0.6 - _Barrido) * 22, 2));
                col += half3(0.35, 0.8, 1) * b * 0.55 - (1 - k) * 0.02;
                col = lerp(col, half3(0.75, 0.95, 1), _Brillo * 0.6);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 c : SV_POSITION; float3 d : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 w = TransformObjectToWorld(v.p.xyz);
                o.d = mul(_Disco, float4(w, 1)).xyz;
                o.c = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(w, TransformObjectToWorldNormal(v.n), _LightDirection)));
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                clip(_Corte - length(i.d.xz));
                return 0;
            }
            ENDHLSL
        }
    }
}
