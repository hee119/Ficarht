Shader "Vefects/SH_Vefects_URP_Unlit_Flipbook_Pixel_01"
{
    Properties
    {
        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}

        [HDR]_R("R", Color) = (1,0.97,0.58,1)
        [HDR]_G("G", Color) = (1,0.72,0.25,1)
        [HDR]_B("B", Color) = (0.59,0.25,0.09,1)
        [HDR]_Outline("Outline", Color) = (0.21,0.03,0.02,1)

        _FlatColor("Flat Color", Range(0,1)) = 0
        _Emissive("Emissive", Float) = 1

        [Header(Render)]
        _Cull("Cull", Float) = 2
        _Src("Src", Float) = 5
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

        Cull [_Cull]
        Blend [_Src] [_Dst]
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            Name "Forward"

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

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            CBUFFER_START(UnityPerMaterial)

            float4 _MainTexture_ST;

            float4 _R;
            float4 _G;
            float4 _B;
            float4 _Outline;

            float _FlatColor;
            float _Emissive;

            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;

                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTexture);
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        i.uv
                    );

                // RGB Channel Blend
                half4 col = _Outline;

                col = lerp(col, _B, tex.b);
                col = lerp(col, _G, tex.g);
                col = lerp(col, _R, tex.r);

                // Vertex Color
                half4 vcColor = i.color * col;

                // Flat Color
                col = lerp(vcColor, i.color, _FlatColor);

                // Emissive
                col.rgb *= _Emissive;

                // Alpha
                half alpha =
                    tex.a * i.color.a;

                return half4(col.rgb, alpha);
            }

            ENDHLSL
        }
    }
}