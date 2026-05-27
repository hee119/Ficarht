Shader "Vefects/URP_Fake_Decal_Cracks_01"
{
    Properties
    {
        _Emissive("Emissive", Float) = 1

        [Header(Decal)]
        _DecalTexture("Decal Texture", 2D) = "white" {}
        _DecalRotation("Decal Rotation", Float) = 0
        _DecalScaleFromCenter("Decal Scale From Center", Float) = 1
        _DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)

        [Header(Fake Decal)]
        _FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
        _FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
        _FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
        _ErosionSmoothness("Erosion Smoothness", Float) = 0.25

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0
        _LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 0.5

        [Header(Render)]
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;

                float4 uv : TEXCOORD1;
                float4 uv2 : TEXCOORD2;

                float4 color : COLOR;
            };

            TEXTURE2D(_DecalTexture);
            SAMPLER(sampler_DecalTexture);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Emissive;

            float _DecalRotation;
            float _DecalScaleFromCenter;
            float2 _DecalScaleFromCenterNonUniform;

            float _FakeDecalDepthFade;
            float _FakeDecalDepthFadeErosion;
            float _FakeDecalDepthFadeErosionSmoothness;

            float _ErosionSmoothness;

            float _LUTAmplitude;
            float _LUTOffset;
            float _LUTPanSpeed;
            float _LUTErosionSmoothness;

            float2 RotateUV(float2 uv, float2 center, float rotation)
            {
                float rad = radians(rotation);

                float s = sin(rad);
                float c = cos(rad);

                uv -= center;

                float2x2 m = float2x2(c, -s, s, c);

                uv = mul(m, uv);

                uv += center;

                return uv;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInput = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = posInput.positionCS;
                o.screenPos = ComputeScreenPos(posInput.positionCS);

                o.uv = v.uv;
                o.uv2 = v.uv2;

                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float eros = i.uv.w;
                float em = i.uv.z;

                float randomRotate = i.uv2.x;
                float LUTMult = i.uv2.y;

                float2 uv = i.uv.xy;

                uv -= 0.5;
                uv /= (_DecalScaleFromCenterNonUniform * _DecalScaleFromCenter);
                uv += 0.5;

                uv = RotateUV(uv, float2(0.5, 0.5), _DecalRotation + randomRotate);

                float4 decalTex = SAMPLE_TEXTURE2D(_DecalTexture, sampler_DecalTexture, uv);

                float lutSmooth = smoothstep(
                    eros,
                    eros + _LUTErosionSmoothness,
                    decalTex.g
                );

                float erosionSmooth = smoothstep(
                    eros,
                    eros + _ErosionSmoothness,
                    decalTex.g
                );

                float erosionMask = saturate(erosionSmooth);

                float2 lutUV =
                    ((saturate(lutSmooth) * (_LUTAmplitude + LUTMult)) + _LUTOffset).xx;

                lutUV += _Time.y * _LUTPanSpeed;

                float3 lutColor =
                    SAMPLE_TEXTURE2D(_LUT, sampler_LUT, lutUV).rgb;

                float3 finalColor =
                    lerp(i.color.rgb, lutColor, erosionMask);

                finalColor *= (_Emissive * em);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                #if UNITY_REVERSED_Z
                    float rawDepth = SampleSceneDepth(screenUV);
                #else
                    float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif

                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float objDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);

                float depthFade =
                    saturate((sceneDepth - objDepth) / _FakeDecalDepthFade);

                float fadeMask = smoothstep(
                    _FakeDecalDepthFadeErosion,
                    _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness,
                    depthFade
                );

                float erosSat = saturate(eros);

                float alphaPart =
                    saturate(
                        saturate((decalTex.r - erosSat) / (1.0 - erosSat))
                        * i.color.a
                    );

                float alpha =
                    saturate(
                        max(erosionMask, alphaPart)
                        * (1.0 - saturate(fadeMask))
                    );

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}