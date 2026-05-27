Shader "Vefects/SH_VFX_Vefects_Add_Dissolve_URP_New"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}

        _Emissive_Intensity("Emissive Intensity", Float) = 1
        _NoisePower("Noise Power", Float) = 1
        _NoiseScale("Noise Scale", Vector) = (1,1,0,0)
        _NoiseSpeed("Noise Speed", Vector) = (0,0.2,0,0)

        [Space(20)]
        _Cull("Cull", Float) = 2
        _Src("Src", Float) = 1
        _Dst("Dst", Float) = 1
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
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float2 uv2         : TEXCOORD1;
            };

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);

            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            float4 _Texture_ST;
            float4 _Noise_ST;

            float _Emissive_Intensity;
            float _NoisePower;
            float2 _NoiseScale;
            float2 _NoiseSpeed;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _Texture);
                o.uv2 = v.uv2;
                o.color = v.color;
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

                float texMask =
                    SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv).r;

                float baseMask = saturate(noise * texMask);

                float uv2Mask = (1.0 - i.uv2.y); 
                float dissolve = saturate(baseMask + uv2Mask);

                float intensity = dissolve * i.uv2.x;

                float3 color =
                    i.color.rgb *
                    i.color.a *
                    intensity *
                    _Emissive_Intensity;

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
}