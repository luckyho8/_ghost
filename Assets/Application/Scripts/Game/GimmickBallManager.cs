using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기믹 볼 스폰 및 레벨별 크기/속도 제어.
/// 모든 설정값은 Inspector에서 조절 가능.
/// </summary>
public class GimmickBallManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("볼 프리팹 (Gimmick_Ball_01)")]
    [SerializeField] private GameObject ballPrefab;

    [Tooltip("GameManager 참조")]
    [SerializeField] private GameManager gameManager;

    [Header("Spawn Settings")]
    [Tooltip("볼 스폰 위치 (비워두면 그리드 중앙)")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("볼 개수")]
    [SerializeField] private int ballCount = 1;

    [Tooltip("볼 Y 위치 (높이)")]
    [SerializeField] private float ballY = 0.5f;

    [Header("Ball Size")]
    [Tooltip("초기 볼 크기 (Mash_Point_01 스케일)")]
    [SerializeField] private float initialBallScale = 0.5f;

    [Tooltip("레벨당 크기 증가량")]
    [SerializeField] private float scalePerLevel = 0.1f;

    [Tooltip("크기 성장 시작 레벨")]
    [SerializeField] private int growStartLevel = 3;

    [Tooltip("최대 볼 크기")]
    [SerializeField] private float maxBallScale = 1.5f;

    [Header("Ball Speed")]
    [Tooltip("초기 볼 이동 속도")]
    [SerializeField] private float initialSpeed = 4f;

    [Tooltip("레벨당 속도 증가량")]
    [SerializeField] private float speedPerLevel = 0.3f;

    [Tooltip("최대 볼 속도")]
    [SerializeField] private float maxSpeed = 10f;

    [Header("Reflection")]
    [Tooltip("반사 시 랜덤 편차 확률 (0~1, 갇힘 방지)")]
    [Range(0f, 1f)]
    [SerializeField] private float randomDeflectChance = 0.12f;

    [Tooltip("랜덤 편차 최대 각도")]
    [SerializeField] private float randomDeflectAngle = 15f;

    private List<GimmickBall> activeBalls = new List<GimmickBall>();
    private int lastAppliedLevel = 0;

    private void Start()
    {
        SpawnBalls();
    }

    /// <summary>설정된 개수만큼 볼 스폰</summary>
    public void SpawnBalls()
    {
        ClearBalls();

        for (int i = 0; i < ballCount; i++)
        {
            if (ballPrefab == null) continue;

            Vector3 pos = GetSpawnPosition(i);
            GameObject ballObj = Instantiate(ballPrefab, pos, Quaternion.identity, transform);

            // Rigidbody 확보 (GimmickBall.Init에서 물리 설정)
            if (ballObj.GetComponent<Rigidbody>() == null)
                ballObj.AddComponent<Rigidbody>();

            // GimmickBall 컴포넌트 추가 및 초기화
            GimmickBall ball = ballObj.GetComponent<GimmickBall>();
            if (ball == null)
                ball = ballObj.AddComponent<GimmickBall>();
            ball.Init(this, gameManager, initialSpeed, randomDeflectChance, randomDeflectAngle);
            ball.SetMeshScale(initialBallScale);

            activeBalls.Add(ball);
        }

        lastAppliedLevel = gameManager != null ? gameManager.currentLevel : 1;
    }

    /// <summary>레벨 변경 시 호출 — 볼 크기/속도 갱신</summary>
    public void OnLevelChanged(int newLevel)
    {
        if (newLevel == lastAppliedLevel) return;
        lastAppliedLevel = newLevel;

        float currentScale = CalculateScale(newLevel);
        float currentSpeed = CalculateSpeed(newLevel);

        foreach (var ball in activeBalls)
        {
            if (ball == null) continue;
            ball.SetMeshScale(currentScale);
            ball.SetSpeed(currentSpeed);
        }
    }

    private float CalculateScale(int level)
    {
        if (level < growStartLevel)
            return initialBallScale;
        int growSteps = level - growStartLevel;
        float scale = initialBallScale + scalePerLevel * growSteps;
        return Mathf.Min(scale, maxBallScale);
    }

    private float CalculateSpeed(int level)
    {
        float spd = initialSpeed + speedPerLevel * (level - 1);
        return Mathf.Min(spd, maxSpeed);
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoint != null)
        {
            Vector3 pos = spawnPoint.position;
            pos.x += index * 1.5f; // 복수 볼 시 간격
            pos.y = ballY;
            return pos;
        }

        // 기본: 그리드 중앙
        float centerX = 0f;
        float centerZ = 0f;
        if (gameManager != null)
        {
            centerX = (gameManager.GridMinX + gameManager.GridMaxX) * 0.5f;
            centerZ = 0f; // 그리드 중앙 높이
        }
        return new Vector3(centerX + index * 1.5f, ballY, centerZ);
    }

    public void ClearBalls()
    {
        foreach (var ball in activeBalls)
        {
            if (ball != null)
                Destroy(ball.gameObject);
        }
        activeBalls.Clear();
    }

    private void OnDestroy()
    {
        ClearBalls();
    }
}
