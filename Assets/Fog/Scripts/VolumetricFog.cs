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
        Texture3DCompute = 4,
        SimplexNoise = 8,
        SimplexNoiseCompute = 16,
    }

    [RequireComponent(typeof(Camera))]
    public class VolumetricFog : MonoBehaviour
    {
        private const RenderTextureFormat FogRenderTextureFormat = RenderTextureFormat.ARGBHalf;

        public Shader calculateFogShader;
        public Shader blurShader;
        public Shader fogShader;

        private Material m_ApplyBlurMaterial;
        private Material m_CalculateFogMaterial;
        private Material m_ApplyFogMaterial;

        public Transform m_Light;

        public List<Light> m_FogLightCasters;

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
        private RenderTexture m_FogTexture3DCompute;
        private RenderTexture m_FogTextureSimplex;

        public Material ApplyFogMaterial
        {
            get
            {
                if (!m_ApplyFogMaterial && fogShader)
                {
                    m_ApplyFogMaterial = new Material(fogShader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                return m_ApplyFogMaterial;
            }
        }

        public Material CalculateFogMaterial
        {
            get
            {
                if (!m_CalculateFogMaterial && calculateFogShader)
                {
                    m_CalculateFogMaterial = new Material(calculateFogShader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                return m_CalculateFogMaterial;
            }
        }

        #endregion

        #region Cache PropertyID

        private static int NoiseTexture, BlueNoiseTexture, NoiseTex3D;
        private static int LightColor, FogColor, FogWorldPosition, LightDir, FogDirection;
        private static int FogDensity, RayleighScatteringCoeff, MieScatteringCoeff,
            ExtinctionCoeff, Anisotropy, KFactor, LightIntensity, FogSize,
            RayMarchingSteps, AmbientFog, BaseHeightDensity, HeightDensityCoeff,
            NoiseScale, FogSpeed;
        private static int InverseProjectionMatrix, InverseViewMatrix;

        private void InitPropertiesID()
        {
            NoiseTexture = Shader.PropertyToID("_NoiseTexture");
            BlueNoiseTexture = Shader.PropertyToID("_BlueNoiseTexture");
            NoiseTex3D = Shader.PropertyToID("_NoiseTex3D");

            LightColor = Shader.PropertyToID("_LightColor");
            FogColor = Shader.PropertyToID("_FogColor");
            FogWorldPosition = Shader.PropertyToID("_FogWorldPosition");
            LightDir = Shader.PropertyToID("_LightDir");
            FogDirection = Shader.PropertyToID("_FogDirection");

            FogDensity = Shader.PropertyToID("_FogDensity");
            RayleighScatteringCoeff = Shader.PropertyToID("_RayleighScatteringCoeff");
            MieScatteringCoeff = Shader.PropertyToID("_MieScatteringCoeff");
            ExtinctionCoeff = Shader.PropertyToID("_ExtinctionCoeff");
            Anisotropy = Shader.PropertyToID("_Anisotropy");
            KFactor = Shader.PropertyToID("_KFactor");
            LightIntensity = Shader.PropertyToID("_LightIntensity");
            FogSize = Shader.PropertyToID("_FogSize");
            RayMarchingSteps = Shader.PropertyToID("_RayMarchingSteps");
            AmbientFog = Shader.PropertyToID("_AmbientFog");
            BaseHeightDensity = Shader.PropertyToID("_BaseHeightDensity");
            HeightDensityCoeff = Shader.PropertyToID("_HeightDensityCoeff");
            NoiseScale = Shader.PropertyToID("_NoiseScale");
            FogSpeed = Shader.PropertyToID("_FogSpeed");
        }

        #endregion Cache PropertyID

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
            InitPropertiesID();
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
    }
}