using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[Serializable]
public struct GrassDictionary
{
    public string meshName;
    public List<GrassDataItem> itemDatas;
}

[Serializable]
public struct GrassDataItem
{
    public Matrix4x4 materix;
    public Vector4 lightmapScaleOffset;
    public int lightmapIndex;
}

public class DrawMeshData
{
    public int id;
    public Mesh mesh;
    public Material material;
    public List<Matrix4x4> materixs;
    public List<Vector4> lightmapOffsets;
    public MaterialPropertyBlock block;
    public bool shadow;
    public bool lightProbe;
}

public class GrassSystem : MonoBehaviour
{
    [SerializeField, Header("草的配置数据")]
    private GrassDataObject grassDataObject;
    [SerializeField, Header("草的模型网格")]
    private Mesh[] meshs;
    [SerializeField, Header("草实际使用的材质")]
    private Material[] materials;
    [SerializeField, Header("光照贴图")]
    private Texture2D lightmapTex;
    [SerializeField, Header("光照贴图的方向")]
    private Texture2D lightmapDir;
    [SerializeField, Header("光照贴图的索引")]
    private int[] lightmapIndexs;
    [SerializeField, Header("草有影子")]
    private bool[] shadows;
    [SerializeField, Header("草受光照探针影响")]
    private bool[] lightProbes;
    [SerializeField, Header("草接受LightMap")]
    private bool[] lightmapOn;
    [SerializeField, Header("草的颜色")]
    private Color[] colors;
    [SerializeField, Header("实际看草的相机，默认为主相机")]
    private Camera viewGrassCamera;
    [SerializeField, Header("场景边界")]
    private Bounds bounds;
    [SerializeField, Header("草的深度")]
    private int depth = 5;
    [SerializeField, Header("显示调试框")]
    private bool debugDraw = false;
    private List<DrawMeshData> drawMeshList = null;
    private List<CullingTreeNode> leaves = null;
    private HashSet<int> grassVisibleIDList = null;
    private Vector3 m_cachedCamPos;
    private Quaternion m_cachedCamRot;
    private Plane[] cameraFrustumPlanes = new Plane[6];
    private CullingTreeNode cullingTree;
    private bool m_Init = false;
    private List<Matrix4x4>[] tmpMaterixsArray;
    private List<Vector4>[] tmpLightmapOffsetArray;

    public const int MAX_INDEX = 10000;
    public static readonly string LIGHTMAPST = "unity_LightmapST";
    public static readonly string LIGHTMAP = "unity_Lightmap";
    public static readonly string LIGHTMAP_KEYWORLD = "LIGHTMAP_ON";

    void Start()
    {
        if(!SystemInfo.supportsInstancing)
        {
            return;
        }
        if (viewGrassCamera == null)
        {
            viewGrassCamera = Camera.main;
        }
        lightmapTex = LightmapSettings.lightmaps[0].lightmapColor;
        lightmapDir = LightmapSettings.lightmaps[0].lightmapDir;
        grassVisibleIDList = new HashSet<int>();
        leaves = new List<CullingTreeNode>();
        drawMeshList = new List<DrawMeshData>();
        List<GrassDictionary> datas = grassDataObject.dataList;
        
        List<GrassDictionary> newDatas = new List<GrassDictionary>();
        int len = datas.Count;
        for (int i = 0; i < datas.Count; i++)
        {
            GrassDictionary data = datas[i];
            int num = data.itemDatas.Count / 1000 + 1;
            for(int j = 0; j < num; j++)
            {
                GrassDictionary gd = new GrassDictionary();
                gd.meshName = data.meshName;
                gd.itemDatas = new List<GrassDataItem>();
                for (int k = j * 1000; k < (j + 1) * 1000 && k < data.itemDatas.Count; k++)
                {
                    gd.itemDatas.Add(data.itemDatas[k]);
                }
                newDatas.Add(gd);
            }
        }
        //Debug.Log("newDatas="+newDatas.Count);
        for (int i = 0; i < newDatas.Count; i++)
        {
            GrassDictionary data = newDatas[i];
            int index = 0;
            DrawMeshData dm = new DrawMeshData();
            dm.mesh = GetMeshByName(newDatas[i].meshName, out index);
            dm.id = i;
            dm.material = materials[index];
            if (lightmapOn[index])
            {
                dm.material.EnableKeyword(LIGHTMAP_KEYWORLD);
            }
            dm.shadow = shadows[index];
            dm.lightProbe = lightProbes[index];
            dm.materixs = GetMatrixList(data.itemDatas);
            dm.lightmapOffsets = GetLightmapOffset(data.itemDatas);
            dm.block = GenerateMaterialProperty(lightmapTex, colors[index], dm.lightmapOffsets);
            drawMeshList.Add(dm);
        }
        // tmpMaterixsArray = new List<Matrix4x4>[drawMeshList.Count];
        // tmpLightmapOffsetArray = new List<Vector4>[drawMeshList.Count];
        // cullingTree = new CullingTreeNode(bounds, depth, false);
        // cullingTree.RetrieveAllLeaves(leaves);
        // for (int i = 0; i < drawMeshList.Count; i++)
        // {
        //     int id = drawMeshList[i].id;
        //     List<Matrix4x4> materixList = drawMeshList[i].materixs;
        //     for (int j = 0; j < materixList.Count; j++)
        //     {
        //         int grassVisibleID = id * MAX_INDEX + j + 1;
        //         cullingTree.FindLeaf(materixList[j].GetColumn(3), grassVisibleID);
        //     }
        //     tmpMaterixsArray[i] = new List<Matrix4x4>();
        //     tmpLightmapOffsetArray[i] = new List<Vector4>();
        // }
        // cullingTree.ClearEmpty();
        SortMatruix();
        m_Init = true;
    }

    private void SortMatruix()
    {
        for (int i = 0; i < drawMeshList.Count; i++)
        {
        }
    }

    private Mesh GetMeshByName(string meshName, out int index)
    {
        for (int i = 0; i < meshs.Length; i++)
        {
            if (meshs[i].name == meshName)
            {
                index = i;
                return meshs[i];
            }
        }
        index = 0;
        return null;
    }

    private List<Matrix4x4> GetMatrixList(List<GrassDataItem> datas)
    {
        List<Matrix4x4> matrixList = new List<Matrix4x4>();
        for (int i = 0; i < datas.Count; i++)
        {
            matrixList.Add(datas[i].materix);
        }
        return matrixList;
    }

    private List<Vector4> GetLightmapOffset(List<GrassDataItem> datas)
    {
        List<Vector4> offsetList = new List<Vector4>();
        for (int i = 0; i < datas.Count; i++)
        {
            offsetList.Add(datas[i].lightmapScaleOffset);
        }
        return offsetList;
    }

    private MaterialPropertyBlock GenerateMaterialProperty(Texture2D lightmapTexture, Color color, List<Vector4> lightmapOffset)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture(LIGHTMAP, lightmapTexture);
        block.SetVectorArray("unity_LightmapST", new Vector4[1023]);
        //block.SetFloat("_Index", lightmapIndex);
        block.SetColor("_Color", color);
        return block;
    }

    void FrustumCulling()
    {
        if (viewGrassCamera == null)
        {
            viewGrassCamera = Camera.main;
        }
        if (viewGrassCamera == null)
        {
            return;
        }
        if (m_cachedCamRot == viewGrassCamera.transform.rotation && m_cachedCamPos == viewGrassCamera.transform.position && Application.isPlaying)
        {
            return;
        }
        GeometryUtility.CalculateFrustumPlanes(viewGrassCamera, cameraFrustumPlanes);
        grassVisibleIDList.Clear();
        #if UNITY_EDITOR
            BoundsListVis.Clear();
            cullingTree.RetrieveLeaves(cameraFrustumPlanes, BoundsListVis, grassVisibleIDList);
        #else
            cullingTree.RetrieveLeaves(cameraFrustumPlanes, null, grassVisibleIDList);
        #endif
        m_cachedCamPos = viewGrassCamera.transform.position;
        m_cachedCamRot = viewGrassCamera.transform.rotation;
    }

    void Update()
    {
        if (m_Init)
        {
            // FrustumCulling();
            // for (int i = 0; i < drawMeshList.Count; i++)
            // {
            //     DrawMeshData data = drawMeshList[i];
            //     LightProbeUsage lightProbeUsage = LightProbeUsage.Off;
            //     if (data.lightProbe)
            //     {
            //         lightProbeUsage = LightProbeUsage.BlendProbes;
            //     }
            //     List<Matrix4x4> tmpMaterixs = tmpMaterixsArray[i];
            //     List<Vector4> tmpLightmapOffset = tmpLightmapOffsetArray[i];
            //     tmpMaterixs.Clear();
            //     tmpLightmapOffset.Clear();
            //     List<Matrix4x4> matrixList = data.materixs;
            //     for (int j = 0; j < matrixList.Count; j++)
            //     {
            //         int grassVisibleID = data.id * MAX_INDEX + j + 1;
            //         if (grassVisibleIDList.Contains(grassVisibleID))
            //         {
            //             tmpMaterixs.Add(data.materixs[j]);
            //             tmpLightmapOffset.Add(data.lightmapOffsets[j]);
            //         }
            //     }
            //     if(tmpMaterixs.Count > 1000)
            //     {
            //         Debug.Log("相机中超过1000个草，API不支持绘画超过1000个实例，修改草的数量或降低相机中可以看到的草的密度");
            //         return;
            //     }
            //     if (tmpMaterixs.Count > 0)
            //     {
            //         data.block.SetVectorArray(LIGHTMAPST, tmpLightmapOffset.ToArray());
            //     Graphics.DrawMeshInstanced(data.mesh, 0, data.material, tmpMaterixs, data.block, ShadowCastingMode.Off, data.shadow, 0, null, lightProbeUsage);
            
            //     }
            // }

            for (int i = 0; i < drawMeshList.Count; i++)
            {
                DrawMeshData data = drawMeshList[i];
                Graphics.DrawMeshInstanced(data.mesh, 0, data.material, data.materixs, data.block, ShadowCastingMode.Off, data.shadow, 0, null, LightProbeUsage.Off);
            }
        }
    }

    #if UNITY_EDITOR
    
    private List<Bounds> BoundsListVis = new List<Bounds>();
    void OnDrawGizmos()
    {
        if (debugDraw)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            for (int i = 0; i < BoundsListVis.Count; i++)
            {
                Gizmos.DrawWireCube(BoundsListVis[i].center, BoundsListVis[i].size);
            }
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
    #endif

}
