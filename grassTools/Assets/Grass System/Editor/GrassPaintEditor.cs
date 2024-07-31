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
        string globalSettingPath = "Assets/GlobalSetting.asset";
        FileInfo f = new FileInfo(globalSettingPath);
        if (f.Exists)
        {
            File.Delete(globalSettingPath);
            File.Delete(globalSettingPath + ".meta");
            AssetDatabase.Refresh();
        }
        GrassGlobalSetting settings = ScriptableObject.CreateInstance<GrassGlobalSetting>();
        AssetDatabase.CreateAsset(settings, globalSettingPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

   [MenuItem("Tools/刷草/刷草工具 %g", false, 2)]
	static void Open()
	{
	    var window = (GrassPaintEditor) EditorWindow.GetWindowWithRect(typeof(GrassPaintEditor), new Rect(0, 0, 386,520), false, "Paint Grass");
	    window.Show();   
	}
    public GameObject AddObject;
    public GameObject[] Plants = new GameObject[6];
    public Texture[] TexObjects = new Texture[6];
    public int PlantSelect = 0;
    public int brushSize = 5;
    public float scaleRandomMin = 1f;
    public float scaleRandomMax = 1f;
    public float density = 0.5f;
    private int grassAmount = 0;
    public LayerMask hitMask;
    public int grassLayer;
    private GameObject grassRoot;
    private bool faceToCamera;
    private bool paintActive = true;
    private bool isRandomRotate = true;
    private Camera mainCamera;

    private RaycastHit[] m_Results = new RaycastHit[1];
    private Vector3 hitPos;
    private Vector3 hitNormal;
    private Vector3 cachedPos;
    private int selectFunction = 0;
    public List<GrassPaintData> grassData = new List<GrassPaintData>();

    private float windSpeed;
    private float windStrength;

    private static string GrassDataPath = "Assets/Game/GrassData/";
    private static string GrassRootName = "GrassRoot";
    private static string GrassDataNameSuffix = "_GrassData.asset";

    private void OnEnable()
    {
        mainCamera = Camera.main;
        if(mainCamera == null)
        {
            Debug.LogError("场景没有主相机");
        }
        hitMask = LayerMask.GetMask("Ground");
        AddExistGrassToData();
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
            if (obj.name.Contains(GrassRootName))
            {
                int len = obj.transform.childCount;
                for (int i = 0; i < len; i++)
                {
                    Transform child = obj.transform.GetChild(i);
                        GrassPaintData data = new GrassPaintData();
                        data.obj = child.gameObject;
                        data.position = child.position;
                        grassData.Add(data);
                }
            }
        }
        grassAmount = grassData.Count;
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
        if(grassData != null)
        {
            grassAmount = grassData.Count;
        }
        else
        {
            grassData = new List<GrassPaintData>();
        }
        
        GameObject curSelectObj = Selection.activeGameObject;
        if(curSelectObj != null && curSelectObj.name.Contains(GrassRootName))
        {
            grassRoot = curSelectObj;
        }
        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        GUILayout.BeginHorizontal();
        GUILayout.Label("添加prefab", GUILayout.Width(125));
        
        AddObject = (GameObject)EditorGUILayout.ObjectField("", AddObject, typeof(GameObject), true, GUILayout.Width(160));
        if (GUILayout.Button("+", GUILayout.Width(40)))
        {
            for (int i = 0; i < 6; i++)
            { 
                if (Plants[i] == null)
                {
                    Plants[i] = AddObject;
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
            if (Plants[i] != null)
                TexObjects[i] = AssetPreview.GetAssetPreview(Plants[i]) as Texture;
            else TexObjects[i] = null;
        }
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        PlantSelect = GUILayout.SelectionGrid(PlantSelect, TexObjects, 6, "gridlist", GUILayout.Width(330), GUILayout.Height(55));

        GUILayout.BeginHorizontal();

        for (int i = 0; i < 6; i++)
        {
            if (GUILayout.Button("—", GUILayout.Width(52)))
            {
                Plants[i] = null;
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        paintActive = EditorGUILayout.Toggle("开始绘制", paintActive);
        GUILayout.BeginHorizontal();
        GUILayout.Label("设置", GUILayout.Width(145));        
        GUILayout.EndHorizontal();
        faceToCamera = EditorGUILayout.Toggle("面向相机", faceToCamera);
        isRandomRotate = EditorGUILayout.Toggle("随机旋转", faceToCamera? false : isRandomRotate);
        brushSize = (int)EditorGUILayout.Slider("笔刷大小（1为单棵草）", brushSize, 1, 30);
        scaleRandomMin = EditorGUILayout.Slider("随机缩放最小值", scaleRandomMin, 0.1f, 2f);
        scaleRandomMax = EditorGUILayout.Slider("随机缩放最大值", scaleRandomMax, 0.1f, 2f);
        density = EditorGUILayout.Slider("密度", density, 0, 10);
        LayerMask tempMask = EditorGUILayout.MaskField("检测的层", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(hitMask), InternalEditorUtility.layers);
        hitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        grassLayer = EditorGUILayout.LayerField("草的层", grassLayer);
        EditorGUILayout.Separator();
        GUILayout.Label("风的设置", GUILayout.Width(145)); 
        windSpeed = EditorGUILayout.Slider("风速", windSpeed, -2f, 2f);
        windStrength = EditorGUILayout.Slider("风强度", windStrength, -2f, 2f);

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("创建节点"))
        {
            grassRoot = new GameObject(GrassRootName);
            grassRoot.transform.position = Vector3.zero;
            grassRoot.transform.rotation = Quaternion.identity;
            grassRoot.transform.localScale = Vector3.one;
        }
        if (GUILayout.Button("添加"))
        {
            selectFunction = 1;
        }
        if (GUILayout.Button("删除"))
        {
            selectFunction = 2;
        }
        if(GUILayout.Button("清空"))
        {
            if (EditorUtility.DisplayDialog("清除所有草", "是不是要删除所有刷的草？", "删除", "不删"))
            {
                selectFunction = 0;
                ClearMesh();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical("box", GUILayout.Width(347));
        EditorGUILayout.LabelField("草的总数量: " + grassAmount.ToString(), EditorStyles.label);
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
        if (paintActive)
        {
            DrawHandles();
            if (selectFunction == 1 || selectFunction == 2)
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
        int hits = Physics.RaycastNonAlloc(ray, m_Results, Mathf.Infinity, hitMask);
        for (int i = 0; i < hits; i++)
        {
            hitPos = m_Results[i].point;
            hitNormal = m_Results[i].normal;
        }
        Color discColor = Color.yellow;
        Color discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);

        switch (selectFunction)
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
        Handles.DrawWireDisc(hitPos, hitNormal, brushSize);
        Handles.color = discColor2;
        Handles.DrawSolidDisc(hitPos, hitNormal, brushSize);
        if (hitPos != cachedPos)
        {
            SceneView.RepaintAll();
            cachedPos = hitPos;
        }
    }

    /// <summary>
    /// 收集草的数据，只收集GrassRoot下的草
    /// </summary>
    private void CollectGrassData()
    {
        Scene scene = SceneManager.GetActiveScene();
        string dataName = scene.name + GrassDataNameSuffix;
        DirectoryInfo dir = new DirectoryInfo(GrassDataPath);
        if (!dir.Exists)
        {
            dir.Create();
        }
        string savePath = Path.Combine(GrassDataPath, dataName).Replace('\\', '/');
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
            if (obj.name.Contains(GrassRootName))
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
        string loadPath = Path.Combine(GrassDataPath, dataName + GrassDataNameSuffix).Replace('\\', '/');
        FileInfo f = new FileInfo(loadPath);
        if (f.Exists)
        {
            GrassDataObject datas = (GrassDataObject)AssetDatabase.LoadAssetAtPath(loadPath, typeof(GrassDataObject));
            if (datas != null)
            {
                grassData.Clear();
                for (int i = 0; i < datas.dataList.Count; i++)
                {
                    grassRoot = new GameObject(GrassRootName);
                    grassRoot.transform.position = Vector3.zero;
                    grassRoot.transform.rotation = Quaternion.identity;
                    grassRoot.transform.localScale = Vector3.one;
                    GrassDictionary data = datas.dataList[i];
                    GameObject prefab = null;
                    for (int j = 0; j < Plants.Length; j++)
                    {
                        
                        GameObject obj = Plants[j];
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
                            go.transform.SetParent(grassRoot.transform);
                            go.transform.position = GetPositionFromMatrix(dataItem.materix);
                            go.transform.rotation = GetRotationFromMatrix(dataItem.materix);
                            go.transform.localScale = GetScaleFromMatrix(dataItem.materix);
                            GameObject newPlant = Instantiate(prefab);
                            newPlant.transform.SetParent(go.transform);
                            newPlant.name = "Grass";
                            SetLayerRecursively(go, grassLayer);
                            if (mainCamera != null && faceToCamera)
                            {
                                newPlant.transform.LookAt(mainCamera.transform);
                            }
                            GrassPaintData newData = new GrassPaintData();
                            newData.position = go.transform.position;
                            newData.obj = go;
                            grassData.Add(newData);
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
        if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, hitMask))
        {
            Vector3 hitPos = raycastHit.point;
            if (selectFunction == 1)
            {
                GameObject meshObj = Plants[PlantSelect];
                if (meshObj == null)
                {
                    Debug.LogError("没有选择草");
                    return;
                }
                if (brushSize > 1)
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
                        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * brushSize;
                        Ray ray2 = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        ray2.origin += new Vector3(randomOffset.x, 0, randomOffset.y);
                        if (Physics.Raycast(ray2, out raycastHit, Mathf.Infinity, hitMask))
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
        float circleArea = brushSize * brushSize * Mathf.PI;
        float width = Mathf.Max(0.2f, bounds.size.x);
        float longer = Mathf.Max(0.2f, bounds.size.z);
        float areaNumf = circleArea / (width * longer);
        int areaNum = Mathf.FloorToInt(areaNumf * density);
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
        GameObject prefab = Plants[PlantSelect];
        if (prefab == null)
        {
            Debug.LogError("选择的草找不到");
            return;
        }
        GameObject newPlant = Instantiate(prefab);
        newPlant.transform.SetParent(grassRoot.transform);
        newPlant.transform.position = hitPos;
        Vector3 rotUp = Vector3.ProjectOnPlane(newPlant.transform.forward, hitNormal);
        Quaternion rot = Quaternion.LookRotation(rotUp,hitNormal);
        newPlant.transform.rotation = rot;
        if (isRandomRotate)
        {
            newPlant.transform.rotation *= Quaternion.AngleAxis(Random.Range(0, 360), Vector3.up); 
        }
        if(scaleRandomMin == scaleRandomMax)
        {
            newPlant.transform.localScale = Vector3.one * scaleRandomMin;
        }
        else
        {
            newPlant.transform.localScale = Vector3.one * UnityEngine.Random.Range(scaleRandomMin, scaleRandomMax);
        }
        newPlant.name = prefab.name;
        SetLayerRecursively(newPlant, grassLayer);
        if(mainCamera != null && faceToCamera)
        {
            newPlant.transform.LookAt(mainCamera.transform);
        }
        Undo.RegisterCompleteObjectUndo(newPlant, "Add Grass");
        GrassPaintData newData = new GrassPaintData();
        newData.position = hitPos;
        newData.obj = newPlant;
        grassData.Add(newData);
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
        for (int i = grassData.Count - 1; i >= 0 ; i--)
        {
            GrassPaintData data = grassData[i];
            GameObject selectedObj = Plants[PlantSelect];
            if (data.obj.name == selectedObj.name)
            {
                if (Vector3.Distance(hitPos, grassData[i].position) < brushSize)
                {
                    Undo.DestroyObjectImmediate(grassData[i].obj);
                    DestroyImmediate(grassData[i].obj);
                    grassData.RemoveAt(i);
                    grassAmount--;
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
        for (int i = grassData.Count - 1; i >= 0; i--)
        {
            GrassPaintData data = grassData[i];
            GameObject selectedObj = Plants[PlantSelect];
            if (data.obj.name == selectedObj.name)
            {
                DestroyImmediate(grassData[i].obj);
                grassData.RemoveAt(i);
            }   
        }
        grassAmount = grassData.Count;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
