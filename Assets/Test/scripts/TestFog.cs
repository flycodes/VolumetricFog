using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestFog : MonoBehaviour
{
    public Material m_TestFogMat;

    public Camera m_Camera;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Shader.SetGlobalMatrix(ShaderIDs.InverseProjectionMatrix, m_Camera.projectionMatrix.inverse);
    }
}
