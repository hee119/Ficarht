Shader "Vefects/SH_VFX_Vefects_Add_DissolveTex_URP"
{
    Properties
    {
        _Texture ("Texture", 2D) = "white" {}

        _Cull ("Cull", Float) = 2
        _Src ("Src", Float) = 1
        _Dst ("Dst", Float) = 1
        _ZWrite ("ZWrite", Float) = 0
        _ZTest ("ZTest", Float) = 4
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

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            float4 _Texture_ST;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 uv2         : TEXCOORD1;
                float4 color       : COLOR;
            };

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
                half4 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv);

                // === Amplify original logic reconstructed ===
                float dissolveMask = saturate(
                    (3.0 * (1.0 - i.uv2.z)) + (tex.r + tex.g)
                );

                float intensity = i.color.a * dissolveMask * i.uv2.w;

                half3 emission = i.color.rgb * intensity;

                return half4(emission, 1.0);
            }

            ENDHLSL
        }
    }
}