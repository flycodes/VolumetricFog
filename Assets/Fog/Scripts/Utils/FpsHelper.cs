using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VolumetricFogExtension
{
    public class FpsHelper : MonoBehaviour
    {
        private float m_FpsValue = 0.0f;
        public float FpsValue { get { return m_FpsValue; } }

        private FpsLevel m_PrevFpsLevel = FpsLevel.Null;

        [SerializeField]
        private FpsLevel m_FpsLevel = FpsLevel.Fps60;

        private int m_AccuFrameCnt = 0;
        private int m_UpdateRate = 4;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;

            m_FpsValue = 0.0f;
        }

        void Update()
        {
            if (m_AccuFrameCnt > m_UpdateRate)
            {
                m_FpsValue = 1.0f / Time.unscaledDeltaTime;
                m_AccuFrameCnt = 0;
            }
            else
            {
                ++m_AccuFrameCnt;
            }

            UpdateFps();
        }

        private void UpdateFps()
        {
            if (m_PrevFpsLevel != m_FpsLevel)
            {
                Debug.LogFormat("Change FpsLevel {0}", m_FpsLevel);
                if (m_FpsLevel == FpsLevel.Fps30)
                    Application.targetFrameRate = 30;
                else if (m_FpsLevel == FpsLevel.Fps60)
                    Application.targetFrameRate = 60;
                else if (m_FpsLevel == FpsLevel.Unlimited)
                    Application.targetFrameRate = -1;

                m_PrevFpsLevel = m_FpsLevel;
            }
        }
    }
}