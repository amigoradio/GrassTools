using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class GrassDataObject : ScriptableObject
{
    public List<GrassDictionary> dataList;

    //加一个参数，用于是否分批绘制，每500个mesh作为一个批次
    public void AddGrassData(string meshName, GrassDataItem data, int batchNum = 0)
    {
        if(dataList == null)
        {
            dataList = new List<GrassDictionary>();
        }
        List<GrassDataItem> items = null;
        foreach(GrassDictionary datas in dataList)
        {
            if (datas.meshName == meshName)
            {
                items = datas.itemDatas;
                break;
            }
        }
        if (items != null)
        {
            items.Add(data);
        }
        else 
        {
            items = new List<GrassDataItem>();
            items.Add(data);
            GrassDictionary gd = new GrassDictionary();
            gd.meshName = meshName;
            gd.itemDatas = items;
            dataList.Add(gd);
        }        
    }

    public void SortGrass(int batchNum = 0) 
    {
        foreach (GrassDictionary gd in dataList)
        {
            SortBySortOrder(gd.itemDatas);
        }
        if (batchNum > 0)
        {
            List<GrassDictionary> newDatas = new List<GrassDictionary>();
            for (int i = 0; i < dataList.Count; i++)
            {
                GrassDictionary oldGd = dataList[i];
                int num = oldGd.itemDatas.Count / batchNum + 1;
                //Debug.Log("num=" + num);
                for (int j = 0; j < num; j++)
                {
                    GrassDictionary gd = new GrassDictionary();
                    gd.meshName = oldGd.meshName;
                    gd.itemDatas = new List<GrassDataItem>();
                    //Debug.Log("j=" + j * batchNum + " oldCount=" + oldGd.itemDatas.Count);
                    for (int k = j * batchNum; k < (j + 1) * batchNum && k < oldGd.itemDatas.Count; k++)
                    {
                        //Debug.Log("k=" + k);
                        gd.itemDatas.Add(oldGd.itemDatas[k]);
                    }
                    if (gd.itemDatas.Count > 0)
                    {
                        newDatas.Add(gd);
                    }
                }
            }
            dataList = newDatas;
        }
    }

    /// <summary>
    /// 按 sortOrder 升序排序,由近到远
    /// </summary>
    /// <param name="grassList">需要排序的 GrassData 列表</param>
    private void SortBySortOrder(List<GrassDataItem> grassList)
    {
        if (grassList == null) 
            return;
        grassList.Sort((a, b) =>
        {
            return a.sortOrder.CompareTo(b.sortOrder);
        });
    }
 
}
