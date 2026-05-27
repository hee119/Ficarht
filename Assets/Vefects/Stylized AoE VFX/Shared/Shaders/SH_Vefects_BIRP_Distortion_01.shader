Shader "Vefects/URP_Distortion_01"
{
    Properties
    {
        [Header(Cutout)]
        _CutoutTexture("Cutout Texture", 2D) = "white" {}
        _CutoutMaskSelector("Cutout Mask Selector", Vector) = (0,1,0,0)

        [Header(Distortion Noise)]
        _DistortionNoise("Distortion Noise", 2D) = "white" {}
        _DistortionNoiseSelector("Distortion Noise Selector", Vector) = (0,1,0,0)
        _DistUVS("Dist UV S", Vector) = (1,1,0,0)
        _DistUVP("Dist UV P", Vector) = (0,0,0,0)
        _DistortionLerp("Distortion Lerp", Float) = 1

        [Header(Distortion Dist Noise)]
        _DistortionDist("Distortion Dist", 2D) = "white" {}
        _DistortionDistSelector("Distortion Dist Selector", Vector) = (0,1,0,0)
        _DistDistUVS("Dist Dist UV S", Vector) = (1,1,0,0)
        _DistDistUVP("Dist Dist UV P", Vector) = (0,0,0,0)
        _DistortionDistLerp("Distortion Dist Lerp", Float) = 0.1

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
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
                float4 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float4 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;

                float4 color      : COLOR;
            };

            TEXTURE2D(_CutoutTexture);
            SAMPLER(sampler_CutoutTexture);

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_DistortionDist);
            SAMPLER(sampler_DistortionDist);

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)

            float4 _CutoutMaskSelector;

            float4 _DistortionNoiseSelector;
            float2 _DistUVS;
            float2 _DistUVP;
            float _DistortionLerp;

            float4 _DistortionDistSelector;
            float2 _DistDistUVS;
            float2 _DistDistUVP;
            float _DistortionDistLerp;

            float4 _CutoutTexture_ST;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = posInputs.positionCS;

                o.uv = v.uv;

                o.screenPos = ComputeScreenPos(o.positionCS);

                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 baseUV = i.uv.xy;

                // distortion dist noise
                float2 distDistUV =
                    baseUV * _DistDistUVS +
                    (_Time.y * _DistDistUVP);

                float4 distDistSample =
                    SAMPLE_TEXTURE2D(
                        _DistortionDist,
                        sampler_DistortionDist,
                        distDistUV
                    );

                float distDistMask =
                    saturate(
                        dot(
                            distDistSample,
                            _DistortionDistSelector
                        )
                    );

                float2 distOffset =
                    lerp(
                        float2(0,0),
                        distDistMask.xx,
                        _DistortionDistLerp
                    );

                // main distortion
                float2 distortionUV =
                    baseUV * _DistUVS +
                    (_Time.y * _DistUVP);

                distortionUV += distOffset;

                float4 distortionSample =
                    SAMPLE_TEXTURE2D(
                        _DistortionNoise,
                        sampler_DistortionNoise,
                        distortionUV
                    );

                float distortionMask =
                    saturate(
                        dot(
                            distortionSample,
                            _DistortionNoiseSelector
                        )
                    );

                // cutout
                float2 cutoutUV =
                    baseUV * _CutoutTexture_ST.xy +
                    _CutoutTexture_ST.zw;

                float4 cutoutSample =
                    SAMPLE_TEXTURE2D(
                        _CutoutTexture,
                        sampler_CutoutTexture,
                        cutoutUV
                    );

                float cutoutMask =
                    saturate(
                        dot(
                            cutoutSample,
                            _CutoutMaskSelector
                        )
                    );

                // final distortion
                float finalMask =
                    saturate(
                        distortionMask *
                        cutoutMask
                    );

                float distortionStrength =
                    _DistortionLerp * i.uv.z;

                float2 distortion =
                    lerp(
                        float2(0,0),
                        finalMask.xx,
                        distortionStrength
                    );

                // screen uv
                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                float2 refractedUV =
                    screenUV + distortion;

                // opaque texture
                half4 screenColor =
                    SAMPLE_TEXTURE2D_X(
                        _CameraOpaqueTexture,
                        sampler_CameraOpaqueTexture,
                        refractedUV
                    );

                return half4(
                    screenColor.rgb,
                    i.color.a
                );
            }

            ENDHLSL
        }
    }
}