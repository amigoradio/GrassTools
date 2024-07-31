using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GrassGlobalSetting : ScriptableObject
{
    public int defaultBrushSize = 10;
    public LayerMask defaultHitMask = 1;
    public int defaultGrassLayer = 1;
    public string defaultGrassDataPath = "Assets/GrassData/";
}
