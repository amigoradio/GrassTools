Shader "Custom/GPUInstancingBakeLitAni"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}

        _windDirect("WindDirect", Vector) = (0,0,0,0)
        _windSpeed("WindSpeed", Float) = 1.0
		_windStrengthMin("WindStrengthMin", Range(0 , 1)) = 0
		_windStrengthMax("WindStrengthMax", Range(0 , 1)) = 1
		_AnimRange ("Anim Range", Float) = 10.0
        _FadeRange ("Fade Range", Float) = 1.0
        _TextureIndex("Texture Array Index", Range(0,4)) = 0
    	_LightmapST("_LightmapST",Vector) = (0,0,0,0)
		_Color("Color", Color) = (1, 1, 1, 1)

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "BakedLit"
            Tags{ "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ DEBUG_DISPLAY

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

			#pragma vertex vert
			#pragma fragment frag


            // Lighting include is needed because of GI
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
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

                #if defined(DEBUG_DISPLAY)
                    float3 positionWS : TEXCOORD4;
                    float3 viewDirWS : TEXCOORD5;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				half4 _BaseColor;
				half _Cutoff;
                half _Glossiness;
                half _Metallic;
                half _Surface;
				half2 _windDirect;
                half _windSpeed;
				half _windStrengthMin;
				half _windStrengthMax;
				half _AnimRange;
				half _FadeRange;
			CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color) 
				UNITY_DEFINE_INSTANCED_PROP(half, _TextureIndex) 
				UNITY_DEFINE_INSTANCED_PROP(float4, _LightmapST) 
            UNITY_INSTANCING_BUFFER_END(Props) 

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
					diffuseLighting = SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME),
						TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_INDIRECTION_NAME, LIGHTMAP_SAMPLER_NAME),
						LIGHTMAP_SAMPLE_EXTRA_ARGS, transformCoords, normalWS, encodedLightmap, decodeInstructions);
				#elif defined(LIGHTMAP_ON)
					diffuseLighting = SampleSingleLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME), LIGHTMAP_SAMPLE_EXTRA_ARGS, transformCoords, encodedLightmap, decodeInstructions);
				#endif

				#if defined(DYNAMICLIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
					diffuseLighting += SampleDirectionalLightmap(TEXTURE2D_ARGS(unity_DynamicLightmap, samplerunity_DynamicLightmap),
						TEXTURE2D_ARGS(unity_DynamicDirectionality, samplerunity_DynamicLightmap),
						dynamicLightmapUV, transformCoords, normalWS, false, decodeInstructions);
				#elif defined(DYNAMICLIGHTMAP_ON)
					diffuseLighting += SampleSingleLightmap(TEXTURE2D_ARGS(unity_DynamicLightmap, samplerunity_DynamicLightmap),
						dynamicLightmapUV, transformCoords, false, decodeInstructions);
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
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);//SAMPLE_GIMpb(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = half2(0, 0);
                inputData.shadowMask = half4(1, 1, 1, 1);

            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 centerPos = TransformObjectToWorld(float3(0, 0, 0));
				float3 worldPos = vertexInput.positionWS;
				float strength = smoothstep(_windStrengthMin, _windStrengthMax, worldPos.y - centerPos.y);
				half dist = distance(_WorldSpaceCameraPos, worldPos);
				half factor = smoothstep(_AnimRange, _AnimRange + _FadeRange, dist);
				strength = lerp(strength, 0, factor);
                float customSin = sin(_Time.y * _windSpeed);
				float2 wind = _windDirect * strength * customSin;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz + float3(wind.x, wind.y, 0));

                output.uv0AndFogCoord.xy = TRANSFORM_TEX(input.uv, _BaseMap);
                #if defined(_FOG_FRAGMENT)
                    output.uv0AndFogCoord.z = vertexInput.positionVS.z;
                #else
                    output.uv0AndFogCoord.z = ComputeFogFactor(vertexInput.positionCS.z);
                #endif

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                #if defined(_NORMALMAP)
                    real sign = input.tangentOS.w * GetOddNegativeScale();
                    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                #endif

                half4 lmST = UNITY_ACCESS_INSTANCED_PROP(Props, _LightmapST);
				#if defined(LIGHTMAP_ON)
				 	output.staticLightmapUV = input.staticLightmapUV.xy * lmST.xy + lmST.zw;
				#endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half2 uv = input.uv0AndFogCoord.xy;
                #if defined(_NORMALMAP)
                    half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap)).xyz;
                #else
                    half3 normalTS = half3(0, 0, 1);
                #endif
                InputData inputData;
                InitializeInputData(input, normalTS, inputData);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 tintCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half3 color = texColor.rgb * tintCol.rgb;
                half alpha = texColor.a * tintCol.a;

                AlphaDiscard(alpha, _Cutoff);

                half4 finalColor = UniversalFragmentBakedLit(inputData, color, alpha, normalTS);

                finalColor.a = OutputAlpha(finalColor.a, _Surface);
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
            Cull[_Cull]

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

    }
    FallBack "Universal Render Pipeline/Unlit"
    //CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.BakedLitShader"

}
