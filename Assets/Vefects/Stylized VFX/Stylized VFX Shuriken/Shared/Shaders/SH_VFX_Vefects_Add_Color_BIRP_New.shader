Shader "Vefects/SH_VFX_Vefects_Add_Color_URP_New"
{
    Properties
    {
        _Texture("Texture", 2D) = "white" {}
        _EmissiveIntensity("Emissive Intensity", Float) = 1

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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            float4 _Texture_ST;

            float _EmissiveIntensity;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _Texture);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.uv);

                float3 color =
                    tex.rgb *
                    i.color.rgb *
                    i.color.a *
                    _EmissiveIntensity;

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
}