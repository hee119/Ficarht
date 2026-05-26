Shader "Vefects/URP_Disc_Windup_01"
{
    Properties
    {
        _IsAdd("Is Add", Float) = 0

        [Header(Mask)]
        _MainMask("Main Mask", 2D) = "white" {}
        _MaskErosion("Mask Erosion", Float) = 0
        _MaskErosionSmoothness("Mask Erosion Smoothness", Float) = 0.3

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTOffset("LUT Offset", Float) = 0
        _Emissive("Emissive", Float) = 1

        [Header(Distortion)]
        _DistortionNoise("Distortion Noise", 2D) = "white" {}
        _DistortionIntensity("Distortion Intensity", Float) = 0.1
        _DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
        _DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)

        [Header(Refraction)]
        _RefractionNoise1("Refraction Noise", 2D) = "white" {}
        _RefractionAmount("Refraction Amount", Float) = 1
        _RefractionErosion("Refraction Erosion", Float) = 0
        _RefractionErosionSmoothness("Refraction Erosion Smoothness", Float) = 0.3

        [Header(Cutout)]
        _Cutout("Cutout", 2D) = "white" {}

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [Toggle] _ZWrite("ZWrite", Float) = 0
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

            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;

                float4 screenPos  : TEXCOORD2;

                float4 color      : COLOR;
            };

            TEXTURE2D(_MainMask);
            SAMPLER(sampler_MainMask);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_RefractionNoise1);
            SAMPLER(sampler_RefractionNoise1);

            TEXTURE2D(_Cutout);
            SAMPLER(sampler_Cutout);

            // URP Camera Opaque Texture
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)

            float _IsAdd;

            float _MaskErosion;
            float _MaskErosionSmoothness;

            float _LUTOffset;
            float _Emissive;

            float _DistortionIntensity;
            float2 _DistortionNoiseUVScale;
            float2 _DistortionNoiseUVPanSpeed;

            float _RefractionAmount;
            float _RefractionErosion;
            float _RefractionErosionSmoothness;

            float4 _Cutout_ST;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = posInputs.positionCS;

                o.uv = v.uv;
                o.uv2 = v.uv2;

                o.screenPos = ComputeScreenPos(o.positionCS);

                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // distortion
                float2 distortionUV =
                    uv * _DistortionNoiseUVScale +
                    (_Time.y * _DistortionNoiseUVPanSpeed);

                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionNoise,
                        sampler_DistortionNoise,
                        distortionUV
                    ).rg;

                distortion = (distortion - 0.5) * 2.0;
                distortion *= _DistortionIntensity;

                float2 disUV = float2(
                    uv.x,
                    uv.y + i.uv2.x
                ) + distortion;

                // refraction mask
                float refractionNoise =
                    SAMPLE_TEXTURE2D(
                        _RefractionNoise1,
                        sampler_RefractionNoise1,
                        disUV
                    ).g;

                float refractionMask =
                    smoothstep(
                        _RefractionErosion,
                        _RefractionErosion + _RefractionErosionSmoothness,
                        refractionNoise
                    );

                float verticalFade = saturate(1.0 - uv.y);

                float refractionFinal =
                    saturate(refractionMask * verticalFade);

                // screen uv
                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                float refractionStrength =
                    _RefractionAmount * i.uv2.x;

                float2 refractedUV =
                    screenUV +
                    (refractionFinal.xx * refractionStrength);

                // opaque texture sample
                half4 screenCol =
                    SAMPLE_TEXTURE2D_X(
                        _CameraOpaqueTexture,
                        sampler_CameraOpaqueTexture,
                        refractedUV
                    );

                half4 refracted =
                    lerp(
                        screenCol,
                        screenCol * refractionFinal,
                        _IsAdd
                    );

                // mask
                float maskNoise =
                    SAMPLE_TEXTURE2D(
                        _MainMask,
                        sampler_MainMask,
                        disUV
                    ).g;

                float mask =
                    smoothstep(
                        _MaskErosion,
                        _MaskErosion + _MaskErosionSmoothness,
                        maskNoise
                    );

                mask = saturate(mask * verticalFade);

                // LUT
                float lutUV =
                    (i.uv2.y * mask) + _LUTOffset;

                half3 lutColor =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        float2(lutUV, lutUV)
                    ).rgb;

                half3 emissive =
                    i.color.rgb *
                    i.uv2.y *
                    lutColor *
                    _Emissive;

                half3 finalColor =
                    lerp(
                        refracted.rgb,
                        emissive,
                        mask
                    );

                // cutout alpha
                float2 cutoutUV =
                    uv * _Cutout_ST.xy +
                    _Cutout_ST.zw;

                float cutout =
                    SAMPLE_TEXTURE2D(
                        _Cutout,
                        sampler_Cutout,
                        cutoutUV
                    ).g;

                float alpha =
                    saturate(
                        refractionFinal *
                        i.color.a *
                        cutout
                    );

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}