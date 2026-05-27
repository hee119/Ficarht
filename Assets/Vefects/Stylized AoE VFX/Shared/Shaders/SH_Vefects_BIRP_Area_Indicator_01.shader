Shader "Vefects/URP/AreaIndicator"
{
    Properties
    {
        [MainTexture] _Texture01("Texture 01", 2D) = "white" {}
        _Texture02("Texture 02", 2D) = "white" {}
        _DistortionTexture("Distortion", 2D) = "gray" {}
        _LUT("LUT", 2D) = "white" {}

        [HDR]_Color("Color", Color) = (1,1,1,1)

        _Emission("Emission", Float) = 1
        _DepthFade("Depth Fade", Float) = 1

        _DistortionAmount("Distortion Amount", Float) = 0.05

        _Texture01UVScale("Texture01 UV Scale", Vector) = (1,1,0,0)
        _Texture02UVScale("Texture02 UV Scale", Vector) = (1,1,0,0)
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)

        _Texture01UVPanSpeed("Texture01 Pan", Vector) = (0,0,0,0)
        _Texture02UVPanSpeed("Texture02 Pan", Vector) = (0,0,0,0)
        _DistortionUVPanSpeed("Distortion Pan", Vector) = (0,0,0,0)

        _Erosion("Erosion", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0.001,1)) = 0.1
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float4 color      : COLOR;
                float4 screenPos  : TEXCOORD2;
            };

            TEXTURE2D(_Texture01);
            SAMPLER(sampler_Texture01);

            TEXTURE2D(_Texture02);
            SAMPLER(sampler_Texture02);

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);

            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            float4 _Color;

            float _Emission;
            float _DepthFade;
            float _DistortionAmount;

            float4 _Texture01UVScale;
            float4 _Texture02UVScale;
            float4 _DistortionUVScale;

            float4 _Texture01UVPanSpeed;
            float4 _Texture02UVPanSpeed;
            float4 _DistortionUVPanSpeed;

            float _Erosion;
            float _Smoothness;

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = pos.positionCS;
                o.screenPos = ComputeScreenPos(pos.positionCS);

                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.color = v.color;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float time = _Time.y;

                //--------------------------------
                // DISTORTION
                //--------------------------------

                float2 distortionUV =
                    i.uv * _DistortionUVScale.xy +
                    time * _DistortionUVPanSpeed.xy;

                float2 distortion =
                    SAMPLE_TEXTURE2D(
                        _DistortionTexture,
                        sampler_DistortionTexture,
                        distortionUV
                    ).rg;

                distortion = (distortion * 2 - 1) * _DistortionAmount;

                //--------------------------------
                // TEXTURE 01
                //--------------------------------

                float2 uv1 =
                    i.uv * _Texture01UVScale.xy +
                    time * _Texture01UVPanSpeed.xy;

                float tex1 =
                    SAMPLE_TEXTURE2D(
                        _Texture01,
                        sampler_Texture01,
                        uv1
                    ).r;

                //--------------------------------
                // TEXTURE 02
                //--------------------------------

                float2 uv2 =
                    i.uv * _Texture02UVScale.xy +
                    time * _Texture02UVPanSpeed.xy +
                    distortion;

                float tex2 =
                    SAMPLE_TEXTURE2D(
                        _Texture02,
                        sampler_Texture02,
                        uv2
                    ).g;

                //--------------------------------
                // EROSION
                //--------------------------------

                float mask = tex1 * tex2;

                float erosion =
                    smoothstep(
                        _Erosion,
                        _Erosion + _Smoothness,
                        mask
                    );

                //--------------------------------
                // LUT
                //--------------------------------

                float2 lutUV = float2(erosion, 0.5);

                float3 lut =
                    SAMPLE_TEXTURE2D(
                        _LUT,
                        sampler_LUT,
                        lutUV
                    ).rgb;

                //--------------------------------
                // DEPTH FADE
                //--------------------------------

                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(screenUV);
                #else
                    real depth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1,
                        SampleSceneDepth(screenUV)
                    );
                #endif

                float sceneDepth =
                    LinearEyeDepth(
                        depth,
                        _ZBufferParams
                    );

                float thisDepth =
                    LinearEyeDepth(
                        i.screenPos.z / i.screenPos.w,
                        _ZBufferParams
                    );

                float depthFade =
                    saturate(
                        (sceneDepth - thisDepth)
                        / max(_DepthFade, 0.0001)
                    );

                //--------------------------------
                // FINAL
                //--------------------------------

                float3 finalColor =
                    lut *
                    i.color.rgb *
                    _Color.rgb *
                    _Emission;

                float alpha =
                    erosion *
                    i.color.a *
                    depthFade;

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}