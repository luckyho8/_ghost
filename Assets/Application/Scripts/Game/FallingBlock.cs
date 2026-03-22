using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 낙하 중인 블록. 그리드 이동/회전/경계 체크, 고정 시 그리드 기록.
/// </summary>
public class FallingBlock : MonoBehaviour
{
    private GameManager manager;
    private BlockDataContents data;
    private int pivotGridX;
    private int pivotGridZ;

    public int PivotGridX => pivotGridX;
    public int PivotGridZ => pivotGridZ;
    private List<Transform> cubes = new List<Transform>();

    public void Init(GameManager gm, BlockDataContents blockData, int startX, int startZ)
    {
        manager = gm;
        data = blockData;
        pivotGridX = startX;
        pivotGridZ = startZ;
        cubes.Clear();
        for (int i = 0; i < transform.childCount; i++)
            cubes.Add(transform.GetChild(i));
    }

    public Color BlockColor => data != null ? data.blockColor : Color.white;

    public List<Vector2Int> GetCurrentCells()
    {
        var list = new List<Vector2Int>();
        foreach (var t in cubes)
        {
            Vector3 w = t.position;
            list.Add(new Vector2Int(Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.z)));
        }
        return list;
    }

    private bool WouldBeValid(List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            if (!manager.IsInBounds(c.x, c.y)) return false;
            if (manager.IsCellOccupied(c.x, c.y)) return false;
        }
        return true;
    }

    public bool TryMoveLeft()
    {
        transform.position += Vector3.left;
        pivotGridX--;
        var cells = GetCurrentCells();
        if (!WouldBeValid(cells))
        {
            transform.position += Vector3.right;
            pivotGridX++;
            return false;
        }
        return true;
    }

    public bool TryMoveRight()
    {
        transform.position += Vector3.right;
        pivotGridX++;
        var cells = GetCurrentCells();
        if (!WouldBeValid(cells))
        {
            transform.position += Vector3.left;
            pivotGridX--;
            return false;
        }
        return true;
    }

    public bool TryMoveDown()
    {
        transform.position += new Vector3(0f, 0f, -1f);
        pivotGridZ--;
        var cells = GetCurrentCells();
        if (!WouldBeValid(cells))
        {
            transform.position += new Vector3(0f, 0f, 1f);
            pivotGridZ++;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 회전 후 벽/바닥/블록과 겹치면 좌우 1~4칸 이동(Wall Kick) 시도. 실패 시 회전 복구.
    /// </summary>
    public void TryRotateWithWallKick()
    {
        transform.Rotate(0f, 90f, 0f);
        if (WouldBeValid(GetCurrentCells()))
            return;

        for (int i = 1; i <= 4; i++)
        {
            transform.position += Vector3.right;
            if (WouldBeValid(GetCurrentCells()))
                return;
        }
        transform.position -= 4f * Vector3.right;

        for (int i = 1; i <= 4; i++)
        {
            transform.position += Vector3.left;
            if (WouldBeValid(GetCurrentCells()))
                return;
        }
        transform.position -= 4f * Vector3.left;

        transform.Rotate(0f, -90f, 0f);
    }

    public void HardDrop()
    {
        while (TryMoveDown()) { }
        if (manager != null)
            manager.FreezeCurrentBlock();
    }

    public void WriteToGrid(HashSet<Vector2Int> grid)
    {
        foreach (var c in GetCurrentCells())
            grid.Add(c);
    }
}
