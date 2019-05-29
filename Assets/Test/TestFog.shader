Shader "VolumetricFog/Test/TestFog"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE

    #include "UnityCG.cginc"

	struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
        float3 ray : TEXCOORD1;
    };

	uniform float4x4 _InverseProjectionMatrix;

	v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        
        float4 clipPos = float4(v.texcoord * 2.0 - 1.0, 1.0, 1.0);
        float4 cameraRay = mul(_InverseProjectionMatrix, clipPos);

        o.ray = cameraRay / cameraRay.w;

        return o;
    }

	fixed4 frag(v2f i) : SV_Target
	{
		return fixed4(i.ray.x, i.ray.y, i.ray.z, 1);
	}

	ENDCG


    SubShader
    {
        // Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            ENDCG
        }
    }
}
