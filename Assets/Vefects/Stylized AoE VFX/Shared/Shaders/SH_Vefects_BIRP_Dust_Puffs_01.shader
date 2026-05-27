Shader "Vefects/URP/SH_Vefects_Dust_Puffs_01"
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

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [HideInInspector] _texcoord("", 2D) = "white" {}
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
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)

            float _Emission;
            float _ErosionSmoothness;
            float _ParticleColorLUT;

            float4 _DistortionNoiseTextureSelector;
            float2 _DistortionNoiseUVScale;
            float2 _DistortionNoiseUVPanSpeed;
            float _DistortionIntensity;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;
            float _LUTErosionSmoothness;

            float _Cull;

            CBUFFER_END

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
                float4 screenPos  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2;
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 distortionUV =
                    IN.uv * _DistortionNoiseUVScale +
                    (_Time.y * _DistortionNoiseUVPanSpeed);

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

                float2 mainUV =
                    IN.uv + distortion * _DistortionIntensity;

                float4 mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        mainUV
                    );

                float erosion =
                    smoothstep(
                        IN.uv2.x,
                        IN.uv2.x + _ErosionSmoothness,
                        mainTex.g
                    );

                float lutErosion =
                    smoothstep(
                        IN.uv2.x,
                        IN.uv2.x + _LUTErosionSmoothness,
                        mainTex.g
                    );

                float2 lutUV =
                    ((saturate(lutErosion) * _LUTAmplitude) + _LUTOffset).xx;

                lutUV += (_Time.y * _LUTPanSpeed);

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

                finalColor.rgb *= (_Emission * IN.uv2.z);

                // Depth Fade
                float2 screenUV =
                    IN.screenPos.xy / IN.screenPos.w;

                float sceneDepth =
                    SampleSceneDepth(screenUV);

                float eyeSceneDepth =
                    LinearEyeDepth(
                        sceneDepth,
                        _ZBufferParams
                    );

                float eyeFragDepth =
                    LinearEyeDepth(
                        IN.positionCS.z / IN.positionCS.w,
                        _ZBufferParams
                    );

                float depthFade =
                    saturate(
                        (eyeSceneDepth - eyeFragDepth) /
                        max(IN.uv2.w, 0.0001)
                    );

                float alpha =
                    saturate(
                        erosion *
                        IN.color.a *
                        depthFade
                    );

                return half4(finalColor.rgb, alpha);
            }

            ENDHLSL
        }
    }
}