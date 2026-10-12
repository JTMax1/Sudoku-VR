Shader "CuteMagic_SkyIsland_Pack_Free_01/ShaderMaster/Mobile_HighPerformance_PBR_Outline"
{
    Properties
    {
        [MainColor] _Tint("Main Color", Color) = (1,1,1,1)
        [MainTexture] _AlbedoMap("Albedo (RGB) Alpha(A)", 2D) = "white" {}
        
        _NormalInflow("Normal Map", 2D) = "bump" {}
        _NormalStr("Normal Strength", Range(0, 2)) = 1.0

        [Header(PBR Surface Settings)]
        _PBRComposite("PBR Mask (R:Met, G:AO, B:Rough)", 2D) = "white" {}
        _MetMult("Metallic Multiplier", Range(0, 1)) = 1.0
        _RoughMult("Roughness Multiplier", Range(0, 1)) = 1.0
        _OcclusionMult("AO Multiplier", Range(0, 1)) = 1.0

        [Header(Shadow Settings)]
        // Bổ sung Checkbox Bật/Tắt Nhận Bóng Đổ
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1.0 

        [Header(Outline Settings)]
        [Toggle(_OUTLINE_ON)] _UseOutline("Enable Outline", Float) = 1.0 
        _OutlineColor("Outline Color", Color) = (0.2, 0.2, 0.2, 1) 
        _OutlineWidth("Outline Width", Range(0.0, 10.0)) = 1.0      
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ========================================================
        // PASS 1: VẼ MODEL PBR CƠ BẢN
        // ========================================================
        Pass
        {
            Name "StandardForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // --- Khai báo keyword để nhận diện Checkbox Nhận Bóng ---
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF

            #pragma vertex VertexStage
            #pragma fragment FragmentStage

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct AppData
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 posCS    : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 nWS      : TEXCOORD1;  
                float4 tWS      : TEXCOORD3;  
                float3 vDirWS   : TEXCOORD4;  
                float3 posWS    : TEXCOORD5; 
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AlbedoMap_ST;
                half4 _Tint;
                half _NormalStr;
                half _MetMult;
                half _RoughMult;
                half _OcclusionMult;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _Tint)
                UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
                UNITY_DOTS_INSTANCED_PROP(float, _OutlineWidth)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define _Tint UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Tint)
            #endif

            TEXTURE2D(_AlbedoMap);    SAMPLER(sampler_AlbedoMap);
            TEXTURE2D(_NormalInflow); SAMPLER(sampler_NormalInflow);
            TEXTURE2D(_PBRComposite); SAMPLER(sampler_PBRComposite);

            v2f VertexStage(AppData v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o = (v2f)0;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                VertexPositionInputs posInput = GetVertexPositionInputs(v.vertex.xyz);
                o.posCS = posInput.positionCS;
                o.posWS = posInput.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _AlbedoMap);

                VertexNormalInputs normInput = GetVertexNormalInputs(v.normal, v.tangent);
                o.nWS = normInput.normalWS;
                o.tWS = float4(normInput.tangentWS, v.tangent.w);
                
                o.vDirWS = GetWorldSpaceViewDir(posInput.positionWS);
                OUTPUT_SH(o.nWS, o.vertexSH);

                return o;
            }

            half4 FragmentStage(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 baseColor = SAMPLE_TEXTURE2D(_AlbedoMap, sampler_AlbedoMap, i.uv) * _Tint;
                half4 pbrMask = SAMPLE_TEXTURE2D(_PBRComposite, sampler_PBRComposite, i.uv);
                
                half3 rawNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalInflow, sampler_NormalInflow, i.uv), _NormalStr);
                half3 bitangent = cross(i.nWS, i.tWS.xyz) * i.tWS.w;
                half3 worldNormal = normalize(mul(rawNormal, half3x3(i.tWS.xyz, bitangent, i.nWS)));

                SurfaceData sData = (SurfaceData)0;
                sData.albedo = baseColor.rgb;
                sData.metallic = pbrMask.r * _MetMult;
                sData.smoothness = saturate(1.0 - (pbrMask.b * _RoughMult));
                sData.occlusion = lerp(1.0, pbrMask.g, _OcclusionMult);
                sData.normalTS = rawNormal;
                sData.alpha = baseColor.a;

                InputData lightInput = (InputData)0;
                lightInput.positionWS = i.posWS; 
                lightInput.normalWS = worldNormal;
                lightInput.viewDirectionWS = normalize(i.vDirWS);
                lightInput.bakedGI = SampleSH(worldNormal);
                
                // --- XỬ LÝ NHẬN BÓNG ĐỔ THEO CHECKBOX ---
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    #if defined(_RECEIVE_SHADOWS_OFF)
                        // NẾU TẮT CHECKBOX: Ép tọa độ bóng về 0, ngắt kết nối với ShadowMap
                        lightInput.shadowCoord = float4(0, 0, 0, 0);
                    #else
                        // NẾU BẬT CHECKBOX: Chạy toán học dò tìm bóng bình thường
                        lightInput.shadowCoord = TransformWorldToShadowCoord(i.posWS);
                    #endif
                #else
                    lightInput.shadowCoord = float4(0, 0, 0, 0);
                #endif

                return UniversalFragmentPBR(lightInput, sData);
            }
            ENDHLSL
        }

        // ========================================================
        // PASS 2: VẼ VIỀN BẰNG NORMAL MẶC ĐỊNH CỦA BLENDER
        // ========================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma shader_feature_local _OUTLINE_ON

            #pragma vertex VertOutline
            #pragma fragment FragOutline

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AlbedoMap_ST;
                half4 _Tint;
                half _NormalStr;
                half _MetMult;
                half _RoughMult;
                half _OcclusionMult;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON) && defined(UNITY_DOTS_INSTANCING_ENABLED)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
                UNITY_DOTS_INSTANCED_PROP(float, _OutlineWidth)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define _OutlineColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColor)
            #define _OutlineWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OutlineWidth)
            #endif

            struct AttributesOutline
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsOutline
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsOutline VertOutline(AttributesOutline input)
            {
                VaryingsOutline output = (VaryingsOutline)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                #ifndef _OUTLINE_ON
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                #endif

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                vertexInput.positionCS.xy += normalize(normalCS) * vertexInput.positionCS.w * _OutlineWidth * 0.002;

                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 FragOutline(VaryingsOutline input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ========================================================
        // SHADOW & DEPTH PASSES 
        // ========================================================
        // UsePass này đảm nhiệm việc TẠO RA bóng (Caster). 
        // Muốn tối ưu tắt tạo bóng, hãy vào MeshRenderer và tắt "Cast Shadows".
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}