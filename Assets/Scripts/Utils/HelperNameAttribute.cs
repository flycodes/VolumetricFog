using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelperNameAttribute : PropertyAttribute
{
    public string NewName { get; private set; }
    public HelperNameAttribute(string name)
    {
        NewName = name;
    }
}
