Shader "Custom/URP/Grid_01_URP"
{
    Properties
    {
        _Masks("Masks", 2D) = "white" {}
        _Texture("Texture", 2D) = "white" {}
        _TileOverall("Tile Overall", Float) = 200
        _TileX("Tile X", Float) = 1
        _TileY("Tile Y", Float) = 1

        _TextureTileOverall("Texture Tile Overall", Float) = 1

        _Normal("Normal", 2D) = "bump" {}
        _NormalIntensity("Normal Intensity", Range(0,1)) = 1

        _RoughnessMin("Roughness Min", Range(0,1)) = 0
        _RoughnessMax("Roughness Max", Range(0,1)) = 1

        _Specular("Specular", Range(0,1)) = 0.01

        _ColorOverall("Color Overall", Color) = (1,1,1,1)
        _Color01("Color 01", Color) = (1,1,1,1)
        _Color02("Color 02", Color) = (1,1,1,1)

        _MasksTileOverall("Masks Tile Overall", Float) = 1
        _RandomTileColorsMax("Random Tile Colors Max", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"

            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
            };

            TEXTURE2D(_Masks);
            SAMPLER(sampler_Masks);

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);

            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);

            CBUFFER_START(UnityPerMaterial)

            float _TileOverall;
            float _TileX;
            float _TileY;

            float _TextureTileOverall;

            float _NormalIntensity;

            float _RoughnessMin;
            float _RoughnessMax;

            float _Specular;

            float4 _ColorOverall;
            float4 _Color01;
            float4 _Color02;

            float _MasksTileOverall;
            float _RandomTileColorsMax;

            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;

                OUT.normalWS = normInputs.normalWS;
                OUT.tangentWS = float4(normInputs.tangentWS, IN.tangentOS.w);

                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 tiledUV =
                    (IN.uv * _TileOverall) *
                    float2(_TileX, _TileY);

                float2 maskUV = tiledUV * _MasksTileOverall;

                float4 maskTex =
                    SAMPLE_TEXTURE2D(_Masks, sampler_Masks, maskUV);

                float randomLerp =
                    lerp(0.0, _RandomTileColorsMax, saturate(maskTex.b));

                float3 tileColor =
                    lerp(_Color01.rgb, _Color02.rgb,
                    saturate(maskTex.r) * randomLerp);

                float2 texUV = tiledUV * _TextureTileOverall;

                float4 baseTex =
                    SAMPLE_TEXTURE2D(_Texture, sampler_Texture, texUV);

                float3 albedo =
                    (baseTex.r * tileColor) *
                    _ColorOverall.rgb;

                float smoothness =
                    lerp(_RoughnessMin,
                         _RoughnessMax,
                         baseTex.g);

                float3 normalTS =
                    UnpackNormal(
                        SAMPLE_TEXTURE2D(_Normal, sampler_Normal, tiledUV)
                    );

                normalTS = lerp(float3(0,0,1), normalTS, _NormalIntensity);

                float3 bitangent =
                    cross(IN.normalWS, IN.tangentWS.xyz) *
                    IN.tangentWS.w;

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS.xyz),
                    normalize(bitangent),
                    normalize(IN.normalWS)
                );

                float3 normalWS =
                    normalize(mul(normalTS, TBN));

                Light mainLight = GetMainLight();

                float NdotL =
                    saturate(dot(normalWS, mainLight.direction));

                float3 diffuse =
                    albedo *
                    mainLight.color *
                    NdotL;

                float3 viewDir =
                    normalize(GetWorldSpaceViewDir(IN.positionWS));

                float3 halfDir =
                    normalize(mainLight.direction + viewDir);

                float spec =
                    pow(saturate(dot(normalWS, halfDir)),
                    smoothness * 128.0)
                    * _Specular;

                float3 finalColor =
                    diffuse + spec;

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}