Shader "Custom/BlueHologram"
{
    Properties
    {
        [Header(Base)]
        [HDR]_HologramColor("Hologram Color", Color) = (0.0, 0.6, 1.0, 1.0)
        _Alpha("Base Alpha", Range(0.0, 1.0)) = 0.25
        _EmissionIntensity("Emission Intensity", Range(0.0, 10.0)) = 2.5

        [Header(Fresnel)]
        _FresnelPower("Fresnel Power", Range(0.1, 10.0)) = 3.0
        _FresnelIntensity("Fresnel Intensity", Range(0.0, 10.0)) = 2.0

        [Header(Scan Line)]
        _ScanLineDensity("Scan Line Density", Range(1.0, 300.0)) = 80.0
        _ScanLineSpeed("Scan Line Speed", Range(-10.0, 10.0)) = 2.0
        _ScanLineWidth("Scan Line Width", Range(0.01, 0.99)) = 0.15
        _ScanLineIntensity("Scan Line Intensity", Range(0.0, 5.0)) = 1.5

        [Header(Flicker)]
        _FlickerSpeed("Flicker Speed", Range(0.0, 100.0)) = 20.0
        _FlickerIntensity("Flicker Intensity", Range(0.0, 1.0)) = 0.1

        [Header(Glitch)]
        _GlitchSpeed("Glitch Speed", Range(0.0, 50.0)) = 8.0
        _GlitchStrength("Glitch Strength", Range(0.0, 0.2)) = 0.015
        _GlitchFrequency("Glitch Frequency", Range(1.0, 100.0)) = 25.0

        [Header(Depth)]
        _DepthFadeDistance("Depth Fade Distance", Range(0.001, 5.0)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Hologram"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                float2 uv         : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)

            float4 _HologramColor;

            float _Alpha;
            float _EmissionIntensity;

            float _FresnelPower;
            float _FresnelIntensity;

            float _ScanLineDensity;
            float _ScanLineSpeed;
            float _ScanLineWidth;
            float _ScanLineIntensity;

            float _FlickerSpeed;
            float _FlickerIntensity;

            float _GlitchSpeed;
            float _GlitchStrength;
            float _GlitchFrequency;

            float _DepthFadeDistance;

            CBUFFER_END

            // Simple deterministic pseudo-random value.
            float Random(float value)
            {
                return frac(sin(value * 12.9898) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Divide the model into horizontal world-space bands.
                float glitchBand = floor(
                    positionWS.y * _GlitchFrequency +
                    _Time.y * _GlitchSpeed
                );

                float glitchNoise = Random(glitchBand);

                // Only a small percentage of horizontal bands are displaced.
                float glitchMask = step(0.92, glitchNoise);

                float glitchOffset =
                    (glitchNoise * 2.0 - 1.0) *
                    _GlitchStrength *
                    glitchMask;

                positionWS.x += glitchOffset;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);

                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Stronger glow at silhouette edges.
                float fresnel =
                    1.0 - saturate(dot(normalWS, viewDirWS));

                fresnel = pow(fresnel, _FresnelPower);
                fresnel *= _FresnelIntensity;

                // Animated horizontal scan lines in world space.
                float scanCoordinate =
                    input.positionWS.y * _ScanLineDensity -
                    _Time.y * _ScanLineSpeed;

                float scanWave = frac(scanCoordinate);

                float scanLine =
                    1.0 - smoothstep(
                        0.0,
                        _ScanLineWidth,
                        abs(scanWave - 0.5)
                    );

                scanLine *= _ScanLineIntensity;

                // Global high-frequency hologram flicker.
                float flickerNoise =
                    Random(floor(_Time.y * _FlickerSpeed));

                float flicker =
                    lerp(
                        1.0 - _FlickerIntensity,
                        1.0,
                        flickerNoise
                    );

                // Fade intersections with scene geometry.
                float2 screenUV =
                    input.screenPos.xy /
                    input.screenPos.w;

                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth =
                    LinearEyeDepth(
                        rawSceneDepth,
                        _ZBufferParams
                    );

                float fragmentEyeDepth =
                    LinearEyeDepth(
                        input.positionCS.z /
                        input.positionCS.w,
                        _ZBufferParams
                    );

                float depthDifference =
                    sceneEyeDepth - fragmentEyeDepth;

                float depthFade =
                    saturate(
                        depthDifference /
                        max(_DepthFadeDistance, 0.0001)
                    );

                float brightness =
                    1.0 +
                    fresnel +
                    scanLine;

                float3 finalColor =
                    _HologramColor.rgb *
                    brightness *
                    _EmissionIntensity *
                    flicker;

                float finalAlpha =
                    (_Alpha +
                    fresnel * 0.25 +
                    scanLine * 0.15) *
                    flicker *
                    depthFade;

                finalAlpha = saturate(finalAlpha);

                return half4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}