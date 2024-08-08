using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
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
    private GrassDataObject m_GrassDataObject;
    [SerializeField, Header("草的模型网格")]
    private Mesh[] m_Meshs;
    [SerializeField, Header("草实际使用的材质")]
    private Material[] m_Materials;
    [SerializeField, Header("光照贴图")]
    private Texture2D m_LightmapTex;
    [SerializeField, Header("光照贴图的方向")]
    private Texture2D m_LightmapDir;
    [SerializeField, Header("光照贴图的索引")]
    private int[] m_LightmapIndexs;
    [SerializeField, Header("草有影子")]
    private bool[] m_Shadows;
    [SerializeField, Header("草受光照探针影响")]
    private bool[] m_LightProbes;
    [SerializeField, Header("草接受LightMap")]
    private bool[] m_LightmapOn;
    [SerializeField, Header("草的颜色")]
    private Color[] m_Colors;
    [SerializeField, Header("实际看草的相机，默认为主相机")]
    private Camera m_ViewGrassCamera;
    [SerializeField, Header("场景边界")]
    private Bounds m_Bounds;
    [SerializeField, Header("草的深度")]
    private int m_Depth = 5;
    [SerializeField, Header("显示调试框")]
    private bool m_DebugDraw = false;
    private List<DrawMeshData> _DrawMeshList = null;
    private List<CullingTreeNode> _Leaves = null;
    private HashSet<int> _GrassVisibleIDList = null;
    private Vector3 _CachedCamPos;
    private Quaternion _CachedCamRot;
    private Plane[] _CameraFrustumPlanes = new Plane[6];
    private CullingTreeNode _CullingTree;
    private bool _Init = false;
    private List<Matrix4x4>[] _TmpMaterixsArray;
    private List<Vector4>[] _TmpLightmapOffsetArray;
    private bool _UseOcTree = true;//是否使用OcTree来进行裁剪
    private bool _FirstRender = true;
    public const int MAX_INDEX = 10000;
    public static readonly string LIGHTMAPST = "unity_LightmapST";
    public static readonly string LIGHTMAP = "unity_Lightmap";
    public static readonly string LIGHTMAP_KEYWORLD = "LIGHTMAP_ON";
    public static readonly string LIGHTMAPDIR_KEYWORLD = "DIRLIGHTMAP_COMBINED";

    private bool _UseTextureArray = true;//是否使用Texture2DArray来存储光照贴图和方向图，需要使用自定义mpb的shader
    private Texture2DArray _TextureArray;

    void Start()
    {
        if(!SystemInfo.supportsInstancing)
        {
            return;
        }
        if (m_ViewGrassCamera == null)
        {
            m_ViewGrassCamera = Camera.main;
        }



        m_LightmapTex = LightmapSettings.lightmaps[0].lightmapColor;
        m_LightmapDir = LightmapSettings.lightmaps[0].lightmapDir;

        //构建一个texture2DArray
        if(_UseTextureArray)
        {
            int textureWidth = m_LightmapTex.width;
            int textureHeight = m_LightmapTex.height;
            TextureFormat textFormat = m_LightmapTex.format;
            _TextureArray = new Texture2DArray(textureWidth, textureHeight, 2, textFormat, false);
            Graphics.CopyTexture(m_LightmapTex, 0, 0, _TextureArray, 0, 0);
            Graphics.CopyTexture(m_LightmapDir, 0, 0, _TextureArray, 1, 0);


        // NativeArray<byte> pixelData1 = m_LightmapTex.GetPixelData<byte>(0);
        // NativeArray<byte> pixelData2 = m_LightmapDir.GetPixelData<byte>(0);
                
        // _TextureArray.SetPixelData(pixelData1, 0, 0, 0);
        // _TextureArray.SetPixelData(pixelData2, 0, 1, 0);

        _TextureArray.Apply(false, false);

        }

        _GrassVisibleIDList = new HashSet<int>();
        _Leaves = new List<CullingTreeNode>();
        _DrawMeshList = new List<DrawMeshData>();
        List<GrassDictionary> datas = m_GrassDataObject.dataList;
        
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
            dm.material = m_Materials[index];
            if (m_LightmapOn[index])
            {
                dm.material.EnableKeyword(LIGHTMAP_KEYWORLD);
                dm.material.EnableKeyword(LIGHTMAPDIR_KEYWORLD);
                if(_UseTextureArray)
                {
                    dm.material.SetTexture("_Textures", _TextureArray);
                }
            }
            dm.shadow = m_Shadows[index];
            dm.lightProbe = m_LightProbes[index];
            dm.materixs = GetMatrixList(data.itemDatas);
            dm.lightmapOffsets = GetLightmapOffset(data.itemDatas);
            dm.block = GenerateMaterialProperty(m_Colors[index], dm.lightmapOffsets);
            _DrawMeshList.Add(dm);
        }
        if(_UseOcTree)
        {
            _TmpMaterixsArray = new List<Matrix4x4>[_DrawMeshList.Count];
            _TmpLightmapOffsetArray = new List<Vector4>[_DrawMeshList.Count];
            _CullingTree = new CullingTreeNode(m_Bounds, m_Depth, false);
            _CullingTree.RetrieveAllLeaves(_Leaves);
            for (int i = 0; i < _DrawMeshList.Count; i++)
            {
                int id = _DrawMeshList[i].id;
                List<Matrix4x4> materixList = _DrawMeshList[i].materixs;
                for (int j = 0; j < materixList.Count; j++)
                {
                    int grassVisibleID = id * MAX_INDEX + j + 1;
                    _CullingTree.FindLeaf(materixList[j].GetColumn(3), grassVisibleID);
                }
                _TmpMaterixsArray[i] = new List<Matrix4x4>();
                _TmpLightmapOffsetArray[i] = new List<Vector4>();
            }
            _CullingTree.ClearEmpty();
        }
        _Init = true;
    }

    private Mesh GetMeshByName(string meshName, out int index)
    {
        for (int i = 0; i < m_Meshs.Length; i++)
        {
            if (m_Meshs[i].name == meshName)
            {
                index = i;
                return m_Meshs[i];
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

    private MaterialPropertyBlock GenerateMaterialProperty(Color color, List<Vector4> lightmapOffset)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        if(_UseTextureArray)
        {
            //现在的lightmap只有一张，所以没有使用这个参数
            block.SetFloat("_TextureIndex", 0);
            block.SetVectorArray("_LightmapST", new Vector4[1023]);
        }
        else
        {
            block.SetTexture(LIGHTMAP, m_LightmapTex);
            block.SetVectorArray(LIGHTMAPST, new Vector4[1023]);
        }
        block.SetColor("_Color", color);
        return block;
    }

    void FrustumCulling()
    {
        if (m_ViewGrassCamera == null)
        {
            m_ViewGrassCamera = Camera.main;
        }
        if (m_ViewGrassCamera == null)
        {
            return;
        }
        if (!_FirstRender && _CachedCamRot == m_ViewGrassCamera.transform.rotation && _CachedCamPos == m_ViewGrassCamera.transform.position && Application.isPlaying)
        {
            return;
        }
        GeometryUtility.CalculateFrustumPlanes(m_ViewGrassCamera, _CameraFrustumPlanes);
        _GrassVisibleIDList.Clear();
        #if UNITY_EDITOR
            BoundsListVis.Clear();
            _CullingTree.RetrieveLeaves(_CameraFrustumPlanes, BoundsListVis, _GrassVisibleIDList);
        #else
            cullingTree.RetrieveLeaves(cameraFrustumPlanes, null, grassVisibleIDList);
        #endif
        _CachedCamPos = m_ViewGrassCamera.transform.position;
        _CachedCamRot = m_ViewGrassCamera.transform.rotation;
        if (_GrassVisibleIDList.Count > 0)
        {
            _FirstRender = false;
        }
    }

    void Update()
    {
        if (_Init)
        {
            if (_UseOcTree)
            {
                FrustumCulling();
                for (int i = 0; i < _DrawMeshList.Count; i++)
                {
                    DrawMeshData data = _DrawMeshList[i];
                    LightProbeUsage lightProbeUsage = LightProbeUsage.Off;
                    if (data.lightProbe)
                    {
                        lightProbeUsage = LightProbeUsage.BlendProbes;
                    }
                    List<Matrix4x4> tmpMaterixs = _TmpMaterixsArray[i];
                    List<Vector4> tmpLightmapOffset = _TmpLightmapOffsetArray[i];
                    tmpMaterixs.Clear();
                    tmpLightmapOffset.Clear();
                    List<Matrix4x4> matrixList = data.materixs;
                    for (int j = 0; j < matrixList.Count; j++)
                    {
                        int grassVisibleID = data.id * MAX_INDEX + j + 1;
                        if (_GrassVisibleIDList.Contains(grassVisibleID))
                        {
                            tmpMaterixs.Add(data.materixs[j]);
                            tmpLightmapOffset.Add(data.lightmapOffsets[j]);
                        }
                    }
                    if (tmpMaterixs.Count > 1000)
                    {
                        Debug.Log("相机中超过1000个草，API不支持绘画超过1000个实例，修改草的数量或降低相机中可以看到的草的密度");
                        return;
                    }
                    if (tmpMaterixs.Count > 0)
                    {
                        if(_UseTextureArray)
                        {
                            data.block.SetVectorArray("_LightmapST", tmpLightmapOffset.ToArray());
                        }
                        else
                        {
                            data.block.SetVectorArray(LIGHTMAPST, tmpLightmapOffset.ToArray());
                        }
                        Graphics.DrawMeshInstanced(data.mesh, 0, data.material, tmpMaterixs, data.block, ShadowCastingMode.Off, data.shadow, 0, null, lightProbeUsage);

                    }
                }
            }
            if(!_UseOcTree)
            {
                for (int i = 0; i < _DrawMeshList.Count; i++)
                {
                    DrawMeshData data = _DrawMeshList[i];
                    Graphics.DrawMeshInstanced(data.mesh, 0, data.material, data.materixs, data.block, ShadowCastingMode.Off, data.shadow, 0, null, LightProbeUsage.Off);
                }
            }
        }
    }

    #if UNITY_EDITOR
    
    private List<Bounds> BoundsListVis = new List<Bounds>();
    void OnDrawGizmos()
    {
        if (m_DebugDraw)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            for (int i = 0; i < BoundsListVis.Count; i++)
            {
                Gizmos.DrawWireCube(BoundsListVis[i].center, BoundsListVis[i].size);
            }
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireCube(m_Bounds.center, m_Bounds.size);
        }
    }
    #endif

}
