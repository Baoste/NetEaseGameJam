Shader "Custom/PixelateShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_ScreenHeight("Screen Height", Float) = 512
		_ScreenWidth("Screen Width", Float) = 512
	}
	SubShader
	{
		Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
                float _ScreenHeight;
                float _ScreenWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.uv = v.uv;
				return o;
			}


			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;

				float range = 0.01;
				float exp = 4;
				float a = range / pow((range - 0.5), exp);

				//// corner twist
				float2 newUV;
				if (uv.x < 0.5 && uv.y < 0.5)
				{
					float ty = a*pow((uv.x-0.5), exp);
					float tx = a*pow((uv.y-0.5), exp);
					newUV = uv + float2(-tx*2*abs(uv.x-0.5), -ty*2*abs(uv.y-0.5));
				}
				else if (uv.x > 0.5 && uv.y < 0.5)
				{
					float ty = a*pow((uv.x-0.5), exp);
					float tx = -a*pow((uv.y-0.5), exp) + 1;
					newUV = uv + float2((1-tx)*2*abs(uv.x-0.5), -ty*2*abs(uv.y-0.5));
				}
				else if (uv.x < 0.5 && uv.y > 0.5)
				{
					float ty = -a*pow((uv.x-0.5), exp) + 1;
					float tx = a*pow((uv.y-0.5), exp);
					newUV = uv + float2(-tx*2*abs(uv.x-0.5), (1-ty)*2*abs(uv.y-0.5));
				}
				else
				{
					float ty = -a*pow((uv.x-0.5), exp) + 1;
					float tx = -a*pow((uv.y-0.5), exp) + 1;
					if (uv.y > ty || uv.x > tx)
					newUV = uv + float2((1-tx)*2*abs(uv.x-0.5), (1-ty)*2*abs(uv.y-0.5));
				}

				//float2 newUV = uv;

				// float pixelHeight = _ScreenHeight / 3.14159265;
				// newUV.y = (floor(newUV.y * pixelHeight) + 0.5) / pixelHeight;
				// float pixelWidth = _ScreenWidth / 3.14159265 * 16 / 9;
				// newUV.x = (floor(newUV.x * pixelWidth) + 0.5) / pixelWidth;
				half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, newUV);

				
				// scan line
				col.rb *= step(0, sin(uv.y * _ScreenHeight)+1) * 0.5 + 1;
				col.g *= (sin(uv.y * _ScreenHeight)+1) * 0.5 + 1;

				half3 scanCol = half3(1, 1, 1);
				half4 pixelScanlineBrightness = half4(0.225, 0.85, 0.1, 0.95);
				float ssScanY = step(0, sin(newUV.y / 4 * _ScreenHeight));;
				col *= ssScanY * pixelScanlineBrightness.x + pixelScanlineBrightness.y;

				float ssScanX = smoothstep(-1, 1, sin((newUV.y + _Time.x)* _ScreenHeight));
				col *= ssScanX * pixelScanlineBrightness.z + pixelScanlineBrightness.w;
				
				//// dark corner
				//float dx = newUV.x;
				//float dy = newUV.y;
				//float ex = 0.01;
				//if (newUV.x < 0.5)
				//	col.rgb *= pow(dx/0.5, ex);
				//else
				//	col.rgb *= pow((1-dx)/0.5, ex);
				//if (newUV.y < 0.5)
				//	col.rgb *= pow(dy/0.5, ex);
				//else
				//	col.rgb *= pow((1-dy)/0.5, ex);
				
				//// black corner
				//if (newUV.x < 0 || newUV.x > 1 || newUV.y < 0 || newUV.y > 1)
				//	col.rgb *= 0;
				return col;
			}
			ENDHLSL
		}
	}
}
