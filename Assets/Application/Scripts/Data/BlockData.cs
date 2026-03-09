using UnityEngine;

/// <summary>
/// 하이퍼 캐주얼 퍼즐 게임용 블록 데이터.
/// 4x4 그리드 기반 블록 형태를 정의합니다.
/// Ghost > Block Editor 윈도우에서 편집합니다.
/// </summary>
public class BlockData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("고유 아이디")]
    public int blockID;

    [Tooltip("블록 이름")]
    public string blockName = "New Block";

    [Header("Prefab")]
    [Tooltip("블록 메시용 프리팹 (예: box_1x1). 필수 할당")]
    public GameObject blockPrefab;

    [Header("Difficulty")]
    [Tooltip("1, 2, 3 단계 난이도 티어")]
    [Range(1, 3)]
    public int tier = 1;

    [Header("Shape (4x4 Grid)")]
    [Tooltip("행 우선 순서: [0~3]=1행, [4~7]=2행, [8~11]=3행, [12~15]=4행. 인덱스 0 = 피벗(좌측 하단)")]
    public bool[] shapeData = new bool[16];

    [Header("Visual")]
    [Tooltip("블록 고유 색상. URP 머티리얼 Base Map 컬러로 적용")]
    public Color blockColor = Color.white;

    /// <summary>
    /// 그리드 인덱스 (0~15)를 행/열 (0~3)로 변환.
    /// </summary>
    public static void IndexToRowCol(int index, out int row, out int col)
    {
        row = index / 4;
        col = index % 4;
    }

    /// <summary>
    /// 행/열을 그리드 인덱스로 변환.
    /// </summary>
    public static int RowColToIndex(int row, int col)
    {
        return Mathf.Clamp(row, 0, 3) * 4 + Mathf.Clamp(col, 0, 3);
    }

    /// <summary>
    /// 4x4 피벗 인덱스: 좌측 하단 첫 번째 칸 (Index 0).
    /// </summary>
    public const int PivotIndex = 0;

    private void OnValidate()
    {
        if (shapeData == null || shapeData.Length != 16)
            shapeData = new bool[16];
    }
}
