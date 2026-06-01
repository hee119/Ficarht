Shader "Vefects/URP/SH_VFX_Vefects_Distortion_01"
{
    Properties
    {
        _CutoutTexture("Cutout Texture", 2D) = "white" {}
        _CutoutMaskSelector("Cutout Mask Selector", Vector) = (0,1,0,0)

        _DistortionNoise("Distortion Noise", 2D) = "white" {}
        _DistUVS("Dist UV S", Vector) = (1,1,0,0)
        _DistUVP("Dist UV P", Vector) = (0,0,0,0)

        _DistortionDist("Distortion Dist", 2D) = "white" {}
        _DistDistUVS("Dist Dist UV S", Vector) = (1,1,0,0)
        _DistDistUVP("Dist Dist UV P", Vector) = (0,0,0,0)

        _DistortionLerp("Distortion Lerp", Float) = 1
        _DistortionDistLerp("Distortion Dist Lerp", Float) = 0.1

        _Cutout("Cutout", 2D) = "white" {}
        _CutoutErosion("Cutout Erosion", Float) = 0
        _CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 0.05
        _CutoutRotation("Cutout Rotation", Float) = 0
        _CutoutOffset("Cutout Offset", Vector) = (0,0,0,0)

        _BaseAlpha("Base Alpha", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D(_DistortionNoise);
            SAMPLER(sampler_DistortionNoise);

            TEXTURE2D(_DistortionDist);
            SAMPLER(sampler_DistortionDist);

            TEXTURE2D(_CutoutTexture);
            SAMPLER(sampler_CutoutTexture);

            TEXTURE2D(_Cutout);
            SAMPLER(sampler_Cutout);

            float4 _CutoutMaskSelector;

            float2 _DistUVS;
            float2 _DistUVP;

            float2 _DistDistUVS;
            float2 _DistDistUVP;

            float _DistortionLerp;
            float _DistortionDistLerp;

            float _CutoutErosion;
            float _CutoutErosionSmoothness;
            float _CutoutRotation;
            float2 _CutoutOffset;

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
                float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            float2 Rotate(float2 uv, float angle)
            {
                float s = sin(radians(angle));
                float c = cos(radians(angle));
                uv -= 0.5;
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                return uv + 0.5;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // distortion noise
                float2 uvNoise = i.uv * _DistUVS + _DistUVP;
                float noise = SAMPLE_TEXTURE2D(_DistortionNoise, sampler_DistortionNoise, uvNoise).r;

                float2 uvDist = i.uv * _DistDistUVS + _DistDistUVP;
                float distNoise = SAMPLE_TEXTURE2D(_DistortionDist, sampler_DistortionDist, uvDist).r;

                float2 distortion = (noise + distNoise * _DistortionDistLerp) * _DistortionLerp;

                // cutout
                float2 cutUV = Rotate(i.uv + _CutoutOffset, _CutoutRotation);
                float cutMask = SAMPLE_TEXTURE2D(_Cutout, sampler_Cutout, cutUV).r;
                cutMask = smoothstep(_CutoutErosion, _CutoutErosion + _CutoutErosionSmoothness, cutMask);

                // screen sample
                float2 finalUV = screenUV + distortion * cutMask;
                float3 col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, finalUV).rgb;

                return float4(col, i.color.a * cutMask);
            }

            ENDHLSL
        }
    }
}