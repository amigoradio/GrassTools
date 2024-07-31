using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GrassDataObject : ScriptableObject
{
    public List<GrassDictionary> dataList;

    public void AddGrassData(string meshName, GrassDataItem data)
    {
        if(dataList == null)
        {
            dataList = new List<GrassDictionary>();
        }
        List<GrassDataItem> items = null;
        foreach(GrassDictionary datas in dataList)
        {
            if(datas.meshName == meshName)
            {
                items = datas.itemDatas;
                break;
            }
        }
        if(items == null)
        {
            items = new List<GrassDataItem>();
            items.Add(data);
            GrassDictionary gd = new GrassDictionary();
            gd.meshName = meshName;
            gd.itemDatas = items;
            dataList.Add(gd);
        }
        else
        {
            items.Add(data);
        }
    }

}
