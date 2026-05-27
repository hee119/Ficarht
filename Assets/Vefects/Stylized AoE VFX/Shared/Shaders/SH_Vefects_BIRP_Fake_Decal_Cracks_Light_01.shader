Shader "Vefects/SH_Vefects_URP_Fake_Decal_Cracks_Light_01"
{
    Properties
    {
        _Specular("Specular", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0

        _NoiseEmissive("Noise Emissive", 2D) = "white" {}
        _Emissive("Emissive", Float) = 1
        _EmissiveErosionSmoothness("Emissive Erosion Smoothness", Float) = 0.5
        _EmissivePanSpeed("Emissive Pan Speed", Float) = 0.3

        [Space(33)][Header(Decal)][Space(13)]
        _DecalTexture("Decal Texture", 2D) = "white" {}
        _DecalRotation("Decal Rotation", Float) = 0
        _DecalScaleFromCenter("Decal Scale From Center", Float) = 1
        _DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)

        [Space(33)][Header(Fake Decal)][Space(13)]
        _FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
        _FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
        _FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
        _ErosionSmoothness("Erosion Smoothness", Float) = 0.5

        [Space(33)][Header(LUT)][Space(13)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0

        [Space(33)][Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
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
            Cull [_Cull]
            ZWrite Off
            ZTest [_ZTest]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv         : TEXCOORD0;
                float4 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;

                float4 uv         : TEXCOORD0;
                float4 uv2        : TEXCOORD1;

                float4 screenPos  : TEXCOORD2;

                float3 positionWS : TEXCOORD3;
                float3 normalWS   : TEXCOORD4;
            };

            TEXTURE2D(_DecalTexture);
            SAMPLER(sampler_DecalTexture);

            TEXTURE2D(_NoiseEmissive);
            SAMPLER(sampler_NoiseEmissive);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Specular;
            float _Smoothness;

            float _Emissive;
            float _EmissivePanSpeed;
            float _EmissiveErosionSmoothness;

            float _DecalRotation;
            float _DecalScaleFromCenter;

            float2 _DecalScaleFromCenterNonUniform;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;

            float _FakeDecalDepthFade;
            float _FakeDecalDepthFadeErosion;
            float _FakeDecalDepthFadeErosionSmoothness;

            float _ErosionSmoothness;

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = normalInputs.normalWS;

                o.uv = v.uv;
                o.uv2 = v.uv2;

                o.color = v.color;

                o.screenPos = ComputeScreenPos(o.positionCS);

                return o;
            }

            float2 RotateUV(float2 uv, float angle)
            {
                float2 center = float2(0.5, 0.5);

                uv -= center;

                float s = sin(angle);
                float c = cos(angle);

                float2x2 rot = float2x2(c, -s, s, c);

                uv = mul(rot, uv);

                uv += center;

                return uv;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 decalUV = i.uv.xy;

                float randomRotate = i.uv2.x;

                decalUV = ((decalUV - 0.5) /
                          (_DecalScaleFromCenterNonUniform * _DecalScaleFromCenter))
                          + 0.5;

                float angle =
                    radians(_DecalRotation + randomRotate);

                decalUV = RotateUV(decalUV, angle);

                float4 decalTex =
                    SAMPLE_TEXTURE2D(
                        _DecalTexture,
                        sampler_DecalTexture,
                        decalUV);

                float crackTexture = decalTex.r;
                float gradientTexture = decalTex.g;
                float emissiveTexture = decalTex.b;
                float alphaTex = decalTex.a;

                // LUT COLOR
                float LUTMult = i.uv2.y;

                float2 lutUV =
                    ((crackTexture * (_LUTAmplitude + LUTMult))
                    + _LUTOffset).xx;

                lutUV += _Time.y * _LUTPanSpeed;

                float3 albedo =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        lutUV).rgb;

                // EMISSIVE
                float emissiveMask =
                    smoothstep(
                        i.uv2.w,
                        i.uv2.w + _EmissiveErosionSmoothness,
                        emissiveTexture);

                float emissiveTime = _Time.y * _EmissivePanSpeed;

                float2 noiseUV1 =
                    decalUV * 0.3 +
                    emissiveTime * float2(0, -0.25);

                float2 noiseUV2 =
                    decalUV +
                    emissiveTime * float2(0, 0.05);

                float noise1 =
                    SAMPLE_TEXTURE2D(
                        _NoiseEmissive,
                        sampler_NoiseEmissive,
                        noiseUV1).g;

                float noise2 =
                    SAMPLE_TEXTURE2D(
                        _NoiseEmissive,
                        sampler_NoiseEmissive,
                        noiseUV2).g;

                float emissiveNoise =
                    saturate(noise1 * noise2);

                float3 emission =
                    emissiveNoise *
                    emissiveMask *
                    i.color.rgb *
                    (_Emissive * i.uv.z);

                // ALPHA
                float erosion = i.uv.w;

                float alphaMask =
                    smoothstep(
                        erosion,
                        erosion + _ErosionSmoothness,
                        alphaTex);

                float gradientMask =
                    smoothstep(
                        erosion,
                        erosion + _ErosionSmoothness,
                        gradientTexture);

                // DEPTH FADE
                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                #if UNITY_REVERSED_Z
                    float sceneDepth =
                        SampleSceneDepth(screenUV);
                #else
                    float sceneDepth =
                        lerp(UNITY_NEAR_CLIP_VALUE, 1,
                        SampleSceneDepth(screenUV));
                #endif

                float sceneEyeDepth =
                    LinearEyeDepth(
                        sceneDepth,
                        _ZBufferParams);

                float objectEyeDepth =
                    LinearEyeDepth(
                        i.positionCS.z,
                        _ZBufferParams);

                float depthFade =
                    saturate(
                        (sceneEyeDepth - objectEyeDepth)
                        / _FakeDecalDepthFade);

                float depthErosion =
                    smoothstep(
                        _FakeDecalDepthFadeErosion,
                        _FakeDecalDepthFadeErosion
                        + _FakeDecalDepthFadeErosionSmoothness,
                        depthFade);

                float alpha =
                    saturate(
                        alphaMask *
                        gradientMask *
                        i.color.a *
                        (1.0 - depthErosion));

                // SIMPLE LIGHTING
                Light mainLight = GetMainLight();

                float3 normalWS = normalize(i.normalWS);

                float NdotL =
                    saturate(dot(normalWS, mainLight.direction));

                float3 lighting =
                    albedo * (0.25 + NdotL);

                lighting += emission;

                return half4(lighting, alpha);
            }

            ENDHLSL
        }
    }
}