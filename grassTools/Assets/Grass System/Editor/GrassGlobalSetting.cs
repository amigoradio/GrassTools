using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GrassGlobalSetting : ScriptableObject
{
    //笔刷大小
    public int brushSize = 10;
    //草的scale范围最小值
    public float scaleRandomMin = 1f;
    //草的scale范围最大值
    public float scaleRandomMax = 1f;
    //密度
    public float density = 0.5f;
    //草种植的层
    public LayerMask hitMask = 1;
    //草的层
    public int grassLayer = 1;
    //草数据保存的目录
    public string grassDataPath = "Assets/GrassData/";
    //刷的草的父节点的名称
    public string grassRootName = "GrassRoot";
    //草数据的后缀:场景名_GrassData.asset
    public string grassDataNameSuffix = "_GrassData.asset";
    //风的速度
    public float windSpeed = 0f;
    //风的强度
    public float windStrength = 0f;

}
