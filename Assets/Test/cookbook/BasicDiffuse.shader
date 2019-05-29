Shader "Test/cookbook/BasicDiffuse"
{
	Properties
	{
		_EmissiveColor ("Emissive Color", Color) = (1,1,1,1)
		_AmbientColor ("Ambient Color", Color) = (1,1,1,1)
		_MySlideValue ("Slider", Range(0, 10)) = 2.5
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM

		// #pragma surface surf Standard fullforwardshadows
		// #pragma surface surf Lambert
		#pragma surface surf BasicDiffuse

		inline float4 LightingBasicDiffuse(SurfaceOutput s, fixed3 lightDir, fixed atten)
		{
			float difLight = max(0, dot(s.Normal, lightDir));
			// float halfLambert = difLight * 0.5 + 0.5;

			float4 col;
			col.rgb = s.Albedo * _LightColor0.rgb * (difLight * atten * 2);
			col.a = s.Alpha;
			return col;
		}

		// Use shader model 3.0 target, to get nicer looking lighting
		// #pragma target 3.0

		float4 _EmissiveColor;
		float4 _AmbientColor;
		float _MySlideValue;

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		void surf (Input IN, inout SurfaceOutput o) {
			float4 c;
			c = pow((_EmissiveColor + _AmbientColor), _MySlideValue);

			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}

	FallBack "Diffuse"
}
