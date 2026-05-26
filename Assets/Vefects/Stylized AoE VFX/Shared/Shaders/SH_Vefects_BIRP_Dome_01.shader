Shader "Vefects/URP/Dome_01"
{
    Properties
    {
        [Header(Main)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _MainTextureSelector("Main Texture Selector", Vector) = (0,1,0,0)
        _MainTextureUVScale("Main Texture UV Scale", Vector) = (1,1,0,0)
        _MainTextureUVPanSpeed("Main Texture UV Pan Speed", Vector) = (0.01,0.3,0,0)

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0
        _ParticleColorLUT("Particle Color / LUT", Range(0,1)) = 0

        [Header(Distortion)]
        _DistortionTexture("Distortion Texture", 2D) = "white" {}
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
        _DistortionUVPanSpeed("Distortion UV Pan Speed", Vector) = (-0.02,0.5,0,0)
        _DistortionAmount("Distortion Amount", Float) = 0.3

        [Header(Cutout)]
        _CutoutMask("Cutout Mask", 2D) = "white" {}
        _CutoutMaskPower("Cutout Mask Power", Float) = 1
        _CutoutMaskMultiply("Cutout Mask Multiply", Float) = 1

        [Header(Erosion)]
        _ErosionSmoothness("Erosion Smoothness", Float) = 1
        _LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1

        [Header(Output)]
        _Emission("Emission", Float) = 1
        _IsAdd("Is Add", Range(0,1)) = 0

        [Header(Depth Fade)]
        _DepthFadeDistance("Depth Fade Distance", Float) = 1

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Dst", Float) = 10
        [Toggle] _ZWrite("ZWrite", Float) = 0
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

            Blend [_Src] [_Dst]
            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 color      : COLOR;
                float4 screenPos  : TEXCOORD2;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_CutoutMask);
            SAMPLER(sampler_CutoutMask);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float4 _MainTextureSelector;

            float2 _MainTextureUVScale;
            float2 _MainTextureUVPanSpeed;

            float2 _DistortionUVScale;
            float2 _DistortionUVPanSpeed;
            float _DistortionAmount;

            float _CutoutMaskPower;
            float _CutoutMaskMultiply;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;

            float _ParticleColorLUT;

            float _ErosionSmoothness;
            float _LUTErosionSmoothness;

            float _Emission;
            float _IsAdd;

            float _DepthFadeDistance;

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = pos.positionCS;
                o.screenPos = ComputeScreenPos(pos.positionCS);

                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 mainUV =
                    i.uv * _MainTextureUVScale +
                    (_Time.y * _MainTextureUVPanSpeed);

                float2 distUV =
                    i.uv * _DistortionUVScale +
                    (_Time.y * _DistortionUVPanSpeed);

                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTexture,
                        sampler_DistortionTexture,
                        distUV
                    ).rg;

                distortion *= _DistortionAmount;

                float4 mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        mainUV + distortion
                    );

                float mainMask = dot(mainTex, _MainTextureSelector);
                mainMask = saturate(mainMask);

                float cutout =
                    SAMPLE_TEXTURE2D(
                        _CutoutMask,
                        sampler_CutoutMask,
                        i.uv
                    ).g;

                cutout = pow(cutout, _CutoutMaskPower);
                cutout = saturate(cutout) * _CutoutMaskMultiply;
                cutout = saturate(cutout);

                float combined = saturate(mainMask * cutout);

                float erosion =
                    smoothstep(
                        i.uv2.x,
                        i.uv2.x + _ErosionSmoothness,
                        combined
                    );

                erosion = saturate(erosion);

                float lutErosion =
                    smoothstep(
                        i.uv2.x,
                        i.uv2.x + _LUTErosionSmoothness,
                        combined
                    );

                lutErosion = saturate(lutErosion);

                float2 lutUV;
                lutUV.x = lutErosion * _LUTAmplitude + _LUTOffset;
                lutUV.y = _Time.y * _LUTPanSpeed;

                float3 lutColor =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        lutUV
                    ).rgb;

                float4 baseColor = i.color;

                float4 lutBlend =
                    i.color * float4(lutColor, 1);

                float4 finalColor =
                    lerp(
                        baseColor,
                        lutBlend,
                        _ParticleColorLUT
                    );

                float4 addColor =
                    finalColor * erosion;

                finalColor =
                    lerp(
                        finalColor,
                        addColor,
                        _IsAdd
                    );

                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                float sceneDepth =
                    SampleSceneDepth(screenUV);

                float sceneEye =
                    LinearEyeDepth(
                        sceneDepth,
                        _ZBufferParams
                    );

                float particleEye =
                    LinearEyeDepth(
                        i.screenPos.z / i.screenPos.w,
                        _ZBufferParams
                    );

                float depthFade =
                    saturate(
                        (sceneEye - particleEye)
                        / _DepthFadeDistance
                    );

                float alpha =
                    erosion *
                    i.color.a *
                    depthFade;

                float3 emission =
                    finalColor.rgb *
                    (_Emission * i.uv.y);

                return half4(emission, alpha);
            }

            ENDHLSL
        }
    }
}