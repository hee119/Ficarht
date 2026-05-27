Shader "Vefects/URP_Coaster_01"
{
    Properties
    {
        [Header(Main)]
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)

        [Header(Distortion)]
        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Range(0,1)) = 0.05
        _DistortionSpeed("Distortion Speed", Vector) = (0,1,0,0)

        [Header(Erosion)]
        _MaskTex("Mask", 2D) = "white" {}
        _Erosion("Erosion", Range(0,1)) = 0
        _ErosionSmooth("Erosion Smooth", Range(0.001,1)) = 0.1

        [Header(Fresnel)]
        _FresnelPower("Fresnel Power", Range(0.1,10)) = 3
        _FresnelStrength("Fresnel Strength", Range(0,5)) = 1

        [Header(Emission)]
        _Emission("Emission", Range(0,10)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "Forward"

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)

            float4 _MainTex_ST;
            float4 _DistortionTex_ST;
            float4 _MaskTex_ST;

            float4 _Color;

            float _DistortionStrength;
            float4 _DistortionSpeed;

            float _Erosion;
            float _ErosionSmooth;

            float _FresnelPower;
            float _FresnelStrength;

            float _Emission;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(v.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(v.normalOS);

                o.positionHCS = posInputs.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                o.normalWS = normalInputs.normalWS;

                float3 worldPos = posInputs.positionWS;
                o.viewDirWS = GetWorldSpaceViewDir(worldPos);

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 distortionUV =
                    i.uv +
                    (_Time.y * _DistortionSpeed.xy);

                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTex,
                        sampler_DistortionTex,
                        distortionUV
                    ).rg * 2 - 1;

                distortion *= _DistortionStrength;

                float2 finalUV = i.uv + distortion;

                half4 mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        finalUV
                    );

                half mask =
                    SAMPLE_TEXTURE2D(
                        _MaskTex,
                        sampler_MaskTex,
                        finalUV
                    ).r;

                half erosion =
                    smoothstep(
                        _Erosion,
                        _Erosion + _ErosionSmooth,
                        mask
                    );

                float3 normalWS = normalize(i.normalWS);
                float3 viewDir = normalize(i.viewDirWS);

                float fresnel =
                    pow(
                        1.0 - saturate(dot(normalWS, viewDir)),
                        _FresnelPower
                    );

                fresnel *= _FresnelStrength;

                half4 col =
                    mainTex *
                    i.color *
                    _Color;

                col.rgb *= (_Emission + fresnel);

                col.a *= erosion;

                return col;
            }

            ENDHLSL
        }
    }
}