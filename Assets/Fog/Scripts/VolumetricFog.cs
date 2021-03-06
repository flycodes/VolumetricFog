﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VolumetricFogExtension
{
    public enum FpsLevel { Null, Fps30, Fps60, Unlimited, }

    public enum MieScatteringApproximation
    {
        HenyeyGreenstein,
        CornetteShanks,
        Schlick,
        Off
    }

    /// <summary>
    /// TODO: Add compute shader later.
    /// </summary>
    [Flags]
    public enum NoiseSource
    {
        Texture2D = 1,
        Texture3D = 2,
        SimplexNoise = 4,
    }

    [RequireComponent(typeof(Camera))]
    public class VolumetricFog : MonoBehaviour
    {
        private const RenderTextureFormat FogRenderTextureFormat = RenderTextureFormat.ARGBHalf;

        public Shader m_CalculateFogShader;
        public Shader m_ApplyBlurShader;
        public Shader m_ApplyFogShader;

        public Transform m_Light;
        public List<Light> m_FogLightCasters;

        public Texture2D m_FogTexture2D;
        public Texture2D m_BlurNoiseTexture2D;

        public Vector3 m_FogWorldPosition;
        public bool m_LimitFogSize = true;
        public float m_FogSize = 10.0f;

        [Range(0, 8)]
        public int m_RenderTextureResDivision;

        [Range(16, 256)]
        public int m_RayMarchingSteps = 128;

        public bool m_OptimizeSettingsFPS;

        [SerializeField]
        private FpsLevel m_FpsLevel = FpsLevel.Fps60;
        public FpsLevel FpsLevel
        {
            set
            {
                m_FpsLevel = value;
                m_FpsHelper.FpsLevel = m_FpsLevel;
            }
            get { return m_FpsLevel; }
        }


        // 瑞利散射
        public bool m_EnableRayleighScattering = true;
        public float m_RayleighScatteringCoeff = 0.25f;

        // 米氏散射
        public float m_MieScatteringCoeff = 0.25f;
        public MieScatteringApproximation m_MieScatteringApproximation = MieScatteringApproximation.HenyeyGreenstein;

        public float m_FogDensityCoeff = 0.3f;
        public float m_ExtinctionCoeff = 0.01f;

        [Range(-1, 1)]
        public float m_Anisotropy = 0.5f;

        public float m_HeightDensityCoeff = 0.5f;
        public float m_BaseHeightDensity = 0.5f;

        // 模糊
        [Range(1, 8)]
        public int m_BlurIterations = 4;
        public float m_BlurDepthFallOff = 0.5f;
        public Vector3 m_BlurOffsets = new Vector3(1, 2, 3);
        public Vector3 m_BlurWeights = new Vector3(0.213f, 0.17f, 0.036f);

        // 颜色处理
        public bool m_UseLightColor = false;
        public Color m_FogInShadowColor = Color.black;
        public Color m_FogInLightColor = Color.white;

        [Range(0, 1)]
        public float m_AmbientFog;

        [Range(0, 10)]
        public float m_LightIntensity = 1;

        // 风向, 风速
        public Vector3 m_WindDirection = Vector3.right;
        public float m_WindSpeed = 1f;

        public NoiseSource m_NoiseSource = NoiseSource.Texture2D;
        [Range(-100, 100)]
        public float m_NoiseScale = 0f;
        public DTexture3D m_3DNoiseTextureDimensions = DTexture3D.one;

        public bool m_AddSceneColor = false;
        public bool m_BlurEnabled = false;
        public bool m_ShadowsEnabled = false;
        public bool m_HeightFogEnabled = false;

        private float k_Factor;

        #region Materials

        private Texture3D m_FogTexture3D;
        private RenderTexture m_FogTextureSimplex;

        private Material m_ApplyBlurMaterial;
        private Material m_CalculateFogMaterial;
        private Material m_ApplyFogMaterial;

        public Material ApplyFogMaterial
        {
            get
            {
                if (!m_ApplyFogMaterial && m_ApplyFogShader)
                {
                    m_ApplyFogMaterial = new Material(m_ApplyFogShader);
                    m_ApplyFogMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return m_ApplyFogMaterial;
            }
        }

        public Material ApplyBlurMaterial
        {
            get
            {
                if (!m_ApplyBlurMaterial && m_ApplyBlurShader)
                {
                    m_ApplyBlurMaterial = new Material(m_ApplyBlurShader);
                    m_ApplyBlurMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return m_ApplyBlurMaterial;
            }
        }

        public Material FogMaterial
        {
            get
            {
                if (!m_CalculateFogMaterial && m_CalculateFogShader)
                {
                    m_CalculateFogMaterial = new Material(m_CalculateFogShader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                return m_CalculateFogMaterial;
            }
        }

        #endregion Materials

        private Camera m_Camera;
        private Camera RequiredCamera
        {
            get
            {
                if (m_Camera == null)
                    m_Camera = GetComponent<Camera>();

                return m_Camera;
            }
        }

        private CommandBuffer m_AfterShadowPass;

        #region Keywords and Features

        internal readonly string Shader_SHADOWS_ON = "SHADOWS_ON";
        internal readonly string Shader_SHADOWS_OFF = "SHADOWS_OFF";
        internal readonly string Shader_HG_SCATTERING = "HG_SCATTERING";
        internal readonly string Shader_CS_SCATTERING = "CS_SCATTERING";
        internal readonly string Shader_SCHLICK_HG_SCATTERING = "SCHLICK_HG_SCATTERING";
        internal readonly string Shader_SNOISE = "SNOISE";
        internal readonly string Shader_NOISE2D = "NOISE2D";
        internal readonly string Shader_NOISE3D = "NOISE3D";

        internal readonly string Shader_HEIGHTFOG = "HEIGHTFOG";
        internal readonly string Shader_RAYLEIGH_SCATTERING = "RAYLEIGH_SCATTERING";
        internal readonly string Shader_HENYEY_GREENSTEIN = "HENYEY_GREENSTEIN";
        internal readonly string Shader_CORNETTE_SHANKS = "CORNETTE_SHANKS";
        internal readonly string Shader_SCHLICK = "SCHLICK";
        internal readonly string Shader_LIMIT_FOG_SIZE = "LIMIT_FOG_SIZE";

        #endregion Keywords and Features

        [SerializeField]
        private FpsHelper m_FpsHelper;

        void Awake()
        {
            if (m_FpsHelper == null)
            {
                Debug.LogError("Need MonoBehaviour FpsHelper");
            }
        }

        void Start()
        {
            m_FogLightCasters.ForEach(AddLightCommandBuffer);
            Regenerate3DTexture();
        }

        private void OnDestroy()
        {
            m_FogLightCasters.ForEach(RemoveLightCommandBuffer);
        }

        private void Regenerate3DTexture()
        {
            bool b3d = m_NoiseSource == NoiseSource.Texture3D;
            if (!b3d)
                return;

            m_FogTexture3D = TextureHelper.CreateFogLUT3DFrom2DSlices(m_FogTexture2D, m_3DNoiseTextureDimensions);
        }

        private void CalculateKFactor()
        {
            k_Factor = 1.55f * m_Anisotropy - (0.55f * Mathf.Pow(m_Anisotropy, 3));
        }

        private void AddLightCommandBuffer(Light light)
        {
            m_AfterShadowPass = new CommandBuffer { name = "Volumetric Fog ShadowMap" };
            m_AfterShadowPass.SetGlobalTexture("VolumetricFog_ShadowMap",
                new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive));

            if (light)
            {
                light.AddCommandBuffer(LightEvent.AfterShadowMap, m_AfterShadowPass);
            }
        }

        private void RemoveLightCommandBuffer(Light light)
        {
            if (m_AfterShadowPass != null && light != null)
            {
                light.RemoveCommandBuffer(LightEvent.AfterShadowMap, m_AfterShadowPass);
            }
        }

        private float CalculateRaymarchStepRation()
        {
            if (!m_OptimizeSettingsFPS) return 1;

            var currentFPS = m_FpsHelper.FpsValue;
            var targetFPS = 30f;
            switch (m_FpsLevel)
            {
                case FpsLevel.Fps30:
                    targetFPS = 30;
                    break;
                case FpsLevel.Fps60:
                    targetFPS = 60;
                    break;
                case FpsLevel.Unlimited:
                    targetFPS = currentFPS; // do not optimize
                    break;
                default:
                    Debug.Log("FPS Target not found");
                    break;
            }
            return Mathf.Clamp01(currentFPS / targetFPS);
        }

        private bool IsRelatedAssetsLoaded()
        {
            return m_FogTexture2D != null || ApplyFogMaterial != null || m_ApplyFogShader != null ||
                FogMaterial != null || m_CalculateFogShader != null ||
                m_ApplyBlurMaterial != null || m_ApplyBlurShader != null;
        }

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!IsRelatedAssetsLoaded())
            {
                Graphics.Blit(src, dst);
                return;
            }

            if (m_ShadowsEnabled)
            {
                Shader.EnableKeyword(Shader_SHADOWS_ON);
                Shader.DisableKeyword(Shader_SHADOWS_OFF);
            }
            else
            {
                Shader.DisableKeyword(Shader_SHADOWS_ON);
                Shader.EnableKeyword(Shader_SHADOWS_OFF);
            }

            m_Light.GetComponent<Light>().intensity = m_LightIntensity;

            var fogRTWidth = src.width >> m_RenderTextureResDivision;
            var fogRTHeight = src.height >> m_RenderTextureResDivision;

            var fogRT1 = RenderTexture.GetTemporary(fogRTWidth, fogRTHeight, 0, RenderTextureFormat.ARGBHalf);
            var fogRT2 = RenderTexture.GetTemporary(fogRTWidth, fogRTHeight, 0, RenderTextureFormat.ARGBHalf);

            fogRT1.filterMode = FilterMode.Bilinear;
            fogRT2.filterMode = FilterMode.Bilinear;

            SetMieScattering();
            SetNoiseSource();

            Shader.SetGlobalMatrix(ShaderIDs.InverseViewMatrix, RequiredCamera.cameraToWorldMatrix);
            Shader.SetGlobalMatrix(ShaderIDs.InverseProjectionMatrix, RequiredCamera.projectionMatrix.inverse);

            RenderFog(fogRT1, src);
            BlurFog(fogRT1, fogRT2);
            BlendWithScene(src, dst, fogRT1);

            RenderTexture.ReleaseTemporary(fogRT1);
            RenderTexture.ReleaseTemporary(fogRT2);
        }

        private void RenderFog(RenderTexture fogRenderTexture, RenderTexture src)
        {
            if (m_EnableRayleighScattering)
            {
                ShaderFeatureHelper(FogMaterial, Shader_RAYLEIGH_SCATTERING, true);
                FogMaterial.SetFloat(ShaderIDs.RayleighScatteringCoeff, m_RayleighScatteringCoeff);
            }
            else
            {
                ShaderFeatureHelper(FogMaterial, Shader_RAYLEIGH_SCATTERING, false);
            }

            ShaderFeatureHelper(FogMaterial, Shader_LIMIT_FOG_SIZE, m_LimitFogSize);
            ShaderFeatureHelper(FogMaterial, Shader_HEIGHTFOG, m_HeightFogEnabled);

            var rmsr = CalculateRaymarchStepRation();

            FogMaterial.SetFloat(ShaderIDs.RayMarchingSteps, m_RayMarchingSteps * Mathf.Pow(rmsr, 2));
            FogMaterial.SetFloat(ShaderIDs.FogDensity, m_FogDensityCoeff);
            FogMaterial.SetFloat(ShaderIDs.NoiseScale, m_NoiseScale);

            FogMaterial.SetFloat(ShaderIDs.ExtinctionCoeff, m_ExtinctionCoeff);
            FogMaterial.SetFloat(ShaderIDs.Anisotropy, m_Anisotropy);
            FogMaterial.SetFloat(ShaderIDs.BaseHeightDensity, m_BaseHeightDensity);

            FogMaterial.SetVector(ShaderIDs.FogWorldPosition, m_FogWorldPosition);
            FogMaterial.SetFloat(ShaderIDs.FogSize, m_FogSize);
            FogMaterial.SetFloat(ShaderIDs.LightIntensity, m_LightIntensity);

            FogMaterial.SetColor(ShaderIDs.FogColor, m_Light.GetComponent<Light>().color);
            FogMaterial.SetColor(ShaderIDs.ShadowColor, m_FogInShadowColor);
            FogMaterial.SetColor(ShaderIDs.FogColor, m_UseLightColor ? m_Light.GetComponent<Light>().color : m_FogInLightColor);

            FogMaterial.SetVector(ShaderIDs.LightDir, m_Light.GetComponent<Light>().transform.forward);
            FogMaterial.SetFloat(ShaderIDs.AmbientFog, m_AmbientFog);

            FogMaterial.SetVector(ShaderIDs.FogDirection, m_WindDirection);
            FogMaterial.SetFloat(ShaderIDs.FogSpeed, m_WindSpeed);

            FogMaterial.SetTexture(ShaderIDs.BlurNoiseTexture, m_BlurNoiseTexture2D);

            Graphics.Blit(src, fogRenderTexture, FogMaterial);
        }

        private void BlurFog(RenderTexture fogTarget1, RenderTexture fogTarget2)
        {
            if (!m_BlurEnabled)
                return;

            ApplyBlurMaterial.SetFloat(ShaderIDs.BlurDepthFalloff, m_BlurDepthFallOff);

            Vector4 blurOffsets = new Vector4(0, m_BlurOffsets.x, m_BlurOffsets.y, m_BlurOffsets.z);
            ApplyBlurMaterial.SetVector(ShaderIDs.BlurOffsets, blurOffsets);

            Vector4 blurWeights = new Vector4(m_BlurWeights.x + m_BlurWeights.y + m_BlurWeights.z, m_BlurWeights.x, m_BlurWeights.y, m_BlurWeights.z);
            ApplyBlurMaterial.SetVector(ShaderIDs.BlurWeights, blurWeights);

            for (int i = 0; i < m_BlurIterations; ++i)
            {
                ApplyBlurMaterial.SetVector(ShaderIDs.BlurDir, new Vector2(0, 1));
                Graphics.Blit(fogTarget1, fogTarget2, ApplyBlurMaterial);

                ApplyBlurMaterial.SetVector(ShaderIDs.BlurDir, new Vector2(1, 0));
                Graphics.Blit(fogTarget1, fogTarget2, ApplyBlurMaterial);
            }
        }

        private void BlendWithScene(RenderTexture src, RenderTexture dst, RenderTexture fogTarget)
        {
            if (!m_AddSceneColor)
            {
                Graphics.Blit(src, dst);
                return;
            }

            ApplyFogMaterial.SetTexture(ShaderIDs.FogRendertargetLinear, fogTarget);

            Graphics.Blit(src, dst, ApplyFogMaterial);
        }

        private void SetMieScattering()
        {
            ShaderFeatureHelper(FogMaterial, Shader_HG_SCATTERING, false);
            ShaderFeatureHelper(FogMaterial, Shader_CS_SCATTERING, false);
            ShaderFeatureHelper(FogMaterial, Shader_SCHLICK_HG_SCATTERING, false);

            switch (m_MieScatteringApproximation)
            {
                case MieScatteringApproximation.HenyeyGreenstein:
                    ShaderFeatureHelper(FogMaterial, Shader_HG_SCATTERING, true);
                    FogMaterial.SetFloat(ShaderIDs.MieScatteringCoeff, m_MieScatteringCoeff);
                    break;
                case MieScatteringApproximation.CornetteShanks:
                    ShaderFeatureHelper(FogMaterial, Shader_CS_SCATTERING, true);
                    FogMaterial.SetFloat(ShaderIDs.MieScatteringCoeff, m_MieScatteringCoeff);
                    break;
                case MieScatteringApproximation.Schlick:
                    CalculateKFactor();
                    ShaderFeatureHelper(FogMaterial, Shader_SCHLICK_HG_SCATTERING, true);
                    FogMaterial.SetFloat(ShaderIDs.KFactor, k_Factor);
                    FogMaterial.SetFloat(ShaderIDs.MieScatteringCoeff, m_MieScatteringCoeff);
                    break;
                case MieScatteringApproximation.Off:
                    break;
                default:
                    break;
            }
        }

        private void SetNoiseSource()
        {
            ShaderFeatureHelper(FogMaterial, Shader_SNOISE, false);
            ShaderFeatureHelper(FogMaterial, Shader_NOISE2D, false);
            ShaderFeatureHelper(FogMaterial, Shader_NOISE3D, false);

            switch (m_NoiseSource)
            {
                case NoiseSource.Texture2D:
                    ShaderFeatureHelper(FogMaterial, Shader_NOISE2D, false);
                    break;
                case NoiseSource.Texture3D:
                    ShaderFeatureHelper(FogMaterial, Shader_NOISE3D, false);
                    break;
                case NoiseSource.SimplexNoise:
                    ShaderFeatureHelper(FogMaterial, Shader_SNOISE, true);
                    break;
                default:
                    break;
            }
        }

        private void ShaderFeatureHelper(Material mat, string key, bool enable)
        {
            if (enable)
            {
                mat.EnableKeyword(key);
            }
            else
            {
                mat.DisableKeyword(key);
            }
        }
    }
}