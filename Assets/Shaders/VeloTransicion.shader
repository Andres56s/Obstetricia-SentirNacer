// Velo de transición realidad mixta <-> sala virtual: una esfera alrededor de la cabeza que se cierra
// suavemente desde el suelo y se abre desde el techo. Sin movimiento de cámara, para no marear.
Shader "MR/VeloTransicion"
{
    Properties
    {
        _ColorBajo ("Color bajo", Color) = (0.62, 0.78, 0.86, 1)
        _ColorAlto ("Color alto", Color) = (0.93, 0.96, 0.97, 1)
        _Cubre ("Cubierto (0-1)", Range(0, 1)) = 0
        _Abre ("Abierto (0-1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBajo;
                half4 _ColorAlto;
                half _Cubre;
                half _Abre;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half h = normalize(i.dir).y * 0.5 + 0.5;              // 0 suelo, 1 techo
                const half w = 0.55;                                   // banda muy ancha = sin bordes bruscos
                half cubre = 1 - smoothstep(_Cubre * (1 + w) - w, _Cubre * (1 + w), h);
                half abre = 1 - smoothstep(_Abre * (1 + w) - w, _Abre * (1 + w), 1 - h);
                half4 c = lerp(_ColorBajo, _ColorAlto, h);
                c.a = cubre * (1 - abre);
                return c;
            }
            ENDHLSL
        }
    }
}
