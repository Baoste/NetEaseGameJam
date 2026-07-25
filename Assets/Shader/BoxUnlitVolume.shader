Shader "Custom/URP/BoxUnlitVolume"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5

        _OutsideLighting("Outside Lighting", Range(0, 1)) = 0.1

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 3.5

            #pragma vertex Vert
            #pragma fragment Frag

            // GPU instancing
            #pragma multi_compile_instancing

            // Main light shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            // Additional lights
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Other URP lighting features
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;
                half4 _BaseColor;

                half _Metallic;
                half _Smoothness;

                half _OutsideLighting;
                float _LightVolumeCount;
                float4x4 _LightBoxWorldToLocal0;
                float4x4 _LightBoxWorldToLocal1;
                float3 _LightBoxCenter0;
                float3 _LightBoxCenter1;
                float3 _LightBoxHalfSize0;
                float3 _LightBoxHalfSize1;
                float3 _BoxLightDirection0;
                float3 _BoxLightDirection1;
                float4 _BoxLightColorIntensity0;
                float4 _BoxLightColorIntensity1;
                // x = ambient, y = local-space fade width
                float4 _BoxLightSettings0;
                float4 _BoxLightSettings1;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float2 uv               : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                half3 viewDirWS   : TEXCOORD3;

                /*
                 * x   = fog factor
                 * yzw = vertex lighting
                 */
                half4 fogFactorAndVertexLight : TEXCOORD4;

                DECLARE_LIGHTMAP_OR_SH(
                    staticLightmapUV,
                    vertexSH,
                    5
                );

                float4 positionCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;

                output.viewDirWS =
                    GetWorldSpaceViewDir(positionInputs.positionWS);

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _BaseMap
                );

                half fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);

                half3 vertexLighting = VertexLighting(
                    positionInputs.positionWS,
                    normalInputs.normalWS
                );

                output.fogFactorAndVertexLight =
                    half4(fogFactor, vertexLighting);

                OUTPUT_LIGHTMAP_UV(
                    input.staticLightmapUV,
                    unity_LightmapST,
                    output.staticLightmapUV
                );

                OUTPUT_SH(
                    output.normalWS,
                    output.vertexSH
                );

                return output;
            }

            /*
             * Returns:
             *
             * 0 = fragment is outside the box
             * 1 = fragment is inside the box
             *
             * When _BoxFadeWidth is greater than zero, the mask
             * gradually changes from 0 to 1 near the inner edge.
             */
            float GetBoxMask(
                float3 positionWS,
                float4x4 worldToLocal,
                float3 boxCenter,
                float3 boxHalfSize,
                float fadeWidth)
            {
                // Convert fragment world position into box local space
                float3 positionBoxSpace = mul(
                    worldToLocal,
                    float4(positionWS, 1.0)
                ).xyz;

                // Position relative to BoxCollider.center
                float3 relativePosition =
                    positionBoxSpace - boxCenter;

                /*
                 * Positive values mean the fragment is inside
                 * the corresponding box axis.
                 *
                 * Negative values mean it is outside.
                 */
                float3 distanceToSurface =
                    boxHalfSize - abs(relativePosition);

                /*
                 * The minimum component represents the distance
                 * to the nearest box face.
                 */
                float signedInsideDistance = min(
                    distanceToSurface.x,
                    min(
                        distanceToSurface.y,
                        distanceToSurface.z
                    )
                );

                // Hard boundary
                if (fadeWidth <= 0.00001)
                {
                    return step(
                        0.0,
                        signedInsideDistance
                    );
                }

                // Soft boundary on the inside of the box
                return saturate(
                    signedInsideDistance /
                    max(fadeWidth, 0.00001)
                );
            }

            SurfaceData CreateSurfaceData(float2 uv)
            {
                SurfaceData surfaceData = (SurfaceData)0;

                half4 baseSample = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    uv
                );

                baseSample *= _BaseColor;

                surfaceData.albedo = baseSample.rgb;
                surfaceData.alpha = baseSample.a;

                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;

                // No normal map in this basic version
                surfaceData.normalTS = half3(0, 0, 1);

                surfaceData.occlusion = 1;
                surfaceData.emission = half3(0, 0, 0);

                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                return surfaceData;
            }

            InputData CreateInputData(Varyings input)
            {
                InputData inputData = (InputData)0;

                inputData.positionWS = input.positionWS;

                inputData.normalWS =
                    NormalizeNormalPerPixel(input.normalWS);

                inputData.viewDirectionWS =
                    SafeNormalize(input.viewDirWS);

                inputData.shadowCoord =
                    TransformWorldToShadowCoord(input.positionWS);

                inputData.fogCoord =
                    input.fogFactorAndVertexLight.x;

                inputData.vertexLighting =
                    input.fogFactorAndVertexLight.yzw;

                inputData.bakedGI = SAMPLE_GI(
                    input.staticLightmapUV,
                    input.vertexSH,
                    inputData.normalWS
                );

                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(input.positionCS);

                inputData.shadowMask =
                    SAMPLE_SHADOWMASK(input.staticLightmapUV);

                return inputData;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData = CreateSurfaceData(input.uv);
                InputData inputData = CreateInputData(input);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 finalColor = surfaceData.albedo * _OutsideLighting;

                if (_LightVolumeCount > 0.5)
                {
                    float mask0 = GetBoxMask(input.positionWS, _LightBoxWorldToLocal0,
                        _LightBoxCenter0, _LightBoxHalfSize0, _BoxLightSettings0.y);
                    half diffuse0 = saturate(dot(normalWS, -normalize(_BoxLightDirection0)));
                    half3 lighting0 = _BoxLightColorIntensity0.rgb *
                        (_BoxLightSettings0.x + diffuse0 * _BoxLightColorIntensity0.a);
                    finalColor = lerp(finalColor, surfaceData.albedo * lighting0, mask0);
                }

                if (_LightVolumeCount > 1.5)
                {
                    float mask1 = GetBoxMask(input.positionWS, _LightBoxWorldToLocal1,
                        _LightBoxCenter1, _LightBoxHalfSize1, _BoxLightSettings1.y);
                    half diffuse1 = saturate(dot(normalWS, -normalize(_BoxLightDirection1)));
                    half3 lighting1 = _BoxLightColorIntensity1.rgb *
                        (_BoxLightSettings1.x + diffuse1 * _BoxLightColorIntensity1.a);
                    // The second volume adds naturally in overlap regions.
                    finalColor += surfaceData.albedo * lighting1 * mask1;
                }

                // Optional: fog still affects the visible lit area.
                finalColor = MixFog(
                    finalColor,
                    inputData.fogCoord
                );

                return half4(
                    finalColor,
                    surfaceData.alpha
                );
            }

            ENDHLSL
        }

        /*
         * The model still casts shadows normally.
         * The BoxCollider only changes the Forward rendering result.
         */
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
