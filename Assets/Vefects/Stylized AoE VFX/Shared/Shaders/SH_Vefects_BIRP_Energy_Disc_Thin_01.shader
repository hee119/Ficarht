Shader "Vefects/URP_Energy_Disc_Thin_01_Fixed"
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
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

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
                // 1. UV 및 랜덤 연산
                float2 mainUV = i.uv * _MainTextureUVScale + (_Time.y * _MainTextureUVSpeed);
                float particleRandom = i.uv2.y;
                mainUV += particleRandom;

                // 2. 텍스처 및 에로전 마스크
                float4 mainTex = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, mainUV);
                float mask = saturate(dot(mainTex, _MainTextureChannelSelector));
                
                // i.uv2.x (파티클의 Custom Data 혹은 속성)를 사용한 부드러운 침식 효과
                float erosion = saturate(smoothstep(i.uv2.x, i.uv2.x + _ErosionSmoothness, mask));

                // 3. Emission 연산 (Color over Lifetime 반영)
                // i.uv.z 에러 수정을 위해 i.uv.z를 제거하거나 1.0으로 대체했습니다.
                float3 emission = i.color.rgb * erosion * _Emission;

                // 4. Depth Fade (화면 경계 부드럽게)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                
                // Metal 환경 에러 방지를 위해 안정적인 방식으로 selfDepth 계산
                float selfEyeDepth = i.screenPos.w; 

                // i.uv.w 에러 수정을 위해 0.1(고정값) 또는 다른 파라미터로 대체
                float depthFade = saturate((sceneEyeDepth - selfEyeDepth) / 0.1);

                // 5. 최종 알파 (Color over Lifetime의 Alpha 반영)
                float alpha = saturate(erosion * i.color.a * depthFade);

                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}