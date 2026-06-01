Shader "Vefects/URP/SH_VFX_Slash"
{
    Properties
    {
        _Slash_Texture("Slash Texture", 2D) = "white" {}
        _Slash_Scale("Slash Scale", Float) = 1
        _Slash_Speed("Slash Speed", Float) = 1

        _Slash_Noise_Texture("Slash Noise Texture", 2D) = "white" {}
        _Slash_Noise_Scale("Slash Noise Scale", Vector) = (1,1,0,0)
        _Slash_Noise_Speed("Slash Noise Speed", Vector) = (-1,0.5,0,0)
        _Slash_Noise_Intensity("Slash Noise Intensity", Float) = 1

        _Emissive_Slash_Texture("Emissive Slash Texture", 2D) = "white" {}
        _Emissive_Slash_Scale("Emissive Slash Scale", Float) = 1
        _Emissive_Slash_Speed("Emissive Slash Speed", Float) = 1
        _Emissive_Intensity("Emissive Intensity", Float) = 3

        _Emissive_Dissolve_Texture("Emissive Dissolve Texture", 2D) = "white" {}
        _Emissive_Dissolve_Scale("Emissive Dissolve Scale", Vector) = (1,1,0,0)
        _Emissive_Dissolve_Speed("Emissive Dissolve Speed", Vector) = (1,1,0,0)

        _Distortion_Noise_Texture("Distortion Noise Texture", 2D) = "white" {}
        _Distortion_Noise_Scale("Distortion Noise Scale", Vector) = (1,1,0,0)
        _Distortion_Noise_Speed("Distortion Noise Speed", Vector) = (1,1,0,0)
        _Distortion_Intensity("Distortion Intensity", Float) = 1

        _Color_Noise_Texture("Color Noise Texture", 2D) = "white" {}
        _ColorNoise_Scale("Color Noise Scale", Vector) = (1,1,0,0)
        _ColorNoise_Speed("Color Noise Speed", Vector) = (1,1,0,0)
        _Color_Boost("Color Boost", Float) = 1

        _Mask("Mask", 2D) = "white" {}
        _Opacity_Boost("Opacity Boost", Float) = 1

        _Color_1("Color 01", Color) = (1,0,0.6,1)
        _Color_2("Color 02", Color) = (0.06,0,1,1)
        _Emissive_Color("Emissive Color", Color) = (1,0,0.6,1)

        _AdditiveLerp("Additive Lerp", Float) = 0

        _Cutout("Cutout", 2D) = "white" {}
        _CutoutErosion("Cutout Erosion", Float) = 0
        _CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 0.05
        _CutoutRotation("Cutout Rotation", Float) = 0
        _CutoutOffset("Cutout Offset", Vector) = (0,0,0,0)

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
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend [_Src] [_Dst]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 uv2 : TEXCOORD1;
            };

            TEXTURE2D(_Slash_Texture); SAMPLER(sampler_Slash_Texture);
            TEXTURE2D(_Slash_Noise_Texture); SAMPLER(sampler_Slash_Noise_Texture);
            TEXTURE2D(_Emissive_Slash_Texture); SAMPLER(sampler_Emissive_Slash_Texture);
            TEXTURE2D(_Emissive_Dissolve_Texture); SAMPLER(sampler_Emissive_Dissolve_Texture);
            TEXTURE2D(_Distortion_Noise_Texture); SAMPLER(sampler_Distortion_Noise_Texture);
            TEXTURE2D(_Color_Noise_Texture); SAMPLER(sampler_Color_Noise_Texture);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            TEXTURE2D(_Cutout); SAMPLER(sampler_Cutout);

            CBUFFER_START(UnityPerMaterial)
            float _Slash_Scale;
            float _Slash_Speed;

            float2 _Slash_Noise_Scale;
            float2 _Slash_Noise_Speed;
            float _Slash_Noise_Intensity;

            float _Emissive_Slash_Scale;
            float _Emissive_Slash_Speed;
            float _Emissive_Intensity;

            float2 _Emissive_Dissolve_Scale;
            float2 _Emissive_Dissolve_Speed;

            float2 _Distortion_Noise_Scale;
            float2 _Distortion_Noise_Speed;
            float _Distortion_Intensity;

            float2 _ColorNoise_Scale;
            float2 _ColorNoise_Speed;
            float _Color_Boost;

            float _Opacity_Boost;

            float4 _Color_1;
            float4 _Color_2;
            float4 _Emissive_Color;

            float _AdditiveLerp;

            float _CutoutErosion;
            float _CutoutErosionSmoothness;
            float2 _CutoutOffset;
            float _CutoutRotation;
            CBUFFER_END

            float2 RotateUV(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                uv -= 0.5;
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                return uv + 0.5;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.uv2 = v.uv2;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;

                // Color noise
                float2 colUV = i.uv * _ColorNoise_Scale + t * _ColorNoise_Speed;
                float3 baseColor = lerp(_Color_1.rgb, _Color_2.rgb,
                    SAMPLE_TEXTURE2D(_Color_Noise_Texture, sampler_Color_Noise_Texture, colUV).r);

                // Distortion
                float2 distUV = i.uv * _Distortion_Noise_Scale + t * _Distortion_Noise_Speed;
                float distortion =
                    SAMPLE_TEXTURE2D(_Distortion_Noise_Texture, sampler_Distortion_Noise_Texture, distUV).r
                    * 0.1 * _Distortion_Intensity;

                // Slash
                float2 slashUV = float2(i.uv.x * _Slash_Scale, i.uv.y)
                    + float2(t * _Slash_Speed, 0);

                float slash =
                    SAMPLE_TEXTURE2D(_Slash_Texture, sampler_Slash_Texture, slashUV + distortion).r;

                float noise =
                    SAMPLE_TEXTURE2D(_Slash_Noise_Texture, sampler_Slash_Noise_Texture,
                    i.uv * _Slash_Noise_Scale + t * _Slash_Noise_Speed).g;

                float mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv).r;

                float intensity = saturate((slash * _Slash_Noise_Intensity + noise)) * mask;

                // Emissive
                float2 emissiveUV = float2(i.uv.x * _Emissive_Slash_Scale, i.uv.y)
                    + float2(t * _Emissive_Slash_Speed, 0);

                float2 dissolveUV = i.uv * _Emissive_Dissolve_Scale + t * _Emissive_Dissolve_Speed;

                float emissiveTex =
                    SAMPLE_TEXTURE2D(_Emissive_Slash_Texture, sampler_Emissive_Slash_Texture,
                    emissiveUV + distortion).g;

                float dissolve =
                    SAMPLE_TEXTURE2D(_Emissive_Dissolve_Texture, sampler_Emissive_Dissolve_Texture,
                    dissolveUV).r;

                float emissive = saturate(emissiveTex * dissolve);

                float3 emissiveColor =
                    baseColor * _Color_Boost * intensity +
                    (i.color.rgb * emissive * _Emissive_Color.rgb * _Emissive_Intensity);

                // Cutout
                float2 cuv = i.uv + _CutoutOffset;
                cuv = RotateUV(cuv, radians(_CutoutRotation));

                float cut = SAMPLE_TEXTURE2D(_Cutout, sampler_Cutout, cuv).g;
                float cutout = smoothstep(_CutoutErosion,
                    _CutoutErosion + _CutoutErosionSmoothness,
                    cut);

                float alpha = saturate(i.color.a * intensity * _Opacity_Boost * cutout);

                float3 finalCol = lerp(emissiveColor, saturate(emissiveColor * alpha), _AdditiveLerp);

                return float4(finalCol, alpha);
            }

            ENDHLSL
        }
    }
}