Shader "Custom/GlitchScreen"
{
    Properties
    {
        _BaseMap ("Main Tex", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}

        _Intensity ("Intensity", Range(0, 1)) = 0.5
        _WhiteBloom ("WhiteBloom", Range(0, 1)) = 0
        _BlockCount ("Block Count", Range(10, 200)) = 20
        _OffsetStrength ("Offset Strength", Range(0, 0.2)) = 0.03
        _RGBOffset ("RGB Offset", Range(0, 0.03)) = 0.005
        _NoiseStrength ("Noise Strength", Range(0, 0.3)) = 0.05
        _ScanLineStrength ("ScanLine Strength", Range(0, 1)) = 0.15
        _ScanLineCount ("ScanLine Count", Range(100, 1500)) = 600
        _Speed ("Speed", Range(0, 50)) = 20
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "Glitch"

            ZWrite On
            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            float _Intensity;
            float _WhiteBloom;
            float _BlockCount;
            float _OffsetStrength;
            float _RGBOffset;
            float _NoiseStrength;
            float _ScanLineStrength;
            float _ScanLineCount;
            float _Speed;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Simple hash random.
            // Used for per-line glitch offset and screen noise.
            float Random(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Quantize screen into horizontal blocks.
                // Each block receives a different random horizontal offset.
                float blockId = floor(uv.y * _BlockCount);

                // Time is also quantized, otherwise the noise changes too smoothly.
                float timeId = floor(_Time.y * _Speed);

                float lineRand = Random(float2(blockId, timeId));

                // Convert random from 0~1 to -1~1.
                float lineOffset = (lineRand * 2.0 - 1.0) * _OffsetStrength * _Intensity;

                // Only some horizontal bands should glitch.
                float glitchMask = step(0.3, lineRand) * _Intensity;

                uv.x += lineOffset * glitchMask;

                // RGB channel split.
                float2 rgbOffset = float2(_RGBOffset * _Intensity, 0);

                float r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + rgbOffset).r;
                float g = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).g;
                float b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - rgbOffset).b;

                float emissionInstensity = 3;
                r += emissionInstensity * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv + rgbOffset).r;
                g += emissionInstensity * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).g;
                b += emissionInstensity * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv - rgbOffset).b;

                float3 col = float3(r, g, b);

                // Scanline darkening.
                float scanLine = sin(input.uv.y * _ScanLineCount) * 0.5 + 0.5;
                col *= lerp(1.0, scanLine, _ScanLineStrength * _Intensity);

                // // Screen noise.
                // float noise = Random(input.uv * _ScreenParams.xy + timeId);
                // col += noise * _NoiseStrength * _Intensity;
                col.rgb += float3(1,1,1) * _WhiteBloom; 
                return float4(col, 1.0);
            }

            ENDHLSL
        }
    }
}