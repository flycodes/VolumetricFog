using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VolumetricFogExtension
{
    static class ShaderIDs
    {
        internal static readonly int NoiseTexture = Shader.PropertyToID("_NoiseTexture");
        internal static readonly int BlurNoiseTexture = Shader.PropertyToID("_BlurNoiseTexture");
        internal static readonly int NoiseTex3D = Shader.PropertyToID("_NoiseTex3D");

        internal static readonly int LightColor = Shader.PropertyToID("_LightColor");
        internal static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        internal static readonly int FogColor = Shader.PropertyToID("_FogColor");
        internal static readonly int FogWorldPosition = Shader.PropertyToID("_FogWorldPosition");
        internal static readonly int LightDir = Shader.PropertyToID("_LightDir");
        internal static readonly int FogDirection = Shader.PropertyToID("_FogDirection");
        internal static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
        internal static readonly int RayleighScatteringCoeff = Shader.PropertyToID("_RayleighScatteringCoeff");
        internal static readonly int MieScatteringCoeff = Shader.PropertyToID("_MieScatteringCoeff");
        internal static readonly int ExtinctionCoeff = Shader.PropertyToID("_ExtinctionCoeff");
        internal static readonly int Anisotropy = Shader.PropertyToID("_Anisotropy");
        internal static readonly int KFactor = Shader.PropertyToID("_KFactor");
        internal static readonly int LightIntensity = Shader.PropertyToID("_LightIntensity");
        internal static readonly int FogSize = Shader.PropertyToID("_FogSize");
        internal static readonly int RayMarchingSteps = Shader.PropertyToID("_RayMarchingSteps");
        internal static readonly int AmbientFog = Shader.PropertyToID("_AmbientFog");
        internal static readonly int BaseHeightDensity = Shader.PropertyToID("_BaseHeightDensity");
        internal static readonly int HeightDensityCoeff = Shader.PropertyToID("_HeightDensityCoeff");
        internal static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        internal static readonly int FogSpeed = Shader.PropertyToID("_FogSpeed");

        // ApplyBlur
        internal static readonly int BlurOffsets = Shader.PropertyToID("_BlurOffsets");
        internal static readonly int BlurWeights = Shader.PropertyToID("_BlurWeights");
        internal static readonly int BlurDepthFalloff = Shader.PropertyToID("_BlurDepthFalloff");
        internal static readonly int BlurDir = Shader.PropertyToID("_BlurDir");

        // ApplyFog
        internal static readonly int FogRendertargetLinear = Shader.PropertyToID("_FogRendertargetLinear");

        internal static readonly int InverseProjectionMatrix = Shader.PropertyToID("_InverseProjectionMatrix");
        internal static readonly int InverseViewMatrix = Shader.PropertyToID("_InverseViewMatrix");
    }
}