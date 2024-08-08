Shader "Custom/GPUInstancingBakeLit"
{
    Properties
    {
		[MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
		[ToggleUI] _AlphaClip("__clip", Float) = 1.0
		_Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
    }

	SubShader
	{
		Tags {"Queue"="Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
		Cull Back

		Pass
		{

			Name "BakedLit"
            Tags{ "LightMode" = "UniversalForward" }
			
			HLSLPROGRAM
			#pragma only_renderers gles gles3 glcore d3d11
            #pragma target 2.0

			#pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

			#pragma multi_compile_instancing
			
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS       : POSITION;
				float2 uv               : TEXCOORD0;
				float2 staticLightmapUV : TEXCOORD1;
				float3 normalOS         : NORMAL;
				float4 tangentOS        : TANGENT;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 uv0AndFogCoord : TEXCOORD0; // xy: uv0, z: fogCoord
				DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 1);
				half3 normalWS : TEXCOORD2;
				
				#if defined(_NORMALMAP)
					half4 tangentWS : TEXCOORD3;
				#endif

				UNITY_VERTEX_INPUT_INSTANCE_ID
    			UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
    			half4 _BaseColor;
				half _Cutoff;
			CBUFFER_END

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			TEXTURE2D(_BumpMap);
			SAMPLER(sampler_BumpMap);
			
			void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
			{
				inputData = (InputData)0;
				inputData.positionWS = float3(0, 0, 0);
				inputData.viewDirectionWS = half3(0, 0, 1);
				#if defined(_NORMALMAP)
					float sgn = input.tangentWS.w;      // should be either +1 or -1
					float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
					inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
					inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
				#else
					inputData.normalWS = input.normalWS;
				#endif
					
				inputData.shadowCoord = float4(0, 0, 0, 0);
				inputData.fogCoord = input.uv0AndFogCoord.z;
				inputData.vertexLighting = half3(0, 0, 0);
				inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
				inputData.normalizedScreenSpaceUV = half2(0, 0);
				inputData.shadowMask = half4(1, 1, 1, 1);
			}

			half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap))
			{
				#ifdef _NORMALMAP
					half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
					return UnpackNormal(n);
				#else
					return half3(0.0h, 0.0h, 1.0h);
				#endif
			}

			Varyings vert(Attributes input)
			{
				Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				o.positionCS = vertexInput.positionCS;
				o.uv0AndFogCoord.xy = TRANSFORM_TEX(input.uv, _BaseMap);
				
				#if defined(_FOG_FRAGMENT)
					o.uv0AndFogCoord.z = vertexInput.positionVS.z;
				#else
					o.uv0AndFogCoord.z = ComputeFogFactor(vertexInput.positionCS.z);
				#endif
				
				VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
				o.normalWS = normalInput.normalWS;
				
				#if defined(_NORMALMAP)
					real sign = input.tangentOS.w * GetOddNegativeScale();
					o.tangentWS = half4(normalInput.tangentWS.xyz, sign);
				#endif

				OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, o.staticLightmapUV);
				OUTPUT_SH(o.normalWS, o.vertexSH);

				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				half2 uv = i.uv0AndFogCoord.xy;
				
				#if defined(_NORMALMAP)
					half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap)).xyz;
				#else
					half3 normalTS = half3(0, 0, 1);
				#endif
				
				InputData inputData;
				InitializeInputData(i, normalTS, inputData);
				
				half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
				half3 color = texColor.rgb * _BaseColor.rgb;
				half alpha = texColor.a * _BaseColor.a;

				AlphaDiscard(alpha, _Cutoff);

				half4 finalColor = UniversalFragmentBakedLit(inputData, color, alpha, normalTS);
				return finalColor;
			}

			ENDHLSL
		}

		Pass
        {
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore d3d11
            #pragma target 2.0

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore d3d11
            #pragma target 2.0

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags{"LightMode" = "Meta"}

            Cull Off

            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore d3d11
            #pragma target 2.0

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitMetaPass.hlsl"

            ENDHLSL
        }

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
	CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.BakedLitShader"
}