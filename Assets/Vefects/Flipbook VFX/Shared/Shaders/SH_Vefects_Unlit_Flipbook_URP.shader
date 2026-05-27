Shader "Vefects/SH_Vefects_Unlit_Flipbook_URP"
{
    Properties
    {
        _MainTexture("Main Texture", 2D) = "white" {}
        [HDR]_R("R", Color) = (1,1,1,1)
        [HDR]_G("G", Color) = (1,1,1,1)
        [HDR]_B("B", Color) = (1,1,1,1)
        [HDR]_Outline("Outline", Color) = (0,0,0,1)

        _Intensity("Intensity", Range(0,5)) = 1

        _UVS("UV Scale", Vector) = (1,1,0,0)
        _UVP("UV Panner", Vector) = (0,0,0,0)

        _DistortionTexture("Distortion Texture", 2D) = "gray" {}
        _DistortionLerp("Distortion Lerp", Range(0,0.1)) = 0.02

        _disolveMap("Dissolve", 2D) = "white" {}
        _ErosionSmoothness("Erosion Smoothness", Range(0.01,15)) = 0.1

        [Toggle]_PIXELATE_ON("Pixelate", Float) = 0
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
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Forward"

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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_disolveMap);
            SAMPLER(sampler_disolveMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _MainTexture_ST;

            float4 _R;
            float4 _G;
            float4 _B;
            float4 _Outline;

            float _Intensity;

            float2 _UVS;
            float2 _UVP;

            float _DistortionLerp;

            float _ErosionSmoothness;

            float _PixelsX;
            float _PixelsY;
            float _PixelsMultiplier;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTexture);
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Panner
                float2 mainUV =
                    uv * _UVS +
                    (_Time.y * _UVP);

                // Distortion
                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTexture,
                        sampler_DistortionTexture,
                        uv
                    ).rg;

                distortion = (distortion - 0.5) * 2.0;
                mainUV += distortion * _DistortionLerp;

                // Pixelate
                #ifdef _PIXELATE_ON

                float pixelWidth =
                    1.0 / (_PixelsX * _PixelsMultiplier);

                float pixelHeight =
                    1.0 / (_PixelsY * _PixelsMultiplier);

                mainUV.x =
                    floor(mainUV.x / pixelWidth) * pixelWidth;

                mainUV.y =
                    floor(mainUV.y / pixelHeight) * pixelHeight;

                #endif

                // Main Texture
                half4 tex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        mainUV
                    );

                // Channel Color Blend
                half4 col = _Outline;

                col = lerp(col, _B, tex.b);
                col = lerp(col, _G, tex.g);
                col = lerp(col, _R, tex.r);

                col *= i.color;
                col.rgb *= _Intensity;

                // Dissolve
                float dissolve =
                    SAMPLE_TEXTURE2D(
                        _disolveMap,
                        sampler_disolveMap,
                        uv
                    ).r;

                float alpha =
                    smoothstep(
                        0,
                        _ErosionSmoothness,
                        tex.a * dissolve
                    );

                alpha *= i.color.a;

                return half4(col.rgb, alpha);
            }

            ENDHLSL
        }
    }
}