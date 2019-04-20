using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    private static int FogDensity;

    private void Awake()
    {
        FogDensity = Shader.PropertyToID("_FogDensity");
        Debug.Log(FogDensity);
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
