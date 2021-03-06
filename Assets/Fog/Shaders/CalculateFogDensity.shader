﻿Shader "VolumetricFog/CalculateFogDensity"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "AutoLight.cginc"
    #include "DistanceFunc.cginc"
    #include "NoiseSimplex.cginc"

    #pragma multi_compile SHADOWS_ON SHADOWS_OFF

    #pragma shader_feature __ HEIGHTFOG
    #pragma shader_feature __ RAYLEIGH_SCATTERING
    #pragma shader_feature __ HENYEY_GREENSTEIN
    #pragma shader_feature __ CORNETTE_SHANKS
    #pragma shader_feature __ SCHLICK
    #pragma shader_feature __ LIMIT_FOG_SIZE
    #pragma shader_feature __ NOISE_2D
    #pragma shader_feature __ NOISE_3D
    #pragma shader_feature __ SNOISE

    UNITY_DECLARE_SHADOWMAP(ShadowMap);

    uniform sampler2D _MainTex, _CameraDepthTexture, _NoiseTexture, _BlurNoiseTexture;

    uniform sampler3D _NoiseTex3D;

    uniform float4 _MainTex_TexelSize, _CameraDepthTexture_TexelSize;

    uniform float3 _ShadowColor, _LightColor, _FogColor, _FogWorldPosition, _LightDir, _FogDirection;

    uniform float _FogDensity, _RayleighScatteringCoeff, _MieScatteringCoeff,
        _ExtinctionCoeff, _Anisotropy, _KFactor, _LightIntensity, _FogSize,
        _RayMarchingSteps, _AmbientFog, _BaseHeightDensity, _HeightDensityCoeff,
        _NoiseScale, _FogSpeed;
    
    uniform float4x4 _InverseViewMatrix, _InverseProjectionMatrix;

    #define STEPS _RayMarchingSteps
    #define STEPSIZE 1/STEPS

    #define e  2.71828182845904523536
    #define pi 3.14159265358979323846

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
        float3 ray : TEXCOORD1;
    };

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

    float map(float3 p)
    {
        float d_box = sdBox(p - float3(_FogWorldPosition), _FogSize);
        return d_box;
    }

	fixed4 getCascadeWeights(float z)
    {
        float4 zNear = float4( z >= _LightSplitsNear ); 
        float4 zFar = float4( z < _LightSplitsFar ); 
        float4 weights = zNear * zFar; 
                
		return weights;
	}

	fixed4 getShadowCoord(float4 worldPos, float4 weights)
    {
		float3 shadowCoord = float3(0,0,0);
			    
		// find which cascades need sampling and then transform the positions to light space using worldtoshadow
			    
		if(weights[0] == 1)
			shadowCoord += mul(unity_WorldToShadow[0], worldPos).xyz; 

		if(weights[1] == 1)
			shadowCoord += mul(unity_WorldToShadow[1], worldPos).xyz; 

		if(weights[2] == 1)
			shadowCoord += mul(unity_WorldToShadow[2], worldPos).xyz; 

		if(weights[3] == 1)
			shadowCoord += mul(unity_WorldToShadow[3], worldPos).xyz; 
			   
        return float4(shadowCoord,1);            
	} 

    // HG相函数, https://www.astro.umd.edu/~jph/HG_note.pdf
    fixed4 getHenyeyGreenstein(float cosTheta)
    {
        float n = 1 - (_Anisotropy * _Anisotropy);;
        float d = 1 + _Anisotropy * _Anisotropy - 2 * _Anisotropy * cosTheta;
        return n / (4 * pi * pow(d, 1.5));
    }

    // 瑞利散射
    float getRayleighPhase(float cosTheta)
    {
        return (3.0 / (16.0 * pi)) * (1 + (cosTheta * cosTheta));
    }
    
    float getCornetteShanks(float cosTheta)
    {
        float g2 = _Anisotropy *_Anisotropy;
        float t1 = (3 * (1 - g2)) / (2 * (2 + g2));
        float cos2 = cosTheta * cosTheta;
        float t2 = (1 + cos2) / (pow((1 + g2 - 2 * _Anisotropy * cos2), 3/2));
        return t1 * t2;
    }

    float getSchlickScattering(float cosTheta)
    {
        float o1 = 1 - (_KFactor * _KFactor);
        float squ = (1 + _KFactor * cosTheta) * (1 + _KFactor * cosTheta);
        float o2 = 4 * pi * squ;
        return o1 / o2;
    }
    
    // 比尔郎伯定律
    float getBeerLaw(float density, float stepSize)
    {
        return saturate(exp(-density * stepSize));
    }

    fixed4 getHeightDensity(float height)
    {
        float epow = pow(e, (-height * _HeightDensityCoeff));
        return _BaseHeightDensity * epow;
    }

    float sampleNoise(float3 position)
    {
        float3 offSet = float3(_Time.yyy) * _FogSpeed * _FogDirection;

        position *= _NoiseScale;
        position += offSet;
        
        float noiseValue = 0;

#if defined(SNOISE)
        noiseValue = snoise(float4(position,_SinTime.y)) * 0.1;
#elif defined(NOISE2D)
        noiseValue = tex2D(_NoiseTexture, position);
#elif defined(NOISE3D)                        
        noiseValue = tex3D(_NoiseTex3D, position);
#endif    
        return noiseValue;   
    }
   
    // 米氏散射
    float getMieScattering(float cosTheta)
    {
        float inScattering = 0;
#if defined(SCHLICK)
        float scattering = getSchlickScattering(cosTheta) * _MieScatteringCoeff;
        inScattering += scattering;
#elif defined(HENYEY_GREENSTEIN)
        float scattering = getHenyeyGreenstein(cosTheta) * _MieScatteringCoeff;
        inScattering += scattering;
#elif defined(CORNETTE_SHANKS)
        float scattering = getCornetteShanks(cosTheta) * _MieScatteringCoeff;
        inScattering += scattering;
#endif
        return inScattering;
    }
    
    // 瑞利散射
    float getRayleighScattering(float cosTheta)
    {
        float inScattering = 0;
#if defined(RAYLEIGH_SCATTERING)
        float scattering = getRayleighPhase(cosTheta) * _RayleighScatteringCoeff;
        inScattering += scattering;
#endif

        return inScattering;
    }

    float getScattering(float cosTheta)
    {
        float inScattering = 0;
        inScattering += getMieScattering(cosTheta);
        inScattering += getRayleighScattering(cosTheta);
        return inScattering;
    }

    fixed4 frag(v2f i) : SV_Target
    {
        float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
        float lindepth = Linear01Depth(depth);

        float4 viewPos = float4(i.ray.xyz * lindepth, 1);
        float3 worldPos = mul(_InverseViewMatrix, viewPos).xyz;
        float4 weights = getCascadeWeights(-viewPos.z);

        float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos.xyz);

        float rayDistance = length(worldPos - _WorldSpaceCameraPos.xyz);

        float stepSize = rayDistance / STEPS;

        float3 curPos = _WorldSpaceCameraPos.xyz;

        float2 interleavedPosition = (fmod(floor(i.pos.xy), 8.0));
        float offset = tex2D(_BlurNoiseTexture, interleavedPosition / 8.0 + float2(0.5/8.0, 0.5/8.0)).w;

        curPos += stepSize * rayDir * offset;

        float3 litFogColor = _LightIntensity * _FogColor;

        float transmittance = 1, extinction = 0;
        float3 result = 0;

        float cosTheta = dot(rayDir, _LightDir);

        int curSteps = 0;

        [loop]
        for (; curSteps < _RayMarchingSteps; curSteps++)
        {
            if (transmittance < 0.001)
                break;

            float distanceSample = 0;
#if defined(LIMIT_FOG_SIZE)
            distanceSample = map(curPos);
#endif

            if (distanceSample < 0.0001)
            {
                float noiseValue = sampleNoise(curPos);
                float fogDensity = noiseValue * _FogDensity;
#if defined(HEIGHTFOG)
                float heightDensity = getHeightDensity(curPos.y);
                fogDensity *= saturate(heightDensity);
#endif       
                extinction = _ExtinctionCoeff * fogDensity;
                transmittance *= getBeerLaw(extinction, stepSize);

                float inScattering = getScattering(cosTheta);
                inScattering *= fogDensity;

#if SHADOWS_ON
                float4 shadowCoord = getShadowCoord(float4(curPos, 1), weights);
                float shadowTerm = UNITY_SAMPLE_SHADOW(ShadowMap, shadowCoord);
                float3 fColor = lerp(_ShadowColor, litFogColor, shadowTerm + _AmbientFog);
#endif

#if SHADOWS_OFF
                float3 fColor = litFogColor;
#endif

                result += inScattering * stepSize * fColor;
            }
            else
            {
                result += _FogColor + _LightIntensity;
            }

            curPos += rayDir * stepSize;
        }

        return fixed4(result, transmittance);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}