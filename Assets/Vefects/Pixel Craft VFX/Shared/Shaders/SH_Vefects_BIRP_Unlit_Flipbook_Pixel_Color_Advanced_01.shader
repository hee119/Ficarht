Shader "Custom/URP/VFX_Flipbook_Advanced"
{
    Properties
    {
        [MainTexture]_MainTex("Main Texture", 2D) = "white" {}

        _Emissive("Emissive", Float) = 1
        _HueShift("Hue Shift", Range(0,1)) = 0

        _FlipbookX("Flipbook X", Float) = 4
        _FlipbookY("Flipbook Y", Float) = 4
        _Frame("Frame", Float) = 0

        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Range(0,0.1)) = 0.02

        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _Dissolve("Dissolve", Range(0,1)) = 0
        _DissolveSoftness("Dissolve Softness", Range(0.001,1)) = 0.1

        [Toggle]_Pixelate("Pixelate", Float) = 0
        _PixelCount("Pixel Count", Float) = 64
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
            Cull Off
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _PIXELATE_ON

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);

            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);

            CBUFFER_START(UnityPerMaterial)

            float4 _MainTex_ST;

            float _Emissive;
            float _HueShift;

            float _FlipbookX;
            float _FlipbookY;
            float _Frame;

            float _DistortionStrength;

            float _Dissolve;
            float _DissolveSoftness;

            float _Pixelate;
            float _PixelCount;

            CBUFFER_END

            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz),
                                float4(c.gb, K.xy),
                                step(c.b, c.g));

                float4 q = lerp(float4(p.xyw, c.r),
                                float4(c.r, p.yzx),
                                step(p.x, c.r));

                float d = q.x - min(q.w, q.y);
                float e = 1e-10;

                return float3(
                    abs(q.z + (q.w - q.y) / (6.0 * d + e)),
                    d / (q.x + e),
                    q.x
                );
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);

                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);

                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            float2 FlipbookUV(float2 uv, float xCount, float yCount, float frame)
            {
                float total = xCount * yCount;

                frame = floor(frame % total);

                float2 tile;

                tile.x = fmod(frame, xCount);
                tile.y = floor(frame / xCount);

                uv /= float2(xCount, yCount);

                uv.x += tile.x / xCount;
                uv.y = 1.0 - uv.y - (tile.y / yCount);

                return uv;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // DISTORTION
                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTex,
                        sampler_DistortionTex,
                        uv
                    ).rg;

                distortion = (distortion - 0.5) * 2.0;

                uv += distortion * _DistortionStrength;

                // PIXELATE
                #ifdef _PIXELATE_ON

                uv = floor(uv * _PixelCount) / _PixelCount;

                #endif

                // FLIPBOOK
                uv = FlipbookUV(
                    uv,
                    _FlipbookX,
                    _FlipbookY,
                    _Frame
                );

                // MAIN TEX
                half4 col =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        uv
                    );

                // HUE SHIFT
                float3 hsv = RGBToHSV(col.rgb);

                hsv.x += _HueShift;

                col.rgb = HSVToRGB(hsv);

                // VERTEX COLOR
                col *= IN.color;

                // DISSOLVE
                float dissolve =
                    SAMPLE_TEXTURE2D(
                        _DissolveTex,
                        sampler_DissolveTex,
                        IN.uv
                    ).r;

                float alpha =
                    smoothstep(
                        _Dissolve,
                        _Dissolve + _DissolveSoftness,
                        dissolve
                    );

                col.a *= alpha;

                // EMISSIVE
                col.rgb *= _Emissive;

                return col;
            }

            ENDHLSL
        }
    }
}