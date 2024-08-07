Shader "Custom/GPUInstancingUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags {"Queue" = "Geometry" "IgnoreProjector" = "True"  "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        Cull Back
        
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_instancing //  INSTANCING_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color) 
            UNITY_INSTANCING_BUFFER_END(Props) 

            #pragma vertex vert
            #pragma fragment frag
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 tintCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * tintCol;
                return col;
            }
            ENDHLSL
        }
    }
}
