Shader "Vefects/SH_Vefects_URP_Central_Area_01"
{
    Properties
    {
        _Emission("Emission", Float) = 1
        _IsAdd("Is Add", Float) = 0
        _ErosionSmoothness("Erosion Smoothness", Float) = 1
        _ParticleColorLUT("Particle Color / LUT", Float) = 0

        [Space(20)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _MainTextureSelector("Main Texture Selector", Vector) = (0,1,0,0)
        _MainTextureUVScale("Main Texture UV Scale", Vector) = (1,1,0,0)
        _MainTextureUVPanSpeed("Main Texture UV Pan Speed", Vector) = (0.01,0.3,0,0)

        [Space(20)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0
        _LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1

        [Space(20)]
        _DistortionTexture("Distortion Texture", 2D) = "white" {}
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
        _DistortionUVPanSpeed("Distortion UV Pan Speed", Vector) = (-0.02,0.5,0,0)
        _DistortionAmount("Distortion Amount", Float) = 0.3

        [Space(20)]
        _PanCutoutMask("Pan Cutout Mask", 2D) = "white" {}
        _PanCutoutMaskPower("Pan Cutout Mask Power", Float) = 1
        _PanCutoutMaskMultiply("Pan Cutout Mask Multiply", Float) = 1

        [Space(20)]
        _CutoutMask("Cutout Mask", 2D) = "white" {}
        _CutoutMaskPower("Cutout Mask Power", Float) = 1
        _CutoutMaskMultiply("Cutout Mask Multiply", Float) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_CutoutMask);
            SAMPLER(sampler_CutoutMask);

            TEXTURE2D(_PanCutoutMask);
            SAMPLER(sampler_PanCutoutMask);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Emission;
            float _IsAdd;
            float _ErosionSmoothness;
            float _ParticleColorLUT;

            float4 _MainTextureSelector;
            float2 _MainTextureUVScale;
            float2 _MainTextureUVPanSpeed;

            float2 _DistortionUVScale;
            float2 _DistortionUVPanSpeed;
            float _DistortionAmount;

            float _CutoutMaskPower;
            float _CutoutMaskMultiply;

            float _PanCutoutMaskPower;
            float _PanCutoutMaskMultiply;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;
            float _LUTErosionSmoothness;

            Varyings vert(Attributes v)
            {
                Varyings o;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 mainUV =
                    i.uv * _MainTextureUVScale +
                    _Time.y * _MainTextureUVPanSpeed;

                float2 distortionUV =
                    i.uv * _DistortionUVScale +
                    _Time.y * _DistortionUVPanSpeed;

                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTexture,
                        sampler_DistortionTexture,
                        distortionUV
                    ).rg * _DistortionAmount;

                float4 mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        mainUV + distortion
                    );

                float dotResult = dot(mainTex, _MainTextureSelector);

                float cutout =
                    pow(
                        SAMPLE_TEXTURE2D(
                            _CutoutMask,
                            sampler_CutoutMask,
                            i.uv
                        ).g,
                        _CutoutMaskPower
                    ) * _CutoutMaskMultiply;

                float2 panUV = i.uv + float2(0, i.uv2.y);

                float panMask =
                    pow(
                        SAMPLE_TEXTURE2D(
                            _PanCutoutMask,
                            sampler_PanCutoutMask,
                            panUV
                        ).g,
                        _PanCutoutMaskPower
                    ) * _PanCutoutMaskMultiply;

                float combined =
                    saturate(dotResult) *
                    saturate(cutout) *
                    saturate(panMask) * 2.0;

                combined = saturate(combined);

                float erosion =
                    smoothstep(
                        i.uv2.x,
                        i.uv2.x + _ErosionSmoothness,
                        combined
                    );

                float lutErosion =
                    smoothstep(
                        i.uv2.x,
                        i.uv2.x + _LUTErosionSmoothness,
                        combined
                    );

                float2 lutUV =
                    float2(
                        saturate(lutErosion) * _LUTAmplitude + _LUTOffset,
                        0
                    );

                lutUV += _Time.y * _LUTPanSpeed;

                float3 lutColor =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        lutUV
                    ).rgb;

                float4 finalColor =
                    lerp(
                        i.color,
                        i.color * float4(lutColor, 1),
                        _ParticleColorLUT
                    );

                finalColor =
                    lerp(
                        finalColor,
                        finalColor * erosion,
                        _IsAdd
                    );

                finalColor.rgb *= (_Emission);

                finalColor.a *= erosion;

                return finalColor;
            }

            ENDHLSL
        }
    }
}