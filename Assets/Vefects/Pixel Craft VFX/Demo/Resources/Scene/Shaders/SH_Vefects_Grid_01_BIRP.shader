Shader "Custom/URP/Vefects_Grid_01"
{
    Properties
    {
        _Masks("Masks", 2D) = "white" {}
        _Texture("Texture", 2D) = "white" {}
        _Normal("Normal", 2D) = "bump" {}

        _TileOverall("Tile Overall", Float) = 200
        _TileX("Tile X", Float) = 1
        _TileY("Tile Y", Float) = 1

        _TextureTileOverall("Texture Tile Overall", Float) = 1
        _MasksTileOverall("Masks Tile Overall", Float) = 1

        _ParallaxScale("Parallax Scale", Range(0,0.1)) = 0.02
        _NormalIntensity("Normal Intensity", Range(0,1)) = 1

        _RoughnessMin("Roughness Min", Range(0,1)) = 0
        _RoughnessMax("Roughness Max", Range(0,1)) = 1

        _Specular("Specular", Range(0,1)) = 0.01

        _ColorOverall("Color Overall", Color) = (1,1,1,1)
        _Color01("Color 01", Color) = (1,1,1,1)
        _Color02("Color 02", Color) = (1,1,1,1)

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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;

                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;

                float3 viewDirTS : TEXCOORD4;
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
            float _MasksTileOverall;

            float _ParallaxScale;
            float _NormalIntensity;

            float _RoughnessMin;
            float _RoughnessMax;

            float _Specular;

            float4 _ColorOverall;
            float4 _Color01;
            float4 _Color02;

            float _RandomTileColorsMax;

            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;

                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = float4(normalInputs.tangentWS, IN.tangentOS.w);

                OUT.uv = IN.uv;

                float3 viewDirWS =
                    GetWorldSpaceViewDir(posInputs.positionWS);

                float3 bitangentWS =
                    cross(normalInputs.normalWS,
                          normalInputs.tangentWS) * IN.tangentOS.w;

                float3x3 TBN =
                    float3x3(
                        normalInputs.tangentWS,
                        bitangentWS,
                        normalInputs.normalWS
                    );

                OUT.viewDirTS = mul(TBN, viewDirWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 tiledUV =
                    (IN.uv * _TileOverall) *
                    float2(_TileX, _TileY);

                // MASK SAMPLE
                float4 maskSample =
                    SAMPLE_TEXTURE2D(_Masks, sampler_Masks, tiledUV);

                // PARALLAX
                float height =
                    saturate(maskSample.g);

                float2 parallaxOffset =
                    (IN.viewDirTS.xy / IN.viewDirTS.z) *
                    ((height - 1.0) * _ParallaxScale);

                float2 bouv =
                    tiledUV + parallaxOffset;

                // NORMAL
                float3 normalTS =
                    UnpackNormal(
                        SAMPLE_TEXTURE2D(_Normal, sampler_Normal, bouv)
                    );

                normalTS =
                    lerp(float3(0,0,1),
                         normalTS,
                         _NormalIntensity);

                float3 bitangentWS =
                    cross(IN.normalWS, IN.tangentWS.xyz) *
                    IN.tangentWS.w;

                float3x3 TBN =
                    float3x3(
                        normalize(IN.tangentWS.xyz),
                        normalize(bitangentWS),
                        normalize(IN.normalWS)
                    );

                float3 normalWS =
                    normalize(mul(normalTS, TBN));

                // TEXTURE
                float4 texSample =
                    SAMPLE_TEXTURE2D(
                        _Texture,
                        sampler_Texture,
                        bouv * _TextureTileOverall
                    );

                // MASKS
                float4 masksTex =
                    SAMPLE_TEXTURE2D(
                        _Masks,
                        sampler_Masks,
                        bouv * _MasksTileOverall
                    );

                float randomColor =
                    lerp(
                        0.0,
                        _RandomTileColorsMax,
                        saturate(masksTex.b)
                    );

                float3 tileColor =
                    lerp(
                        _Color01.rgb,
                        _Color02.rgb,
                        saturate(masksTex.r) * randomColor
                    );

                float3 albedo =
                    (texSample.r * tileColor) *
                    _ColorOverall.rgb;

                float smoothness =
                    lerp(
                        _RoughnessMin,
                        _RoughnessMax,
                        texSample.g
                    );

                // LIGHTING
                Light mainLight = GetMainLight();

                float3 lightDir =
                    normalize(mainLight.direction);

                float3 viewDir =
                    normalize(GetWorldSpaceViewDir(IN.positionWS));

                float NdotL =
                    saturate(dot(normalWS, lightDir));

                float3 diffuse =
                    albedo *
                    mainLight.color *
                    NdotL;

                float3 halfDir =
                    normalize(lightDir + viewDir);

                float specular =
                    pow(
                        saturate(dot(normalWS, halfDir)),
                        lerp(1, 128, smoothness)
                    ) * _Specular;

                float3 finalColor =
                    diffuse + specular;

                return half4(finalColor, 1);
            }

            ENDHLSL
        }
    }
}