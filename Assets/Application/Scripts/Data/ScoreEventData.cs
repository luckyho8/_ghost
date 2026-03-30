using UnityEngine;

/// <summary>
/// 점수 획득 조건별 텍스트/기본 점수 테이블.
/// Resources 또는 Inspector에서 참조.
/// </summary>
[CreateAssetMenu(fileName = "ScoreEventData", menuName = "Ghost/Score Event Data")]
public class ScoreEventData : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("점수 이벤트 키 (코드에서 매칭)")]
        public ScoreEventType eventType;

        [Tooltip("화면에 표시할 텍스트 (예: 1 Line Clear!!)")]
        public string displayText;

        [Tooltip("기본 점수 (레벨 배율 적용 전)")]
        public int baseScore;
    }

    public enum ScoreEventType
    {
        Placement,          // 일반 배치
        LineClear1,
        LineClear2,
        LineClear3,
        LineClear4,
        CellDestroy,
        ComboBonus,
        FastDrop,           // 0.5초 이내 하드드롭
        QuickPlace,         // 0.5~1초 이내 배치
        GhostOffBonus,      // 고스트 OFF 상태 배치 보너스
    }

    [Header("점수 이벤트 테이블")]
    public Entry[] entries = new Entry[]
    {
        new Entry { eventType = ScoreEventType.Placement,      displayText = "Block Placed!",       baseScore = 10 },
        new Entry { eventType = ScoreEventType.LineClear1,     displayText = "1 Line Clear!!",      baseScore = 100 },
        new Entry { eventType = ScoreEventType.LineClear2,     displayText = "2 Line Clear!!",      baseScore = 300 },
        new Entry { eventType = ScoreEventType.LineClear3,     displayText = "3 Line Clear!!!",     baseScore = 700 },
        new Entry { eventType = ScoreEventType.LineClear4,     displayText = "4 Line Clear!!!!",    baseScore = 1500 },
        new Entry { eventType = ScoreEventType.CellDestroy,    displayText = "Destroy!",            baseScore = 50 },
        new Entry { eventType = ScoreEventType.ComboBonus,     displayText = "Combo Bonus!",        baseScore = 100 },
        new Entry { eventType = ScoreEventType.FastDrop,       displayText = "Fast Drop!!",         baseScore = 50 },
        new Entry { eventType = ScoreEventType.QuickPlace,     displayText = "Quick Place!",        baseScore = 20 },
        new Entry { eventType = ScoreEventType.GhostOffBonus,  displayText = "Ghost OFF x2!",       baseScore = 0 },
    };

    /// <summary>이벤트 타입으로 Entry 검색</summary>
    public Entry GetEntry(ScoreEventType type)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].eventType == type) return entries[i];
        }
        return null;
    }

    /// <summary>라인 클리어 수에 따라 적절한 타입 반환</summary>
    public static ScoreEventType GetLineClearType(int lineCount)
    {
        switch (lineCount)
        {
            case 1: return ScoreEventType.LineClear1;
            case 2: return ScoreEventType.LineClear2;
            case 3: return ScoreEventType.LineClear3;
            default: return ScoreEventType.LineClear4;
        }
    }
}
