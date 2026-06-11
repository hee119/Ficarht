Shader "Vefects/URP/SH_VFX_Stylized_Dissolve_Fixed"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        _ColorTexture("Color Texture", 2D) = "white" {}
        _GradientShape("Gradient Shape", 2D) = "white" {}
        _GradientMap("Gradient Map", 2D) = "white" {}
        _DistortionMask("Distortion Mask", 2D) = "white" {}
        _DissolveMask("Dissolve Mask", 2D) = "white" {}

        _DistortionIntensity("Distortion Intensity", Float) = 0
        _DissolveMaskInvert("Invert", Float) = 0
        _DissolveOffset("Offset", Float) = 0

        _GradientMapDisplacement("Gradient Offset", Float) = 0
        _InvertGradient("Invert Gradient", Float) = 0

        _CoreColor("Core Color", Color) = (1,1,1,1)
        _CorePower("Core Power", Float) = 1
        _CoreIntensity("Core Intensity", Float) = 0
        _GlowIntensity("Glow", Float) = 1
        _EmissionIntensity("Emission", Float) = 1
        _AlphaBoldness("Alpha", Float) = 1

        _UseDepthFade("Depth Fade", Float) = 0
        _DepthFadeIntensity("Fade Intensity", Float) = 1

        _Cull("Cull", Float) = 2
        _Src("Src", Float) = 5
        _Dst("Dst", Float) = 10
        _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Blend [_Src] [_Dst]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_Texture); SAMPLER(sampler_Texture);
            TEXTURE2D(_ColorTexture); SAMPLER(sampler_ColorTexture);
            TEXTURE2D(_GradientMap); SAMPLER(sampler_GradientMap);
            TEXTURE2D(_DistortionMask); SAMPLER(sampler_DistortionMask);
            TEXTURE2D(_DissolveMask); SAMPLER(sampler_DissolveMask);

            CBUFFER_START(UnityPerMaterial)
            float _DistortionIntensity;
            float _DissolveMaskInvert;
            float _DissolveOffset;
            float _GradientMapDisplacement;
            float _InvertGradient;
            float4 _CoreColor;
            float _CorePower;
            float _CoreIntensity;
            float _GlowIntensity;
            float _EmissionIntensity;
            float _AlphaBoldness;
            float _UseDepthFade;
            float _DepthFadeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // 파티클 시스템에서 오는 색상 데이터
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color; // Vertex Color 전달
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                // Dissolve logic
                float dissolve = SAMPLE_TEXTURE2D(_DissolveMask, sampler_DissolveMask, uv + t * 0.1).r;
                dissolve = saturate(dissolve + uv.y + _DissolveOffset);
                if (_DissolveMaskInvert > 0.5) dissolve = 1 - dissolve;

                // Base texture & Alpha
                float tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv).r;
                float alpha = saturate(tex * dissolve);

                // Color logic
                float3 baseCol = SAMPLE_TEXTURE2D(_ColorTexture, sampler_ColorTexture, uv).rgb;
                
                // [수정 포인트] 파티클 시스템의 RGB 색상을 기본 컬러에 곱해줍니다.
                float3 finalCol = baseCol * i.color.rgb;

                // Core & Glow
                float core = pow(alpha, _CorePower) * _CoreIntensity;
                finalCol = lerp(finalCol, _CoreColor.rgb * i.color.rgb, saturate(core));

                float glow = alpha * _GlowIntensity;
                float emissionMask = saturate(core + glow);

                // 최종 발광 컬러에 파티클 컬러 반영
                float3 emission = finalCol * emissionMask * _EmissionIntensity;

                // Depth Fade
                if (_UseDepthFade > 0.5)
                {
                    float2 screenUV = i.screenPos.xy / i.screenPos.w;
                    float sceneZ = SampleSceneDepth(screenUV);
                    sceneZ = LinearEyeDepth(sceneZ, _ZBufferParams);
                    float selfZ = LinearEyeDepth(i.screenPos.z / i.screenPos.w, _ZBufferParams);
                    float fade = saturate((sceneZ - selfZ) / _DepthFadeIntensity);
                    emission *= fade;
                    alpha *= fade;
                }

                // [수정 포인트] 최종 출력 Alpha에도 파티클의 Alpha(i.color.a)를 정확히 곱함
                return float4(emission, alpha * _AlphaBoldness * i.color.a);
            }
            ENDHLSL
        }
    }
}