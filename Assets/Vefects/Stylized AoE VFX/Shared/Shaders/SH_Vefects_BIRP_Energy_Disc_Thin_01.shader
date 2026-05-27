Shader "Vefects/URP_Energy_Disc_Thin_01"
{
    Properties
    {
        _Emission("Emission", Float) = 1
        _ErosionSmoothness("Erosion Smoothness", Float) = 1

        [Space(20)]
        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}
        _MainTextureChannelSelector("Main Texture Channel Selector", Vector) = (0,1,0,0)
        _MainTextureUVScale("Main Texture UV Scale", Vector) = (2,1,0,0)
        _MainTextureUVSpeed("Main Texture UV Speed", Vector) = (0.1,0,0,0)

        [Space(20)]
        [Header(Render Settings)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Dst", Float) = 10

        [Toggle] _ZWrite("ZWrite", Float) = 0

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

            Tags { "LightMode"="UniversalForward" }

            Blend [_Src] [_Dst]
            Cull [_Cull]
            ZWrite [_ZWrite]
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
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;

                float2 uv : TEXCOORD1;
                float2 uv2 : TEXCOORD2;

                float4 color : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            CBUFFER_START(UnityPerMaterial)

                float _Emission;
                float _ErosionSmoothness;

                float4 _MainTexture_ST;
                float4 _MainTextureChannelSelector;
                float2 _MainTextureUVScale;
                float2 _MainTextureUVSpeed;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = posInputs.positionCS;
                o.screenPos = ComputeScreenPos(posInputs.positionCS);

                o.uv = TRANSFORM_TEX(v.uv, _MainTexture);
                o.uv2 = v.uv2;

                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv =
                    i.uv * _MainTextureUVScale +
                    (_Time.y * _MainTextureUVSpeed);

                float particleRandom = i.uv2.y;

                uv += particleRandom;

                float4 mainTex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        uv
                    );

                float mask =
                    dot(mainTex, _MainTextureChannelSelector);

                mask = saturate(mask);

                float erosion =
                    smoothstep(
                        i.uv2.x,
                        i.uv2.x + _ErosionSmoothness,
                        mask
                    );

                erosion = saturate(erosion);

                float3 emission =
                    i.color.rgb *
                    erosion *
                    (_Emission * i.uv.z);

                // Depth Fade
                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                #if UNITY_REVERSED_Z
                    float sceneDepth =
                        SampleSceneDepth(screenUV);
                #else
                    float sceneDepth =
                        lerp(
                            UNITY_NEAR_CLIP_VALUE,
                            1,
                            SampleSceneDepth(screenUV)
                        );
                #endif

                float sceneEyeDepth =
                    LinearEyeDepth(
                        sceneDepth,
                        _ZBufferParams
                    );

                float partEyeDepth =
                    LinearEyeDepth(
                        i.positionCS.z / i.positionCS.w,
                        _ZBufferParams
                    );

                float depthFade =
                    saturate(
                        (sceneEyeDepth - partEyeDepth)
                        / max(i.uv.w, 0.0001)
                    );

                float alpha =
                    saturate(
                        erosion *
                        i.color.a *
                        depthFade
                    );

                return half4(emission, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}