Shader "Vefects/URP_Unlit_Combat_Flipbook_Advanced_01"
{
    Properties
    {
        [Header(Flipbook)]
        _FlipbookX("Flipbook X", Float) = 4
        _FlipbookY("Flipbook Y", Float) = 4

        [Header(Main)]
        _MaskTexture("Mask Texture", 2D) = "white" {}
        _AlphaTexture("Alpha Texture", 2D) = "white" {}

        [HDR]_R("R", Color) = (1,0.97,0.58,1)
        [HDR]_G("G", Color) = (1,0.72,0.25,1)
        [HDR]_B("B", Color) = (0.59,0.25,0.09,1)
        [HDR]_Outline("Outline", Color) = (0.21,0.03,0.02,1)

        _FlatColor("Flat Color", Range(0,1)) = 0
        _Emissive("Emissive", Float) = 1

        [Header(Distortion)]
        _DistortionTexture("Distortion Texture", 2D) = "gray" {}
        _DistortionLerp("Distortion Lerp", Range(0,0.1)) = 0.02
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
        _DistortionUVPan("Distortion UV Pan", Vector) = (0.1,-0.2,0,0)

        [Header(Erosion)]
        _ErosionNoise("Erosion Noise", 2D) = "white" {}
        _ErosionIntensity("Erosion Intensity", Range(0,1)) = 0
        _ErosionSmoothness("Erosion Smoothness", Float) = 0.2

        [Header(Pixelate)]
        [Toggle(_PIXELATE_ON)] _Pixelate("Pixelate", Float) = 0
        _PixelsX("Pixels X", Float) = 32
        _PixelsY("Pixels Y", Float) = 32
        _PixelsMultiplier("Pixels Multiplier", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _PIXELATE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float2 uv2         : TEXCOORD1;
            };

            TEXTURE2D(_MaskTexture);
            SAMPLER(sampler_MaskTexture);

            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_ErosionNoise);
            SAMPLER(sampler_ErosionNoise);

            float4 _MaskTexture_ST;

            float _FlipbookX;
            float _FlipbookY;

            float4 _R;
            float4 _G;
            float4 _B;
            float4 _Outline;

            float _FlatColor;
            float _Emissive;

            float4 _DistortionUVScale;
            float4 _DistortionUVPan;
            float _DistortionLerp;

            float _ErosionIntensity;
            float _ErosionSmoothness;

            float _PixelsX;
            float _PixelsY;
            float _PixelsMultiplier;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MaskTexture);
                OUT.color = IN.color;
                OUT.uv2 = IN.uv2;

                return OUT;
            }

            float2 PixelateUV(float2 uv)
            {
                float pixelWidth = 1.0 / (_PixelsX * _PixelsMultiplier);
                float pixelHeight = 1.0 / (_PixelsY * _PixelsMultiplier);

                uv.x = floor(uv.x / pixelWidth) * pixelWidth;
                uv.y = floor(uv.y / pixelHeight) * pixelHeight;

                return uv;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 flipbook = float2(_FlipbookX, _FlipbookY);

                float2 distortionUV =
                    (IN.uv * _DistortionUVScale.xy * flipbook) +
                    (_Time.y * _DistortionUVPan.xy);

                float2 distortion =
                    (SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture, distortionUV).rg - 0.5)
                    * 2.0
                    * _DistortionLerp;

                float2 finalUV = IN.uv + distortion;

                #ifdef _PIXELATE_ON
                    finalUV = PixelateUV(finalUV);
                #endif

                half4 maskTex =
                    SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, finalUV);

                half4 colorMix = lerp(_Outline, _B, maskTex.b);
                colorMix = lerp(colorMix, _G, maskTex.g);
                colorMix = lerp(colorMix, _R, maskTex.r);

                half4 finalColor =
                    lerp(IN.color * colorMix, IN.color, _FlatColor);

                finalColor.rgb *= _Emissive;

                half4 alphaTex =
                    SAMPLE_TEXTURE2D(_AlphaTexture, sampler_AlphaTexture, finalUV);

                float alpha = saturate(alphaTex.r);

                float2 erosionUV =
                    (IN.uv * flipbook) +
                    (_Time.y * 0.1);

                float erosion =
                    SAMPLE_TEXTURE2D(_ErosionNoise, sampler_ErosionNoise, erosionUV).r;

                float erosionMask =
                    smoothstep(alpha, alpha + _ErosionSmoothness, erosion);

                alpha = lerp(alpha, alpha * erosionMask, _ErosionIntensity);

                alpha *= IN.color.a;

                return half4(finalColor.rgb, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}