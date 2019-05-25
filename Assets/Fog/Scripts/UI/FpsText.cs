using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FpsText : MonoBehaviour
{
    [SerializeField]
    private VolumetricFogExtension.FpsHelper m_FpsHelper;

    private Text m_UIText;

    private void Awake()
    {
        m_UIText = GetComponent<Text>();
    }

    private void Update()
    {
        m_UIText.text = m_FpsHelper.FpsValue.ToString("0.00");
    }
}
