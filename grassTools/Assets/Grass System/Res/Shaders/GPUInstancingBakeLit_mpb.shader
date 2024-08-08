Shader "Custom/GPUInstancingBakeLit_mpb"
{
    Properties
    {
		[MainTexture] _BaseMap("Texture", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
		[ToggleUI] _AlphaClip("AlphaClip", Float) = 0.0
		_Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
		_Textures("Texture Array", 2DArray) = "" {}
    	_TextureIndex("Texture Array Index", Range(0,4)) = 0
    	_LightmapST("_LightmapST",Vector) = (0,0,0,0)
		_Color("Color", Color) = (1, 1, 1, 1)
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
				float2 staticLightmapUV : TEXCOORD1;
				float3 vertexSH : TEXCOORD2;
				half3 normalWS : TEXCOORD3;
				
				#if defined(_NORMALMAP)
					half4 tangentWS : TEXCOORD4;
				#endif

				UNITY_VERTEX_INPUT_INSTANCE_ID
    			UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				half4 _BaseColor;
				half _Cutoff;
			CBUFFER_END

			Texture2DArray _Textures;
			SAMPLER(sampler_Textures);

			UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color) 
				UNITY_DEFINE_INSTANCED_PROP(half, _TextureIndex) 
				UNITY_DEFINE_INSTANCED_PROP(float4, _LightmapST) 
            UNITY_INSTANCING_BUFFER_END(Props) 

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			TEXTURE2D(_BumpMap);
			SAMPLER(sampler_BumpMap);



			// Sample baked and/or realtime lightmap. Non-Direction and Directional if available.
			half3 SampleLightmapMpb(float2 staticLightmapUV, float2 dynamicLightmapUV, half3 normalWS)
			{
				#ifdef UNITY_LIGHTMAP_FULL_HDR
					bool encodedLightmap = false;
				#else
					bool encodedLightmap = true;
				#endif
				half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
				half4 transformCoords = half4(1, 1, 0, 0);
				float3 diffuseLighting = 0;

				#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
					staticLightmapUV = staticLightmapUV * transformCoords.xy + transformCoords.zw;
					real4 direction = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, staticLightmapUV, 1);
    				//real4 direction = SAMPLE_TEXTURE2D_LIGHTMAP(lightmapDirTex, lightmapDirSampler, LIGHTMAP_EXTRA_ARGS_USE);
    				// Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    				real3 illuminance = real3(0.0, 0.0, 0.0);
    				if (encodedLightmap)
    				{
						real4 encodedIlluminance = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, staticLightmapUV, 0).rgba;
        				//real4 encodedIlluminance = SAMPLE_TEXTURE2D_LIGHTMAP(lightmapTex, lightmapSampler, LIGHTMAP_EXTRA_ARGS_USE).rgba;
        				illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
    				}
    				else
    				{
						illuminance = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, staticLightmapUV, 0).rgb;
        				//illuminance = SAMPLE_TEXTURE2D_LIGHTMAP(lightmapTex, lightmapSampler, LIGHTMAP_EXTRA_ARGS_USE).rgb;
    				}

    				real halfLambert = dot(normalWS, direction.xyz - 0.5) + 0.5;
    				diffuseLighting += illuminance * halfLambert / max(1e-4, direction.w);

				#elif defined(LIGHTMAP_ON)
					staticLightmapUV = staticLightmapUV * transformCoords.xy + transformCoords.zw;
    				// Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    				if (encodedLightmap)
    				{
						real4 encodedIlluminance = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, staticLightmapUV, 0).rgba;
        				//real4 encodedIlluminance = SAMPLE_TEXTURE2D_LIGHTMAP(lightmapTex, lightmapSampler, LIGHTMAP_EXTRA_ARGS_USE).rgba;
        				diffuseLighting = DecodeLightmap(encodedIlluminance, decodeInstructions);
    				}
    				else
    				{
						diffuseLighting = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, staticLightmapUV, 0).rgb;
        				//diffuseLighting = SAMPLE_TEXTURE2D_LIGHTMAP(lightmapTex, lightmapSampler, LIGHTMAP_EXTRA_ARGS_USE).rgb;
    				}
				#endif

    			return diffuseLighting;
			}
			
			half3 SAMPLE_GIMpb(float2 staticLmName, float3 shName, half3 normalWSName)
			{
				half3 gi = half3(0, 0, 0);
				#if defined(LIGHTMAP_ON)
				 	gi = SampleLightmapMpb(staticLmName, 0, normalWSName);
				#else
				 	gi = SampleSHPixel(shName, normalWSName);
				#endif
				return gi;
			}

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
				inputData.bakedGI = SAMPLE_GIMpb(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
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
				
				 half4 lmST = UNITY_ACCESS_INSTANCED_PROP(Props, _LightmapST);
				#if defined(LIGHTMAP_ON)
					o.staticLightmapUV = input.staticLightmapUV.xy * lmST.xy + lmST.zw;
				#endif
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
				half4 tintCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
				half3 color = texColor.rgb * tintCol.rgb;
				half alpha = texColor.a * tintCol.a;
				
				AlphaDiscard(alpha, _Cutoff);

				half4 finalColor = UniversalFragmentBakedLit(inputData, color, alpha, normalTS);
				return finalColor;
			}

			ENDHLSL
		}

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
	CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.BakedLitShader"
}