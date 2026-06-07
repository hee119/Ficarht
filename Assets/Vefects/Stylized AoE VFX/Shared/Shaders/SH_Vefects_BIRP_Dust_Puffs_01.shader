Shader "Vefects/URP/SH_Vefects_Dust_Puffs_01_Fixed"
{
    Properties
    {
        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _Emission("Emission", Float) = 1
        _ErosionSmoothness("Erosion Smoothness", Float) = 1
        _ParticleColorLUT("Particle Color(0) / LUT(1)", Range(0, 1)) = 0

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
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTexture); SAMPLER(sampler_MainTexture);
            TEXTURE2D(_DistortionNoise); SAMPLER(sampler_DistortionNoise);
            TEXTURE2D(_LUT); SAMPLER(sampler_LUT);

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
                float4 uv2        : TEXCOORD1; // 파티클 Custom Data (x: Erosion, y: Random, z: Emission, w: DepthFade)
                float4 color      : COLOR;    // Color over Lifetime
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
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2; 
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Distortion 연산
                float2 distortionUV = IN.uv * _DistortionNoiseUVScale + (_Time.y * _DistortionNoiseUVPanSpeed);
                float4 distortionTex = SAMPLE_TEXTURE2D(_DistortionNoise, sampler_DistortionNoise, distortionUV);
                float distortion = saturate(dot(distortionTex, _DistortionNoiseTextureSelector));
                distortion = (distortion - 0.5) * 2.0;

                // 2. Main Texture & Erosion
                float2 mainUV = IN.uv + distortion * _DistortionIntensity;
                float4 mainTex = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, mainUV);
                
                // IN.uv2.x는 파티클 시스템의 Custom Data에서 넘어오는 '침식' 값입니다.
                float erosion = smoothstep(IN.uv2.x, IN.uv2.x + _ErosionSmoothness, mainTex.g);
                float lutErosion = smoothstep(IN.uv2.x, IN.uv2.x + _LUTErosionSmoothness, mainTex.g);

                // 3. LUT Color 연산
                float2 lutUV = ((saturate(lutErosion) * _LUTAmplitude) + _LUTOffset).xx;
                lutUV.x += (_Time.y * _LUTPanSpeed);
                float3 lutColor = SAMPLE_TEXTURE2D(_LUT, sampler_LUT, lutUV).rgb;

                // 4. Color over Lifetime 적용
                // _ParticleColorLUT가 0이면 파티클 색상 그대로, 1이면 LUT 색상을 섞음
                float3 baseRGB = lerp(IN.color.rgb, IN.color.rgb * lutColor, _ParticleColorLUT);
                
                // IN.uv2.z (Custom Data) 에러 방지를 위해 값이 없으면 1.0 사용
                float particleEmissionMult = max(IN.uv2.z, 1.0); 
                float3 finalRGB = baseRGB * _Emission * particleEmissionMult;

                // 5. Depth Fade (에러 수정 버전)
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneDepth = SampleSceneDepth(screenUV);
                float eyeSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                float eyeFragDepth = IN.screenPos.w; // 안정적인 Depth값 참조

                // IN.uv2.w (Custom Data) 에러 방지를 위해 값이 없으면 0.1 사용
                float fadeDist = max(IN.uv2.w, 0.1);
                float depthFade = saturate((eyeSceneDepth - eyeFragDepth) / fadeDist);

                // 6. Final Alpha
                float alpha = saturate(erosion * IN.color.a * depthFade);

                return half4(finalRGB, alpha);
            }
            ENDHLSL
        }
    }
}