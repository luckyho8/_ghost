using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 SO에 모든 블록 정보를 저장. Ghost > Block Editor에서 편집.
/// </summary>
[CreateAssetMenu(fileName = "AllBlockData", menuName = "Ghost/All Block Data", order = 0)]
public class AllBlockData : ScriptableObject
{
    public const int PivotIndex = 0;

    public List<BlockDataContents> blockList = new List<BlockDataContents>();

    public static string BlockIdToDisplay(int id)
    {
        return "#" + id.ToString("00");
    }
}

[Serializable]
public class BlockDataContents
{
    public int blockID;
    public string blockName = "";
    public GameObject blockPrefab;
    [Range(1, 3)]
    public int tier = 1;
    public bool[] shapeData = new bool[16];
    public Color blockColor = Color.white;

    public BlockDataContents()
    {
        if (shapeData == null || shapeData.Length != 16)
            shapeData = new bool[16];
    }
}
