Shader "Custom/Additive_Base_URP"
{
    Properties
    {
        _MainTexture ("MainTexture", 2D) = "white" {}
        _Glow_Intensity ("Glow_Intensity", Float) = 1
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

            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTexture_ST;
                float _Glow_Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;

                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTexture);
                o.color = v.color;

                o.fogFactor = ComputeFogFactor(o.positionHCS.z);

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, i.uv);

                half3 emissive =
                    tex.rgb *
                    i.color.rgb *
                    i.color.a *
                    _Glow_Intensity;

                half4 col = half4(emissive, 1);

                // URP Fog 적용
                col.rgb = MixFog(col.rgb, i.fogFactor);

                return col;
            }

            ENDHLSL
        }
    }
}