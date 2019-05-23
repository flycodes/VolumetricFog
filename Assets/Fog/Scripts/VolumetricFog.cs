using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FogExtension
{
    public enum FpsLevel { Fps30, Fps60, Unlimited, }

    public enum MieScatteringApproximation
    {
        HenyeyGreenstein,
        CornetteShanks,
        Schlick,
        Off
    }

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
        public Vector3 m_3DNoiseTextureDimensions = Vector3.one;

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

        public Material CalculateFogMaterial
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

        #endregion

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

        void Awake()
        {

        }

        void Start()
        {
            m_FogLightCasters.ForEach(AddLightCommandBuffer);
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
            return 0.0f;
        }

        private bool IsRelatedAssetsLoaded()
        {
            return m_FogTexture2D != null || ApplyFogMaterial != null || m_ApplyFogShader != null ||
                CalculateFogMaterial != null || m_CalculateFogShader != null ||
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
                Shader.EnableKeyword("SHADOWS_ON");
                Shader.DisableKeyword("SHADOWS_OFF");
            }
            else
            {
                Shader.DisableKeyword("SHADOWS_ON");
                Shader.EnableKeyword("SHADOWS_OFF");
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
        }

        private void RenderFog(RenderTexture fogRenderTexture, RenderTexture src)
        { }

        private void BlurFog(RenderTexture fogTarget1, RenderTexture fogTarget2)
        { }

        private void BlendWithScene(RenderTexture source, RenderTexture destination, RenderTexture fogTarget)
        { }

        private void SetMieScattering()
        { }

        private void SetNoiseSource()
        { }

        private void ShaderFeatureHelper(Material mat, string key, bool enable)
        {
            if (enable)
                mat.EnableKeyword(key);
            else
                mat.DisableKeyword(key);
        }
    }
}