Shader "SyntheticData/BackgroundAugmentation"
{
    Properties
    {
        _BaseMap ("Background", 2D) = "white" {}
        _Brightness ("Brightness", Range(0.1, 2.0)) = 1.0
        _Contrast ("Contrast", Range(0.1, 2.0)) = 1.0
        _Blur ("Blur", Range(0.0, 1.0)) = 0.0
        _Temperature ("Temperature", Range(3000.0, 9000.0)) = 6500.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Brightness;
                float _Contrast;
                float _Blur;
                float _Temperature;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = _BaseMap_TexelSize.xy * (1.0 + 3.0 * saturate(_Blur));
                float2 uv = saturate(input.uv);
                half4 center = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 neighbours =
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv + float2(offset.x, 0.0))) +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv - float2(offset.x, 0.0))) +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv + float2(0.0, offset.y))) +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv - float2(0.0, offset.y)));
                half3 blurred = (center.rgb * 4.0 + neighbours.rgb) / 8.0;
                half3 color = lerp(center.rgb, blurred, saturate(_Blur));
                color = (color - 0.5) * _Contrast + 0.5;
                color *= _Brightness;

                float warmth = clamp((6500.0 - _Temperature) / 2500.0, -1.0, 1.0);
                color *= half3(1.0 + 0.12 * warmth, 1.0, 1.0 - 0.12 * warmth);
                return half4(saturate(color), center.a);
            }
            ENDHLSL
        }
    }
}
