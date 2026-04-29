using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDifficultyData", menuName = "Ghost/Level Difficulty Data", order = 1)]
public class LevelDifficultyData : ScriptableObject
{
    [Tooltip("레벨별 난이도 엔트리 (Element 0 = 레벨 1). 정의된 마지막 너머 레벨은 마지막 엔트리를 반복.")]
    public List<LevelEntry> entries = new List<LevelEntry>();

    public LevelEntry GetEntry(int level)
    {
        if (entries == null || entries.Count == 0)
            return LevelEntry.MakeDefault();
        int idx = Mathf.Clamp(level - 1, 0, entries.Count - 1);
        return entries[idx];
    }

    public BlockDataContents PickWeightedBlock(int level, List<BlockDataContents> all)
    {
        if (all == null || all.Count == 0) return null;
        var entry = GetEntry(level);

        int chosenTier = WeightedTier(entry.tierWeights);

        // 선택된 티어의 풀이 비어있으면 한 단계 낮은 티어로 폴백 (최대 3회)
        var pool = new List<BlockDataContents>(all.Count);
        int probe = chosenTier;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            pool.Clear();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].tier == probe) pool.Add(all[i]);
            if (pool.Count > 0) break;
            probe = Mathf.Max(1, probe - 1);
        }
        if (pool.Count == 0) return all[UnityEngine.Random.Range(0, all.Count)];
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private static int WeightedTier(float[] weights)
    {
        if (weights == null || weights.Length == 0) return 1;
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
            total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return 1;

        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += Mathf.Max(0f, weights[i]);
            if (r <= acc) return i + 1; // 티어 1-base
        }
        return weights.Length;
    }

    private void Reset() => entries = BuildDefaults();

    [ContextMenu("기본 레벨 테이블 채우기")]
    public void PopulateDefaults() => entries = BuildDefaults();

    private static List<LevelEntry> BuildDefaults()
    {
        return new List<LevelEntry>
        {
            new LevelEntry { duration = 40f, fallSpeed = 1.00f, tierWeights = new float[] { 1.00f, 0.00f, 0.00f } }, // L1
            new LevelEntry { duration = 40f, fallSpeed = 0.90f, tierWeights = new float[] { 1.00f, 0.00f, 0.00f } }, // L2
            new LevelEntry { duration = 40f, fallSpeed = 0.81f, tierWeights = new float[] { 0.90f, 0.08f, 0.02f } }, // L3
            new LevelEntry { duration = 40f, fallSpeed = 0.73f, tierWeights = new float[] { 0.85f, 0.13f, 0.02f } }, // L4
            new LevelEntry { duration = 40f, fallSpeed = 0.66f, tierWeights = new float[] { 0.75f, 0.20f, 0.05f } }, // L5
            new LevelEntry { duration = 40f, fallSpeed = 0.59f, tierWeights = new float[] { 0.60f, 0.30f, 0.10f } }, // L6
            new LevelEntry { duration = 35f, fallSpeed = 0.53f, tierWeights = new float[] { 0.50f, 0.35f, 0.15f } }, // L7
            new LevelEntry { duration = 35f, fallSpeed = 0.48f, tierWeights = new float[] { 0.40f, 0.40f, 0.20f } }, // L8
            new LevelEntry { duration = 30f, fallSpeed = 0.43f, tierWeights = new float[] { 0.30f, 0.45f, 0.25f } }, // L9
            new LevelEntry { duration = 30f, fallSpeed = 0.39f, tierWeights = new float[] { 0.20f, 0.50f, 0.30f } }, // L10
        };
    }
}

[Serializable]
public class LevelEntry
{
    [Tooltip("이 레벨이 유지되는 시간 (다음 레벨까지, 초)")]
    public float duration = 40f;

    [Tooltip("블록 자동 낙하 간격 (초). 한 칸 떨어지는 데 걸리는 시간 — 작을수록 빠름.")]
    public float fallSpeed = 1.0f;

    [Tooltip("티어 출현 가중치 [T1, T2, T3]. 합이 1일 필요 없음 — 자동 정규화. 예: [0.9, 0.08, 0.02]")]
    public float[] tierWeights = new float[] { 1f, 0f, 0f };

    public static LevelEntry MakeDefault() => new LevelEntry
    {
        duration = 40f,
        fallSpeed = 1f,
        tierWeights = new float[] { 1f, 0f, 0f }
    };
}
