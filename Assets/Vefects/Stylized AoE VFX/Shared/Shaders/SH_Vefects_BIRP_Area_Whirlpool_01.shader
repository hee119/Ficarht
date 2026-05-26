Shader "Universal Render Pipeline/Vefects/URP_Area_Whirlpool_01"
{
    Properties
    {
        _Emission("Emission", Float) = 1
        _ErosionSmoothness("Erosion Smoothness", Float) = 1

        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _RadialUVTile("Radial UV Tile", Vector) = (1,1,0,0)
        _RadialUVPanSpeed("Radial UV Pan Speed", Vector) = (0.01,-0.5,0,0)

        _RadialUVDistortNoise("Radial UV Distort Noise", 2D) = "white" {}
        _RadialUVDistortScale("Radial UV Distort Scale", Vector) = (1,1,0,0)
        _RadialUVDistortSpeed("Radial UV Distort Speed", Vector) = (0.1,0.01,0,0)
        _RadialUVDistortIntensity("Radial UV Distort Intensity", Float) = 0.1

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTPanSpeed("LUT Pan Speed", Float) = 0
        _LUTOffset("LUT Offset", Float) = 0

        [Header(Distortion)]
        _DistortionNoise("Distortion Noise", 2D) = "white" {}
        _DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
        _DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
        _DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
        _DistortionIntensity("Distortion Intensity", Float) = 0.03

        [Header(Cutout)]
        _CutoutTexture("Cutout Texture", 2D) = "white" {}
        _CutoutEro("Cutout Ero", Float) = 0
        _CutoutEroSmooth("Cutout Ero Smooth", Float) = 0.3

        [Header(Render)]
        _Cull("Cull", Float) = 2
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
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_RadialUVDistortNoise);
            SAMPLER(sampler_RadialUVDistortNoise);

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_CutoutTexture);
            SAMPLER(sampler_CutoutTexture);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Emission;
            float _ErosionSmoothness;

            float2 _RadialUVTile;
            float2 _RadialUVPanSpeed;

            float2 _RadialUVDistortScale;
            float2 _RadialUVDistortSpeed;
            float _RadialUVDistortIntensity;

            float _LUTAmplitude;
            float _LUTPanSpeed;
            float _LUTOffset;

            float4 _DistortionNoiseTextureSelector;
            float2 _DistortionNoiseUVScale;
            float2 _DistortionNoiseUVPanSpeed;
            float _DistortionIntensity;

            float _CutoutEro;
            float _CutoutEroSmooth;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2;
                OUT.color = IN.color;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Radial UV
                float2 centeredUV = uv * 2.0 - 1.0;

                float angle = frac(atan2(centeredUV.x, centeredUV.y) / 6.2831853);
                float radius = length(centeredUV);

                float2 radialUV = float2(angle, radius);

                // Radial Distort
                float2 distortUV =
                    uv * _RadialUVDistortScale +
                    _Time.y * _RadialUVDistortSpeed;

                float2 radialNoise =
                    SAMPLE_TEXTURE2D(
                        _RadialUVDistortNoise,
                        sampler_RadialUVDistortNoise,
                        distortUV
                    ).rg;

                radialNoise *= _RadialUVDistortIntensity;

                // Radial Pan
                radialUV += _Time.y * _RadialUVPanSpeed;
                radialUV *= _RadialUVTile;

                float2 finalMainUV = radialUV + radialNoise;

                // Distortion
                float2 distortionUV =
                    uv * _DistortionNoiseUVScale +
                    _Time.y * _DistortionNoiseUVPanSpeed;

                float4 distortionTex =
                    SAMPLE_TEXTURE2D(
                        _DistortionNoise,
                        sampler_DistortionNoise,
                        distortionUV
                    );

                float distortion =
                    dot(distortionTex, _DistortionNoiseTextureSelector);

                distortion = saturate(distortion);
                distortion = (distortion - 0.5) * 2.0;

                finalMainUV += distortion * _DistortionIntensity;

                // Cutout
                float cutout =
                    SAMPLE_TEXTURE2D(
                        _CutoutTexture,
                        sampler_CutoutTexture,
                        uv
                    ).g;

                cutout = smoothstep(
                    _CutoutEro,
                    _CutoutEro + _CutoutEroSmooth,
                    cutout
                );

                // Main Texture
                float mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        finalMainUV
                    ).g;

                mainTex *= cutout;

                float erosion = smoothstep(
                    IN.uv2.x,
                    IN.uv2.x + _ErosionSmoothness,
                    saturate(mainTex)
                );

                erosion = saturate(erosion);

                // LUT
                float lutUV =
                    erosion * _LUTAmplitude +
                    _LUTOffset +
                    (_Time.y * _LUTPanSpeed);

                float3 lutColor =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        float2(lutUV, 0.5)
                    ).rgb;

                float3 emission =
                    IN.color.rgb *
                    lutColor *
                    (_Emission * IN.uv2.z);

                float alpha =
                    erosion *
                    IN.color.a;

                return half4(emission, alpha);
            }

            ENDHLSL
        }
    }
}