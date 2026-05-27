Shader "Vefects/SH_Vefects_URP_Disc_Impact_01"
{
    Properties
    {
        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _Emission("Emission", Float) = 1
        _ErosionSmoothness("Erosion Smoothness", Float) = 1
        _ParticleColorLUT("Particle Color / LUT", Float) = 0

        [Header(Distortion)]
        _DistortionNoise("Distortion Noise", 2D) = "white" {}
        _DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
        _DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
        _DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
        _DistortionIntensity("Distortion Intensity", Float) = 0.03

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0
        _LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1

        [Header(Noise Sec)]
        _NoiseSec("Noise Sec", 2D) = "white" {}
        _NoiseSecUVScale("Noise Sec UV Scale", Vector) = (1,1,0,0)

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Float) = 0
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
            ZWrite [_ZWrite]

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

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_NoiseSec);
            SAMPLER(sampler_NoiseSec);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Emission;
            float _ErosionSmoothness;
            float _ParticleColorLUT;

            float4 _DistortionNoiseTextureSelector;
            float2 _DistortionNoiseUVScale;
            float2 _DistortionNoiseUVPanSpeed;
            float _DistortionIntensity;

            float2 _NoiseSecUVScale;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;
            float _LUTErosionSmoothness;

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
                float2 distortionUV =
                    IN.uv * _DistortionNoiseUVScale +
                    _Time.y * _DistortionNoiseUVPanSpeed;

                float4 distortionTex =
                    SAMPLE_TEXTURE2D(
                        _DistortionNoise,
                        sampler_DistortionNoise,
                        distortionUV
                    );

                float distortion =
                    dot(distortionTex, _DistortionNoiseTextureSelector);

                distortion = (saturate(distortion) - 0.5) * 2.0;

                float2 finalUV =
                    IN.uv + distortion * _DistortionIntensity;

                float2 noiseUV =
                    IN.uv * _NoiseSecUVScale +
                    float2(0, IN.uv2.w);

                float noiseSec =
                    SAMPLE_TEXTURE2D(
                        _NoiseSec,
                        sampler_NoiseSec,
                        noiseUV
                    ).g;

                float noiseLerp = lerp(1.0, noiseSec, IN.uv2.z);

                float mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        finalUV
                    ).g;

                mainTex = saturate(mainTex * noiseLerp);

                float erosion =
                    smoothstep(
                        IN.uv2.x,
                        IN.uv2.x + _ErosionSmoothness,
                        mainTex
                    );

                float lutErosion =
                    smoothstep(
                        IN.uv2.x,
                        IN.uv2.x + _LUTErosionSmoothness,
                        mainTex
                    );

                float lutValue =
                    saturate(lutErosion) *
                    _LUTAmplitude +
                    _LUTOffset;

                float2 lutUV =
                    float2(lutValue, lutValue + _Time.y * _LUTPanSpeed);

                float3 lutColor =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        lutUV
                    ).rgb;

                float4 finalColor =
                    lerp(
                        IN.color,
                        IN.color * float4(lutColor, 1),
                        _ParticleColorLUT
                    );

                float alpha =
                    saturate(erosion * IN.color.a);

                float3 emission =
                    finalColor.rgb *
                    _Emission;

                return float4(emission, alpha);
            }

            ENDHLSL
        }
    }
}