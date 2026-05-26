Shader "Universal Render Pipeline/Vefects/FlipbookPixelAdvanced"
{
    Properties
    {
        _MainTexture("Main Texture", 2D) = "white" {}

        [HDR]_R("R", Color) = (1,0.97,0.58,1)
        [HDR]_G("G", Color) = (1,0.72,0.25,1)
        [HDR]_B("B", Color) = (0.59,0.25,0.09,1)
        [HDR]_Outline("Outline", Color) = (0.21,0.03,0.02,1)

        _FlatColor("Flat Color", Range(0,1)) = 0
        _Emissive("Emissive", Float) = 1

        _FlipbookX("Flipbook X", Float) = 7
        _FlipbookY("Flipbook Y", Float) = 1

        _UVS("UV S", Vector) = (1,1,0,0)
        _UVP("UV P", Vector) = (0,0,0,0)

        [Header(Dissolve)]
        _disolveMap("Dissolve Map", 2D) = "white" {}
        _DissolveMapScale("Dissolve Map Scale", Float) = 1

        [Header(Distortion)]
        _DistortionTexture("Distortion Texture", 2D) = "white" {}
        _DistortionLerp("Distortion Lerp", Range(0,0.1)) = 0
        _UVDS("UV D S", Vector) = (1,1,0,0)
        _UVDP("UV D P", Vector) = (0.1,-0.2,0,0)

        [Toggle]_Pixelate("Pixelate", Float) = 0
        _PixelsMultiplier("Pixels Multiplier", Float) = 1
        _PixelsX("Pixels X", Float) = 32
        _PixelsY("Pixels Y", Float) = 32

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

            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _PIXELATE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_disolveMap);
            SAMPLER(sampler_disolveMap);

            float4 _MainTexture_ST;

            float4 _R;
            float4 _G;
            float4 _B;
            float4 _Outline;

            float _FlatColor;
            float _Emissive;

            float _FlipbookX;
            float _FlipbookY;

            float2 _UVS;
            float2 _UVP;

            float2 _UVDS;
            float2 _UVDP;

            float _DistortionLerp;

            float _Pixelate;
            float _PixelsMultiplier;
            float _PixelsX;
            float _PixelsY;

            float _DissolveMapScale;

            Varyings vert(Attributes v)
            {
                Varyings o;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

                o.uv = TRANSFORM_TEX(v.uv, _MainTexture);
                o.uv2 = v.uv2;
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 baseUV =
                    (i.uv * _UVS) +
                    (_Time.y * _UVP);

                float2 distortionUV =
                    (i.uv * _UVDS) +
                    (_Time.y * _UVDP);

                float2 distortion =
                    (SAMPLE_TEXTURE2D(
                        _DistortionTexture,
                        sampler_DistortionTexture,
                        distortionUV).rg - 0.5) * 2.0;

                distortion *= _DistortionLerp;

                float2 finalUV = baseUV + distortion;

                #ifdef _PIXELATE_ON

                    float pixelWidth =
                        1.0 / (_PixelsX * _PixelsMultiplier);

                    float pixelHeight =
                        1.0 / (_PixelsY * _PixelsMultiplier);

                    finalUV.x =
                        floor(finalUV.x / pixelWidth) * pixelWidth;

                    finalUV.y =
                        floor(finalUV.y / pixelHeight) * pixelHeight;

                #endif

                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        finalUV);

                float4 col = lerp(_Outline, _B, tex.b);
                col = lerp(col, _G, tex.g);
                col = lerp(col, _R, tex.r);

                float4 finalColor =
                    lerp(i.color * col, i.color, _FlatColor);

                finalColor *= _Emissive;

                // dissolve
                float opacityW = i.uv2.z;
                float opacityT = i.uv2.w;

                float remap =
                    (opacityT - 1.0) +
                    (opacityW) *
                    (1.0 - (opacityT - 1.0));

                float2 flipbookScale =
                    float2(_FlipbookX, _FlipbookY);

                float2 dissolveUV =
                    ((i.uv * flipbookScale)
                    * _DissolveMapScale)
                    + _FlipbookX;

                float dissolve =
                    smoothstep(
                        remap,
                        remap + opacityT,
                        SAMPLE_TEXTURE2D(
                            _disolveMap,
                            sampler_disolveMap,
                            dissolveUV).g
                    );

                float alpha =
                    tex.a *
                    i.color.a *
                    dissolve;

                return float4(finalColor.rgb, alpha);
            }

            ENDHLSL
        }
    }
}