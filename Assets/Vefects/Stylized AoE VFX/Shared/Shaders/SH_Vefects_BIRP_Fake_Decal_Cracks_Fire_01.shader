Shader "Vefects/URP/Fake_Decal_Cracks_Fire_01"
{
    Properties
    {
        _Specular("Specular", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0

        _NoiseEmissive("Noise Emissive", 2D) = "white" {}
        _Emissive("Emissive", Float) = 1
        _EmissiveErosionSmoothness("Emissive Erosion Smoothness", Float) = 0.5
        _EmissivePanSpeed("Emissive Pan Speed", Float) = 0.3

        _NoiseSpots("Noise Spots", 2D) = "white" {}
        _EmissiveSpots("Emissive Spots", Float) = 1
        _EmissiveSpotsColor("Emissive Spots Color", Color) = (1,0.6235294,0,1)
        _SpotsPanSpeed("Spots Pan Speed", Float) = 0.3

        [Header(Decal)]
        _DecalTexture("Decal Texture", 2D) = "white" {}
        _DecalRotation("Decal Rotation", Float) = 0
        _DecalScaleFromCenter("Decal Scale From Center", Float) = 1
        _DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)

        _FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
        _FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
        _FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1

        _ErosionSmoothness("Erosion Smoothness", Float) = 0.25

        [Header(LUT)]
        _LUT("LUT", 2D) = "white" {}
        _LUTAmplitude("LUT Amplitude", Float) = 1
        _LUTOffset("LUT Offset", Float) = 0
        _LUTPanSpeed("LUT Pan Speed", Float) = 0

        [Header(Render)]
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

            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite Off

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
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;

                float4 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;

                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float4 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;

                float4 color : COLOR;

                float4 screenPos : TEXCOORD2;

                float3 positionWS : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
            };

            TEXTURE2D(_DecalTexture);
            SAMPLER(sampler_DecalTexture);

            TEXTURE2D(_NoiseEmissive);
            SAMPLER(sampler_NoiseEmissive);

            TEXTURE2D(_NoiseSpots);
            SAMPLER(sampler_NoiseSpots);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float _Specular;
            float _Smoothness;

            float _Emissive;
            float _EmissiveErosionSmoothness;
            float _EmissivePanSpeed;

            float _EmissiveSpots;
            float4 _EmissiveSpotsColor;
            float _SpotsPanSpeed;

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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;

                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2;

                OUT.color = IN.color;

                OUT.screenPos = ComputeScreenPos(OUT.positionCS);

                return OUT;
            }

            float2 RotateUV(float2 uv, float rotation)
            {
                float2 center = float2(0.5, 0.5);

                float angle = radians(rotation);

                float s = sin(angle);
                float c = cos(angle);

                uv -= center;

                float2 rotated;
                rotated.x = uv.x * c - uv.y * s;
                rotated.y = uv.x * s + uv.y * c;

                rotated += center;

                return rotated;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 decalUV =
                    ((IN.uv.xy - 0.5) /
                    (_DecalScaleFromCenterNonUniform * _DecalScaleFromCenter))
                    + 0.5;

                decalUV = RotateUV(decalUV, _DecalRotation + IN.uv2.x);

                float4 decalTex = SAMPLE_TEXTURE2D(_DecalTexture, sampler_DecalTexture, decalUV);

                float emissiveMask = decalTex.r;
                float crackMask = decalTex.g;
                float burnMask = decalTex.b;

                //--------------------------------------------------
                // LUT COLOR
                //--------------------------------------------------

                float lutMult = IN.uv2.y;

                float2 lutUV =
                    float2(
                        crackMask * (_LUTAmplitude + lutMult) + _LUTOffset,
                        crackMask * (_LUTAmplitude + lutMult) + _LUTOffset
                    );

                lutUV += _Time.y * _LUTPanSpeed;

                float3 baseColor =
                    SAMPLE_TEXTURE2D(_LUT, sampler_LUT, lutUV).rgb;

                //--------------------------------------------------
                // EMISSIVE
                //--------------------------------------------------

                float timeEmi = _Time.y * _EmissivePanSpeed;

                float2 emiUV1 =
                    decalUV * 0.3 +
                    float2(0, -0.25) * timeEmi;

                float2 emiUV2 =
                    decalUV +
                    float2(0, 0.05) * timeEmi;

                float emiNoise1 =
                    SAMPLE_TEXTURE2D(_NoiseEmissive, sampler_NoiseEmissive, emiUV1).g;

                float emiNoise2 =
                    SAMPLE_TEXTURE2D(_NoiseEmissive, sampler_NoiseEmissive, emiUV2).g;

                float erosEmi = IN.uv2.w;

                float emissiveErosion =
                    smoothstep(
                        erosEmi,
                        erosEmi + _EmissiveErosionSmoothness,
                        emissiveMask
                    );

                float emissive =
                    saturate(emiNoise1 * emiNoise2) *
                    emissiveErosion *
                    _Emissive *
                    IN.uv.z;

                float3 emissiveColor =
                    emissive *
                    IN.color.rgb;

                //--------------------------------------------------
                // SPOTS
                //--------------------------------------------------

                float2 spotsBaseUV = decalUV * 3.0;

                float spotsTime = _Time.y * _SpotsPanSpeed;

                float2 spotsUV1 =
                    spotsBaseUV +
                    float2(0, -0.2) * spotsTime;

                float2 spotsUV2 =
                    spotsBaseUV +
                    float2(0.05, 0.1) * spotsTime;

                float spotsNoise1 =
                    SAMPLE_TEXTURE2D(_NoiseSpots, sampler_NoiseSpots, spotsUV1).g;

                float spotsNoise2 =
                    SAMPLE_TEXTURE2D(_NoiseSpots, sampler_NoiseSpots, spotsUV2).g;

                float spots =
                    burnMask *
                    saturate(pow(abs(spotsNoise1), 2.0) * pow(abs(spotsNoise2), 2.0) * 2.0);

                spots = saturate(pow(abs(spots), 1.5) * 10.0);

                float3 spotsEmission =
                    IN.uv2.z *
                    _EmissiveSpots *
                    _EmissiveSpotsColor.rgb *
                    spots;

                //--------------------------------------------------
                // FINAL EMISSION
                //--------------------------------------------------

                float3 finalEmission =
                    max(emissiveColor, spotsEmission);

                //--------------------------------------------------
                // ALPHA
                //--------------------------------------------------

                float erosion =
                    smoothstep(
                        IN.uv.w,
                        IN.uv.w + _ErosionSmoothness,
                        crackMask
                    );

                //--------------------------------------------------
                // DEPTH FADE
                //--------------------------------------------------

                float2 screenUV =
                    IN.screenPos.xy / IN.screenPos.w;

                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(screenUV);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif

                float sceneEyeDepth =
                    LinearEyeDepth(depth, _ZBufferParams);

                float thisEyeDepth =
                    LinearEyeDepth(IN.positionCS.z, _ZBufferParams);

                float depthFade =
                    saturate(
                        (sceneEyeDepth - thisEyeDepth)
                        / _FakeDecalDepthFade
                    );

                float depthErosion =
                    smoothstep(
                        _FakeDecalDepthFadeErosion,
                        _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness,
                        depthFade
                    );

                float alpha =
                    saturate(
                        erosion *
                        IN.color.a *
                        (1.0 - depthErosion)
                    );

                //--------------------------------------------------
                // LIGHTING
                //--------------------------------------------------

                Light mainLight = GetMainLight();

                float3 normalWS = normalize(IN.normalWS);

                float NdotL =
                    saturate(dot(normalWS, mainLight.direction));

                float3 lighting =
                    baseColor *
                    (0.2 + NdotL * mainLight.color);

                float3 finalColor =
                    lighting + finalEmission;

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}