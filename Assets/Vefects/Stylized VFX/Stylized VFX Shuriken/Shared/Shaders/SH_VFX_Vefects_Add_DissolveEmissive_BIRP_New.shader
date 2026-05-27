Shader "Vefects/SH_VFX_Vefects_Add_DissolveEmissive_URP_New"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.28,0.15,0.12,1)
        _Texture("Texture", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}

        _NoisePower("Noise Power", Float) = 1
        _NoiseScale("Noise Scale", Vector) = (1,1,0,0)
        _NoiseSpeed("Noise Speed", Vector) = (0,0.2,0,0)

        [Space(20)]
        _Cull("Cull", Float) = 2
        _Src("Src", Float) = 1
        _Dst("Dst", Float) = 10
        _ZWrite("ZWrite", Float) = 0
        _ZTest("ZTest", Float) = 4
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
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_Src] [_Dst]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float4 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float4 uv2         : TEXCOORD1;
            };

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);

            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            float4 _Texture_ST;

            float3 _BaseColor;
            float _NoisePower;
            float2 _NoiseScale;
            float2 _NoiseSpeed;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _Texture);
                o.color = v.color;
                o.uv2 = v.uv2;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 noiseUV =
                    i.uv * _NoiseScale +
                    (_Time.y * _NoiseSpeed);

                float noise =
                    SAMPLE_TEXTURE2D(_Noise, sampler_Noise, noiseUV).r;

                noise = pow(noise, _NoisePower);

                float texR = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).r;
                float texG = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).g;
                float texB = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).b;

                float mask = saturate(noise * texR);

                float baseEmission = saturate(mask * 2.0);

                float3 baseColor = baseEmission * _BaseColor;

                float3 vertexGlow =
                    i.color.rgb *
                    (texB * i.uv2.w);

                float3 finalColor = baseColor + vertexGlow;

                float alphaMask =
                    i.color.a * saturate(mask + texG);

                return half4(finalColor, alphaMask);
            }

            ENDHLSL
        }
    }
}