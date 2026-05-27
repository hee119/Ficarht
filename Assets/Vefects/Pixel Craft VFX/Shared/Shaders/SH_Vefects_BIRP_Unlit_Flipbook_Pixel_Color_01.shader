Shader "Universal Render Pipeline/Vefects/FlipbookPixelColor"
{
    Properties
    {
        _OverallTint("Overall Tint", Color) = (1,1,1,1)

        _HueShift("Hue Shift", Range(0,1)) = 0
        _SaturationMultiply("Saturation Multiply", Float) = 1
        _Emissive("Emissive", Float) = 1

        [Header(Main Texture)]
        _MainTexture("Main Texture", 2D) = "white" {}

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull("Cull", Float) = 2
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

            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite Off

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
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            float4 _MainTexture_ST;

            float4 _OverallTint;

            float _HueShift;
            float _SaturationMultiply;
            float _Emissive;

            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);

                float4 p =
                    lerp(
                        float4(c.bg, K.wz),
                        float4(c.gb, K.xy),
                        step(c.b, c.g)
                    );

                float4 q =
                    lerp(
                        float4(p.xyw, c.r),
                        float4(c.r, p.yzx),
                        step(p.x, c.r)
                    );

                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;

                return float3
                (
                    abs(q.z + (q.w - q.y) / (6.0 * d + e)),
                    d / (q.x + e),
                    q.x
                );
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);

                float3 p =
                    abs(frac(c.xxx + K.xyz) * 6.0 - K.www);

                return c.z *
                    lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;

                o.positionCS =
                    TransformObjectToHClip(v.positionOS.xyz);

                o.uv =
                    TRANSFORM_TEX(v.uv, _MainTexture);

                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _MainTexture,
                        sampler_MainTexture,
                        i.uv
                    );

                float3 hsv = RGBToHSV(tex.rgb);

                hsv.x += _HueShift;
                hsv.y *= _SaturationMultiply;

                float3 shiftedRGB = HSVToRGB(hsv);

                float4 finalColor =
                    i.color *
                    _OverallTint *
                    float4(shiftedRGB, 1);

                finalColor *= _Emissive;

                float alpha =
                    tex.a *
                    i.color.a;

                return float4(finalColor.rgb, alpha);
            }

            ENDHLSL
        }
    }
}