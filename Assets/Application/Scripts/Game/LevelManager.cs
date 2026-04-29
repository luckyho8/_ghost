using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨별 난이도 공급자. LevelDifficultyData(SO)를 받아서
/// duration / fallSpeed / 티어 가중치 기반 블록 추첨을 GameManager에 제공.
/// 상태(currentLevel)는 들지 않음 — GameManager의 currentLevel을 인자로 받는 stateless 매니저.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Tooltip("레벨별 난이도 데이터 SO. 비워두면 안전 기본값(40s, fall 1.0, 티어1 100%) 사용.")]
    [SerializeField] private LevelDifficultyData data;

    public bool HasData => data != null && data.entries != null && data.entries.Count > 0;

    public LevelEntry GetEntry(int level)
    {
        if (!HasData) return LevelEntry.MakeDefault();
        return data.GetEntry(level);
    }

    public float GetDurationForLevel(int level) => GetEntry(level).duration;
    public float GetFallSpeedForLevel(int level) => GetEntry(level).fallSpeed;

    public BlockDataContents PickRandomBlock(int level, List<BlockDataContents> all)
    {
        if (all == null || all.Count == 0) return null;
        if (HasData) return data.PickWeightedBlock(level, all);
        return all[Random.Range(0, all.Count)];
    }
}
