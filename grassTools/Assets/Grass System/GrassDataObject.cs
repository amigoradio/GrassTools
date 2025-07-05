using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GrassDataObject : ScriptableObject
{
    public List<GrassDictionary> dataList;

    /// <summary>
    /// 将刷出来的草一颗颗的加入到数据中保存起来
    /// </summary>
    /// <param name="meshName"></param>
    /// <param name="data"></param>
    public void AddGrassData(string meshName, string matName, GrassDataItem data)
    {
        if (dataList == null)
        {
            dataList = new List<GrassDictionary>();
        }
        List<GrassDataItem> items = null;
        foreach (GrassDictionary datas in dataList)
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
            gd.matName = matName;
            gd.itemDatas = items;
            dataList.Add(gd);
        }
    }

    /// <summary>
    /// 排序保存的数据，如果设置了批次的数量，就对数据进行处理
    /// </summary>
    /// <param name="batchNum"></param>
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
                for (int j = 0; j < num; j++)
                {
                    GrassDictionary gd = new GrassDictionary();
                    gd.meshName = oldGd.meshName;
                    gd.matName = oldGd.matName;
                    gd.itemDatas = new List<GrassDataItem>();
                    for (int k = j * batchNum; k < (j + 1) * batchNum && k < oldGd.itemDatas.Count; k++)
                    {
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
    /// 按 sortOrder 降序排列,由远到近，符合半透明物体的排序规则
    /// </summary>
    /// <param name="grassList">需要排序的 GrassData 列表</param>
    private void SortBySortOrder(List<GrassDataItem> grassList)
    {
        if (grassList == null) 
            return;
        grassList.Sort((a, b) =>
        {
            return b.sortOrder.CompareTo(a.sortOrder);
        });
    }
 
}
