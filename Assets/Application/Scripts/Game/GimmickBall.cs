using UnityEngine;

/// <summary>
/// 그리드 안을 대각선으로 이동하며 블록과 상호작용하는 기믹 볼.
/// Rigidbody velocity 기반 물리 이동.
/// - 낙하 중 블록(FallingBlock) 충돌 → 랜덤 블록 변환 + 반사
/// - 고정 블록 충돌 → 셀 파괴 + 반사
/// - 벽 충돌 → 단순 반사
/// 레이어 기반 감지 (태그 의존 X). 갇힘 탈출 로직 포함.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GimmickBall : MonoBehaviour
{
    private float speed;
    private float randomDeflectChance = 0.12f;
    private float randomDeflectAngle = 15f;
    private GameManager gameManager;
    private GimmickBallManager ballManager;
    private Transform meshRoot;
    private Rigidbody rb;
    private Vector3 moveDirection;

    // 레이어 번호 (Inspector에서 설정한 값과 일치해야 함)
    private int wallLayer;
    private int blockLayer;

    // 갇힘 감지
    private int consecutiveCollisions;
    private float lastCollisionTime;
    private const float RapidCollisionWindow = 0.3f; // 이 시간 내 연속 충돌이면 갇힌 것으로 판단
    private const int StuckThreshold = 4;            // 연속 충돌 횟수 임계값

    public void Init(GimmickBallManager manager, GameManager gm, float moveSpeed,
                     float deflectChance, float deflectAngle)
    {
        ballManager = manager;
        gameManager = gm;
        speed = moveSpeed;
        randomDeflectChance = deflectChance;
        randomDeflectAngle = deflectAngle;

        // 레이어 캐시
        wallLayer = LayerMask.NameToLayer("Wall");
        blockLayer = LayerMask.NameToLayer("Block");

        // Rigidbody 설정
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotation;

        // Mash_Point_01 (메시 루트) 참조
        meshRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;

        // 랜덤 대각선 방향으로 초기화 (XZ 평면)
        float angle = Random.Range(30f, 60f) * (Random.value > 0.5f ? 1f : -1f);
        float rad = angle * Mathf.Deg2Rad;
        moveDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
        if (Random.value > 0.5f)
            moveDirection.z = -moveDirection.z;

        rb.velocity = moveDirection * speed;
    }

    public void SetMeshScale(float scale)
    {
        if (meshRoot != null)
            meshRoot.localScale = Vector3.one * scale;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        if (rb != null && rb.velocity.sqrMagnitude > 0.001f)
            rb.velocity = rb.velocity.normalized * speed;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // 속도 일정하게 유지
        Vector3 vel = rb.velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.01f)
            vel = moveDirection * speed;
        else
        {
            moveDirection = vel.normalized;
            vel = moveDirection * speed;
        }
        rb.velocity = vel;
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        int otherLayer = other.layer;

        // 충돌 노말 계산
        Vector3 normal = Vector3.zero;
        if (collision.contactCount > 0)
        {
            normal = collision.GetContact(0).normal;
            normal.y = 0f;
        }
        if (normal.sqrMagnitude < 0.001f)
            normal = -moveDirection;
        normal.Normalize();

        // --- 갇힘 감지 ---
        float now = Time.time;
        if (now - lastCollisionTime < RapidCollisionWindow)
            consecutiveCollisions++;
        else
            consecutiveCollisions = 1;
        lastCollisionTime = now;

        if (consecutiveCollisions >= StuckThreshold)
        {
            EscapeStuck();
            return;
        }

        // --- 벽 충돌 ---
        if (otherLayer == wallLayer)
        {
            ApplyReflection(normal);
            return;
        }

        // --- 블록 레이어 충돌 ---
        if (otherLayer == blockLayer)
        {
            // 낙하 중 블록인지 확인
            FallingBlock falling = other.GetComponentInParent<FallingBlock>();
            if (falling != null)
            {
                // 낙하 블록 → 랜덤 변환
                if (gameManager != null)
                    gameManager.RandomizeCurrentBlock();
                ApplyReflection(normal);
                return;
            }

            // 고정 블록 → 셀 파괴
            Vector3 cubePos = other.transform.position;
            int gx = Mathf.RoundToInt(cubePos.x);
            int gz = Mathf.RoundToInt(cubePos.z);
            if (gameManager != null)
                gameManager.TryDestroyAndRemoveCubeAt(gx, gz);
            ApplyReflection(normal);
            return;
        }

        // 기타 충돌
        ApplyReflection(normal);
    }

    /// <summary>갇힘 탈출: 그리드 중앙으로 워프 + 랜덤 방향 재발사</summary>
    private void EscapeStuck()
    {
        consecutiveCollisions = 0;

        // 그리드 중앙으로 이동
        float centerX = 0f;
        float centerZ = 0f;
        if (gameManager != null)
        {
            centerX = (gameManager.GridMinX + gameManager.GridMaxX) * 0.5f;
            centerZ = 0f;
        }
        transform.position = new Vector3(centerX, transform.position.y, centerZ);

        // 랜덤 방향 재발사
        float angle = Random.Range(30f, 60f) * (Random.value > 0.5f ? 1f : -1f);
        float rad = angle * Mathf.Deg2Rad;
        moveDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
        if (Random.value > 0.5f)
            moveDirection.z = -moveDirection.z;

        rb.velocity = moveDirection * speed;
    }

    private void ApplyReflection(Vector3 normal)
    {
        moveDirection = Vector3.Reflect(moveDirection, normal).normalized;

        // 갇힘 방지: 일정 확률로 랜덤 편차 추가
        if (Random.value < randomDeflectChance)
        {
            float deflect = Random.Range(-randomDeflectAngle, randomDeflectAngle);
            moveDirection = Quaternion.Euler(0f, deflect, 0f) * moveDirection;
        }

        // 수평/수직에 너무 가까운 각도 보정
        float absX = Mathf.Abs(moveDirection.x);
        float absZ = Mathf.Abs(moveDirection.z);
        if (absX < 0.15f || absZ < 0.15f)
        {
            float signX = moveDirection.x >= 0 ? 1f : -1f;
            float signZ = moveDirection.z >= 0 ? 1f : -1f;
            moveDirection.x = signX * Mathf.Max(absX, 0.3f);
            moveDirection.z = signZ * Mathf.Max(absZ, 0.3f);
        }

        moveDirection.y = 0f;
        moveDirection.Normalize();

        rb.velocity = moveDirection * speed;
    }
}
