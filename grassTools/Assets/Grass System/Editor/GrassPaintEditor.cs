using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditorInternal;
using Unity.EditorCoroutines.Editor;
using System.IO;

public struct GrassPaintData
{
    public GameObject obj;
    public Vector3 position;
}

public class GrassPaintEditor : EditorWindow
{
    [MenuItem("Tools/刷草/创建全局配置文件", false, 1)]
    public static void OpenGrassConfig()
    {
        FileInfo f = new FileInfo(GlobalSettingPath);
        if (f.Exists)
        {
            File.Delete(GlobalSettingPath);
            File.Delete(GlobalSettingPath + ".meta");
            AssetDatabase.Refresh();
        }
        GrassGlobalSetting settings = ScriptableObject.CreateInstance<GrassGlobalSetting>();
        AssetDatabase.CreateAsset(settings, GlobalSettingPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

   [MenuItem("Tools/刷草/刷草工具 %g", false, 2)]
	static void Open()
	{
	    var window = (GrassPaintEditor) EditorWindow.GetWindowWithRect(typeof(GrassPaintEditor), new Rect(0, 0, 386,520), false, "Paint Grass");
	    window.Show();   
	}

    public static readonly string GlobalSettingPath = "Assets/GlobalSetting.asset";

    private GameObject _AddObject;
    private GameObject[] _Plants = new GameObject[6];
    private Texture[] _TexObjects = new Texture[6];
    private int _PlantSelect = 0;
    private int _BrushSize = 5;
    private float _ScaleRandomMin = 1f;
    private float _ScaleRandomMax = 1f;
    private float _Density = 0.5f;
    private LayerMask _HitMask;
    private int _GrassLayer;
    private float _WindSpeed;
    private float _WindStrength;
    private int _GrassAmount = 0;
    private GameObject _GrassRoot;
    private bool _FaceToCamera;
    private bool _PaintActive = true;
    private bool _IsRandomRotate = true;
    private Camera _MainCamera;
    private RaycastHit[] _Results = new RaycastHit[1];
    private Vector3 _HitPos;
    private Vector3 _HitNormal;
    private Vector3 _CachedPos;
    private int _SelectFunction = 0;
    private List<GrassPaintData> _GrassDatas = new List<GrassPaintData>();



    // private static string GrassDataPath = "Assets/Game/GrassData/";
    // private static string GrassRootName = "GrassRoot";
    // private static string GrassDataNameSuffix = "_GrassData.asset";

    private GrassGlobalSetting _Settings;

    private void OnEnable()
    {
        _MainCamera = Camera.main;
        if(_MainCamera == null)
        {
            Debug.LogError("场景没有主相机");
        }
        //_HitMask = LayerMask.GetMask("Ground");
        _Settings = (GrassGlobalSetting)AssetDatabase.LoadAssetAtPath(GlobalSettingPath, typeof(GrassGlobalSetting));
        if(_Settings == null)
        {
            Debug.LogError("没有找到全局配置文件");
        }
        else
        {
            _BrushSize = _Settings.brushSize;
            _ScaleRandomMin = _Settings.scaleRandomMin;
            _ScaleRandomMax = _Settings.scaleRandomMax;
            _Density = _Settings.density;
            _HitMask = _Settings.hitMask;
            _GrassLayer = _Settings.grassLayer;
            _WindSpeed = _Settings.windSpeed;
            _WindStrength = _Settings.windStrength;
            AddExistGrassToData();
        }
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += this.HandleUndo;
    }

    /// <summary>
    /// 重新打开刷草工具后，找到场景中之前刷过的草，将这些草加入到数据中方便工具管理
    /// </summary>
    private void AddExistGrassToData()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject obj in roots)
        {
            if (obj.name.Contains(_Settings.grassRootName))
            {
                int len = obj.transform.childCount;
                for (int i = 0; i < len; i++)
                {
                    Transform child = obj.transform.GetChild(i);
                        GrassPaintData data = new GrassPaintData();
                        data.obj = child.gameObject;
                        data.position = child.position;
                        _GrassDatas.Add(data);
                }
            }
        }
        _GrassAmount = _GrassDatas.Count;
    }

    private void RemoveDelegates()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= this.HandleUndo;
    }

    void OnDisable()
    {
        RemoveDelegates();
    }

    void OnDestroy()
    {
        RemoveDelegates();
    }

    void OnGUI()
    {
        if(_GrassDatas != null)
        {
            _GrassAmount = _GrassDatas.Count;
        }
        else
        {
            _GrassDatas = new List<GrassPaintData>();
        }
        
        GameObject curSelectObj = Selection.activeGameObject;
        if(curSelectObj != null && curSelectObj.name.Contains(_Settings.grassRootName))
        {
            _GrassRoot = curSelectObj;
        }
        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        GUILayout.BeginHorizontal();
        GUILayout.Label("添加prefab", GUILayout.Width(125));
        
        _AddObject = (GameObject)EditorGUILayout.ObjectField("", _AddObject, typeof(GameObject), true, GUILayout.Width(160));
        if (GUILayout.Button("+", GUILayout.Width(40)))
        {
            for (int i = 0; i < 6; i++)
            { 
                if (_Plants[i] == null)
                {
                    _Plants[i] = _AddObject;
                    break;
                }
            }
        }
        
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        for (int i = 0; i < 6; i++)
        {
            if (_Plants[i] != null)
                _TexObjects[i] = AssetPreview.GetAssetPreview(_Plants[i]) as Texture;
            else 
                _TexObjects[i] = null;
        }
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        _PlantSelect = GUILayout.SelectionGrid(_PlantSelect, _TexObjects, 6, "gridlist", GUILayout.Width(330), GUILayout.Height(55));

        GUILayout.BeginHorizontal();

        for (int i = 0; i < 6; i++)
        {
            if (GUILayout.Button("—", GUILayout.Width(52)))
            {
                _Plants[i] = null;
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        _PaintActive = EditorGUILayout.Toggle("开始绘制", _PaintActive);
        GUILayout.BeginHorizontal();
        GUILayout.Label("设置", GUILayout.Width(145));        
        GUILayout.EndHorizontal();
        _FaceToCamera = EditorGUILayout.Toggle("面向相机", _FaceToCamera);
        _IsRandomRotate = EditorGUILayout.Toggle("随机旋转", _FaceToCamera? false : _IsRandomRotate);
        _BrushSize = (int)EditorGUILayout.Slider("笔刷大小（1为单棵草）", _BrushSize, 1, 30);
        _ScaleRandomMin = EditorGUILayout.Slider("随机缩放最小值", _ScaleRandomMin, 0.1f, 2f);
        _ScaleRandomMax = EditorGUILayout.Slider("随机缩放最大值", _ScaleRandomMax, 0.1f, 2f);
        _Density = EditorGUILayout.Slider("密度", _Density, 0, 10);
        LayerMask tempMask = EditorGUILayout.MaskField("检测的层", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(_HitMask), InternalEditorUtility.layers);
        _HitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        _GrassLayer = EditorGUILayout.LayerField("草的层", _GrassLayer);
        EditorGUILayout.Separator();
        GUILayout.Label("风的设置", GUILayout.Width(145)); 
        _WindSpeed = EditorGUILayout.Slider("风速", _WindSpeed, -2f, 2f);
        _WindStrength = EditorGUILayout.Slider("风强度", _WindStrength, -2f, 2f);

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("创建节点"))
        {
            _GrassRoot = new GameObject(_Settings.grassRootName);
            _GrassRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _GrassRoot.transform.localScale = Vector3.one;
        }
        if (GUILayout.Button("添加"))
        {
            _SelectFunction = 1;
        }
        if (GUILayout.Button("删除"))
        {
            _SelectFunction = 2;
        }
        if(GUILayout.Button("清空"))
        {
            if (EditorUtility.DisplayDialog("清除所有草", "是不是要删除所有刷的草？", "删除", "不删"))
            {
                _SelectFunction = 0;
                ClearMesh();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        EditorGUILayout.LabelField("草的总数量: " + _GrassAmount.ToString(), EditorStyles.label);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("收集草数据"))
        {
            CollectGrassData();
        }
        if (GUILayout.Button("还原草数据"))
        {
            RestoreGrassData();
        }
        if (GUILayout.Button("添加草的管理系统"))
        {
            GameObject obj = new GameObject("GrassSystem");
            obj.AddComponent<GrassSystem>();
        }
        GUILayout.EndHorizontal();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (_PaintActive)
        {
            DrawHandles();
            if (_SelectFunction == 1 || _SelectFunction == 2)
            {
                Event e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Planting();
                }
            }
        }
    }

    /// <summary>
    /// 显示笔刷的圆环
    /// </summary>
    void DrawHandles()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        int hits = Physics.RaycastNonAlloc(ray, _Results, Mathf.Infinity, _HitMask);
        for (int i = 0; i < hits; i++)
        {
            _HitPos = _Results[i].point;
            _HitNormal = _Results[i].normal;
        }
        Color discColor = Color.yellow;
        Color discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);

        switch (_SelectFunction)
        {
            case 1:
                discColor = Color.green;
                discColor2 = new Color(0f, 0.5f, 0f, 0.4f);
                break;
            case 2:
                discColor = Color.red;
                discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                break;
            case 3:
                discColor = Color.cyan;
                discColor2 = new(0, 0.5f, 0.5f, 0.4f);
                break;
        }
        Handles.color = discColor;
        Handles.DrawWireDisc(_HitPos, _HitNormal, _BrushSize);
        Handles.color = discColor2;
        Handles.DrawSolidDisc(_HitPos, _HitNormal, _BrushSize);
        if (_HitPos != _CachedPos)
        {
            SceneView.RepaintAll();
            _CachedPos = _HitPos;
        }
    }

    /// <summary>
    /// 收集草的数据，只收集GrassRoot下的草
    /// </summary>
    private void CollectGrassData()
    {
        Scene scene = SceneManager.GetActiveScene();
        string dataName = scene.name + _Settings.grassDataNameSuffix;
        DirectoryInfo dir = new DirectoryInfo(_Settings.grassDataPath);
        if (!dir.Exists)
        {
            dir.Create();
        }
        string savePath = Path.Combine(_Settings.grassDataPath, dataName).Replace('\\', '/');
        FileInfo f = new FileInfo(savePath);
        if (f.Exists)
        {
            File.Delete(savePath);
            File.Delete(savePath + ".meta");
            AssetDatabase.Refresh();
        }
        GrassDataObject datas = ScriptableObject.CreateInstance<GrassDataObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject obj in roots)
        {
            if (obj.name.Contains(_Settings.grassRootName))
            {
                MeshRenderer[] mr = obj.GetComponentsInChildren<MeshRenderer>();
                foreach (var item in mr)
                {
                    MeshFilter mf = item.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        GrassDataItem gd = new GrassDataItem();
                        gd.materix = Matrix4x4.TRS(item.transform.position, item.transform.rotation, item.transform.localScale);
                        gd.lightmapScaleOffset = item.lightmapScaleOffset;
                        gd.lightmapIndex = item.lightmapIndex;
                        datas.AddGrassData(mf.sharedMesh.name, gd);
                    }
                }
            }
        }
        AssetDatabase.CreateAsset(datas, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 从保存的数据中恢复草
    /// </summary>
    private void RestoreGrassData()
    {
        Scene scene = SceneManager.GetActiveScene();
        string dataName = scene.name;
        string loadPath = Path.Combine(_Settings.grassDataPath, dataName + _Settings.grassDataNameSuffix).Replace('\\', '/');
        FileInfo f = new FileInfo(loadPath);
        if (f.Exists)
        {
            GrassDataObject datas = (GrassDataObject)AssetDatabase.LoadAssetAtPath(loadPath, typeof(GrassDataObject));
            if (datas != null)
            {
                _GrassDatas.Clear();
                for (int i = 0; i < datas.dataList.Count; i++)
                {
                    _GrassRoot = new GameObject(_Settings.grassRootName);
                    _GrassRoot.transform.position = Vector3.zero;
                    _GrassRoot.transform.rotation = Quaternion.identity;
                    _GrassRoot.transform.localScale = Vector3.one;
                    GrassDictionary data = datas.dataList[i];
                    GameObject prefab = null;
                    for (int j = 0; j < _Plants.Length; j++)
                    {
                        GameObject obj = _Plants[j];
                        MeshRenderer mr = obj.GetComponentInChildren<MeshRenderer>();
                        if (mr != null)
                        {
                            MeshFilter mf = mr.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == data.meshName)
                            {
                                prefab = obj;
                                break;
                            }
                        }
                    }
                    if (prefab != null)
                    {
                        for (int j = 0; j < data.itemDatas.Count; j++)
                        {
                            GrassDataItem dataItem = data.itemDatas[j];
                            GameObject go = new GameObject(prefab.name + "_" + j.ToString());
                            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                            go.transform.localScale = Vector3.one;
                            go.transform.SetParent(_GrassRoot.transform);
                            go.transform.position = GetPositionFromMatrix(dataItem.materix);
                            go.transform.rotation = GetRotationFromMatrix(dataItem.materix);
                            go.transform.localScale = GetScaleFromMatrix(dataItem.materix);
                            GameObject newPlant = Instantiate(prefab);
                            newPlant.transform.SetParent(go.transform);
                            newPlant.name = "Grass";
                            SetLayerRecursively(go, _GrassLayer);
                            if (_MainCamera != null && _FaceToCamera)
                            {
                                newPlant.transform.LookAt(_MainCamera.transform);
                            }
                            GrassPaintData newData = new GrassPaintData();
                            newData.position = go.transform.position;
                            newData.obj = go;
                            _GrassDatas.Add(newData);
                        }
                    }
                    else
                    {
                        Debug.LogError("没有找到对应的prefab");
                    }
                }
            }
        }
        else
        {
            Debug.Log("数据文件不已经存在！");
        }
    }

    private Vector3 GetPositionFromMatrix(Matrix4x4 matrix)
    {
        Vector3 pos = matrix.GetColumn(3);
        return pos;
    }

    private Quaternion GetRotationFromMatrix(Matrix4x4 matrix)
    {
        Quaternion rot = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        return rot;
    }

    private Vector3 GetScaleFromMatrix(Matrix4x4 matrix)
    {
        Vector3 scale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
        return scale;
    }


    /// <summary>
    /// 从鼠标点击位置向下打射线，打到ground层的物体，生成草
    /// 
    /// </summary>
    private void Planting()
    {
    	Event e = Event.current;                        
        RaycastHit raycastHit;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, _HitMask))
        {
            Vector3 hitPos = raycastHit.point;
            if (_SelectFunction == 1)
            {
                GameObject meshObj = _Plants[_PlantSelect];
                if (meshObj == null)
                {
                    Debug.LogError("没有选择草");
                    return;
                }
                if (_BrushSize > 1)
                {
                    Mesh mesh = meshObj.GetComponentInChildren<MeshFilter>().sharedMesh;
                    Bounds bounds = mesh.bounds;
                    int grassCount = CaculateGrassNum(bounds);
                    //Debug.Log("生成草数量=" + grassCount);
                    if (grassCount > 1000)
                    {
                        Debug.LogError("一次性绘制1000棵草，数量太多了，请确认下配置");
                        return;
                    }
                    int index = 0;
                    List<Bounds> grassBounds = new List<Bounds>();
                    for (int i = 0; i < grassCount;)
                    {
                        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * _BrushSize;
                        Ray ray2 = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        ray2.origin += new Vector3(randomOffset.x, 0, randomOffset.y);
                        if (Physics.Raycast(ray2, out raycastHit, Mathf.Infinity, _HitMask))
                        {
                            float x = Mathf.Max(0.2f, bounds.size.x);
                            float z = Mathf.Max(0.2f, bounds.size.z);
                            Vector3 halfSize = new Vector3(x * 0.5f, 0.2f, z * 0.5f);
                            Bounds newb = new Bounds(raycastHit.point, halfSize);
                            if (!CheckOverlapGrass(newb, grassBounds))
                            {
                                //Debug.Log("生成草=" + i.ToString());
                                Vector3 pos = raycastHit.point;
                                Vector3 normal = raycastHit.normal;
                                Bounds b = new Bounds(pos, halfSize);
                                grassBounds.Add(b);
                                GenerateGrass(pos, normal);
                                i++;
                            }
                            else
                            {
                                index++;
                                if (index > 10)
                                {
                                    i++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Vector3 pos = raycastHit.point;
                    Vector3 normal = raycastHit.normal;
                    GenerateGrass(pos, normal);
                }
            }
            else
            {
                RemoveGrass(hitPos);
            }
        }
	}

    private bool CheckOverlapGrass(Bounds newb, List<Bounds> grassBounds)
    {
        for (int i = 0; i < grassBounds.Count; i++)
        {
            if (grassBounds[i].Intersects(newb))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 用笔刷的面积/草的mesh的面积，得出应该刷的草的数量，再*密度
    /// </summary>
    /// <returns></returns>
    private int CaculateGrassNum(Bounds bounds)
    {
        float circleArea = _BrushSize * _BrushSize * Mathf.PI;
        float width = Mathf.Max(0.2f, bounds.size.x);
        float longer = Mathf.Max(0.2f, bounds.size.z);
        float areaNumf = circleArea / (width * longer);
        int areaNum = Mathf.FloorToInt(areaNumf * _Density);
        if(areaNum < 1)
        {
            areaNum = 1;
        }
        return areaNum;
    }

    /// <summary>
    /// 生成一颗草,设置草的层级，面向相机，随机缩放
    /// </summary>
    /// <param name="hitPos"></param>
    /// <param name="hitNormal"></param>
    private void GenerateGrass(Vector3 hitPos, Vector3 hitNormal)
    {
        GameObject prefab = _Plants[_PlantSelect];
        if (prefab == null)
        {
            Debug.LogError("选择的草找不到");
            return;
        }
        GameObject newPlant = Instantiate(prefab);
        newPlant.transform.SetParent(_GrassRoot.transform);
        newPlant.transform.position = hitPos;
        Vector3 rotUp = Vector3.ProjectOnPlane(newPlant.transform.forward, hitNormal);
        Quaternion rot = Quaternion.LookRotation(rotUp,hitNormal);
        newPlant.transform.rotation = rot;
        if (_IsRandomRotate)
        {
            newPlant.transform.rotation *= Quaternion.AngleAxis(Random.Range(0, 360), Vector3.up); 
        }
        if(_ScaleRandomMin == _ScaleRandomMax)
        {
            newPlant.transform.localScale = Vector3.one * _ScaleRandomMin;
        }
        else
        {
            newPlant.transform.localScale = Vector3.one * UnityEngine.Random.Range(_ScaleRandomMin, _ScaleRandomMax);
        }
        newPlant.name = prefab.name;
        SetLayerRecursively(newPlant, _GrassLayer);
        if(_MainCamera != null && _FaceToCamera)
        {
            newPlant.transform.LookAt(_MainCamera.transform);
        }
        Undo.RegisterCompleteObjectUndo(newPlant, "Add Grass");
        GrassPaintData newData = new GrassPaintData();
        newData.position = hitPos;
        newData.obj = newPlant;
        _GrassDatas.Add(newData);
    }

    /// <summary>
    /// 循环设置物体的层级
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="layer"></param>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// 移除与射线点距离在半径内的草
    /// </summary>
    /// <param name="terrainHit"></param>
    /// <param name="e"></param>
    private void RemoveGrass(Vector3 hitPos)
    {
        for (int i = _GrassDatas.Count - 1; i >= 0 ; i--)
        {
            GrassPaintData data = _GrassDatas[i];
            GameObject selectedObj = _Plants[_PlantSelect];
            if (data.obj.name == selectedObj.name)
            {
                if (Vector3.Distance(hitPos, _GrassDatas[i].position) < _BrushSize)
                {
                    Undo.DestroyObjectImmediate(_GrassDatas[i].obj);
                    DestroyImmediate(_GrassDatas[i].obj);
                    _GrassDatas.RemoveAt(i);
                    _GrassAmount--;
                }
            }
        }
    }

    public void HandleUndo()
    {
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 清除所有已经刷的草
    /// </summary>
    public void ClearMesh()
    {
        for (int i = _GrassDatas.Count - 1; i >= 0; i--)
        {
            GrassPaintData data = _GrassDatas[i];
            GameObject selectedObj = _Plants[_PlantSelect];
            if (data.obj.name == selectedObj.name)
            {
                DestroyImmediate(_GrassDatas[i].obj);
                _GrassDatas.RemoveAt(i);
            }   
        }
        _GrassAmount = _GrassDatas.Count;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
