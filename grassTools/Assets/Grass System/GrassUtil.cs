using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class GrassUtil
{

    private static Mesh _grassMesh;
    public static Mesh CreateGrassMesh()
    {
        var grassMesh = new Mesh { name = "Grass Quad" };
        float width = 1f;
        float height = 1f;
        float halfWidth = width / 2;
        grassMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-halfWidth, 0, 0.0f),
                new Vector3(-halfWidth,  height, 0.0f),
                new Vector3(halfWidth, 0, 0.0f),
                new Vector3(halfWidth,  height, 0.0f),

            });
        grassMesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 0),
                new Vector2(1, 1),
            });

        grassMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3, },
        MeshTopology.Triangles, 0, false);
        grassMesh.RecalculateNormals();
        grassMesh.UploadMeshData(true);
        return grassMesh;
    }

    public static Mesh unitMesh
    {
        get
        {
            if (_grassMesh != null)
            {
                return _grassMesh;
            }
            _grassMesh = CreateGrassMesh();
            return _grassMesh;
        }
    }


    /// <summary>
    /// 三角形内部，取平均分布的随机点
    /// </summary>
    public static Vector3 RandomPointInsideTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var x = Random.Range(0, 1f);
        var y = Random.Range(0, 1f);
        if (y > 1 - x)
        {
            //如果随机到了右上区域，那么反转到左下
            var temp = y;
            y = 1 - x;
            x = 1 - temp;
        }
        var vx = p2 - p1;
        var vy = p3 - p1;
        return p1 + x * vx + y * vy;
    }


    //计算三角形面积
    public static float GetAreaOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var vx = p2 - p1;
        var vy = p3 - p1;
        var dotvxy = Vector3.Dot(vx, vy);
        var sqrArea = vx.sqrMagnitude * vy.sqrMagnitude - dotvxy * dotvxy;
        return 0.5f * Mathf.Sqrt(sqrArea);
    }

    public static Vector3 GetFaceNormal(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var vx = p2 - p1;
        var vy = p3 - p1;
        return Vector3.Cross(vx, vy);
    }

    //要做数据的压缩，必须让asset中的vecter4和矩阵支持json，所以需要Newtonsoft.Json和插件
    //https://github.com/jilleJr/Newtonsoft.Json-for-Unity.Converters.git#1.6.3

    /// <summary>
    /// Gzip压缩数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte[] Compress(byte[] data)
    {
        using (MemoryStream output = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }

    /// <summary>
    /// Gzip解压缩数据
    /// </summary>
    /// <param name="compressedData"></param>
    /// <returns></returns>
    public static byte[] Decompress(byte[] compressedData)
    {
        using (MemoryStream input = new MemoryStream(compressedData))
        {
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return output.ToArray();
                }
            }
        }
    }

    /// <summary>
    /// 序列化为JSON字节数组
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static byte[] ToJsonBytes(ScriptableObject obj)
    {
        string jsonData = JsonConvert.SerializeObject(obj);
        return System.Text.Encoding.UTF8.GetBytes(jsonData);
    }

    /// <summary>
    /// 从JSON字节数组反序列化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static T FromJsonBytes<T>(byte[] bytes) where T : ScriptableObject
    {
        string json = System.Text.Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json);
    }

}
