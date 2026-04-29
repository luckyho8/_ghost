using System.Collections;
using System.Collections.Generic;
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
    private bool isPaused;
    private Vector3 savedVelocity;

    // 스케일 펀치
    private float originalScale = 1f;
    private Coroutine punchCoroutine;

    // TimeStop 비주얼 (반투명 + 콜라이더 OFF)
    private Collider mainCollider;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Coroutine timeStopFadeCo;
    private bool isTimeStopActive;
    private float currentVisualAlpha = 1f;
    private const float TimeStopGhostAlpha = 0.3f;
    private const float TimeStopFadeDuration = 0.2f;

    // 레이어 번호 (Inspector에서 설정한 값과 일치해야 함)
    private int wallLayer;
    private int blockLayer;

    // 갇힘 감지
    private int consecutiveCollisions;
    private float lastCollisionTime;
    private const float RapidCollisionWindow = 0.3f; // 이 시간 내 연속 충돌이면 갇힌 것으로 판단
    private const int StuckThreshold = 4;            // 연속 충돌 횟수 임계값

    // 충돌 후 노말 방향 푸시 거리 (인접 셀 즉시 재충돌 방지)
    private const float PostCollisionPush = 0.1f;

    // ── Debug ───────────────────────────────────────────────
    private bool dbgShowTrail;
    private bool dbgShowGizmos;
    private bool dbgLogCollisions;
    private bool dbgShowOverlay;

    private LineRenderer trailLine;
    private Queue<Vector3> trailPositions;
    private const int TrailMaxPoints = 80;
    private const float TrailWidth = 0.07f;

    // 디버그 표시용 마지막 충돌 정보
    private Vector3 lastContactPoint;
    private Vector3 lastContactNormal;
    private string lastCollisionDesc = "(none)";
    private int totalCollisions;

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
        originalScale = scale;
        if (meshRoot != null)
            meshRoot.localScale = Vector3.one * scale;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        if (rb != null && rb.velocity.sqrMagnitude > 0.001f)
            rb.velocity = rb.velocity.normalized * speed;
    }

    /// <summary>라인클리어 등 연출 중 볼 일시 정지/재개</summary>
    public void SetPaused(bool paused)
    {
        if (rb == null) return;
        if (paused && !isPaused)
        {
            savedVelocity = rb.velocity;
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            isPaused = true;
        }
        else if (!paused && isPaused)
        {
            rb.isKinematic = false;
            rb.velocity = savedVelocity;
            isPaused = false;
        }
    }

    /// <summary>TimeStop 아이템: 물리 정지 + 콜라이더 OFF + 반투명 페이드. 게임 진행 방해 없음.</summary>
    public void SetTimeStopState(bool active)
    {
        if (isTimeStopActive == active) return;
        isTimeStopActive = active;

        SetPaused(active);

        if (mainCollider == null) mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = !active;

        if (timeStopFadeCo != null) StopCoroutine(timeStopFadeCo);
        if (gameObject.activeInHierarchy)
            timeStopFadeCo = StartCoroutine(FadeAlpha(active ? TimeStopGhostAlpha : 1f, TimeStopFadeDuration));
    }

    private IEnumerator FadeAlpha(float target, float duration)
    {
        EnsureRendererCache();
        float start = currentVisualAlpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            ApplyAlphaToRenderers(a);
            yield return null;
        }
        ApplyAlphaToRenderers(target);
        timeStopFadeCo = null;
    }

    private void EnsureRendererCache()
    {
        if (cachedRenderers == null) cachedRenderers = GetComponentsInChildren<Renderer>(true);
        if (mpb == null) mpb = new MaterialPropertyBlock();
    }

    private void ApplyAlphaToRenderers(float alpha)
    {
        currentVisualAlpha = alpha;
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            var r = cachedRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            Color c = (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                ? r.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
            c.a = alpha;
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || isPaused) return;

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

    private void Update()
    {
        UpdateTrail();
    }

    // ── Debug API ──────────────────────────────────────────

    /// <summary>매니저에서 디버그 토글을 일괄 설정</summary>
    public void SetDebugFlags(bool showTrail, bool showGizmos, bool logCollisions, bool showOverlay)
    {
        dbgShowTrail = showTrail;
        dbgShowGizmos = showGizmos;
        dbgLogCollisions = logCollisions;
        dbgShowOverlay = showOverlay;

        if (!dbgShowTrail && trailLine != null)
            trailLine.gameObject.SetActive(false);
    }

    private void UpdateTrail()
    {
        if (!dbgShowTrail) return;
        EnsureTrailRenderer();
        trailLine.gameObject.SetActive(true);

        trailPositions.Enqueue(transform.position);
        while (trailPositions.Count > TrailMaxPoints) trailPositions.Dequeue();

        trailLine.positionCount = trailPositions.Count;
        int idx = 0;
        foreach (var p in trailPositions)
            trailLine.SetPosition(idx++, p);
    }

    private void EnsureTrailRenderer()
    {
        if (trailPositions == null) trailPositions = new Queue<Vector3>(TrailMaxPoints);
        if (trailLine != null) return;

        var go = new GameObject("BallTrail_Debug");
        go.transform.SetParent(null, true); // world space
        trailLine = go.AddComponent<LineRenderer>();
        trailLine.useWorldSpace = true;
        trailLine.startWidth = TrailWidth;
        trailLine.endWidth = TrailWidth * 0.3f;
        trailLine.material = new Material(Shader.Find("Sprites/Default"));
        trailLine.startColor = new Color(1f, 0.95f, 0.2f, 0.9f);
        trailLine.endColor = new Color(1f, 0.95f, 0.2f, 0f);
        trailLine.numCornerVertices = 2;
        trailLine.numCapVertices = 2;
        trailLine.sortingOrder = 100;
    }

    private void OnDrawGizmos()
    {
        if (!dbgShowGizmos || rb == null) return;
        // 현재 속도 벡터 (cyan)
        Gizmos.color = Color.cyan;
        Vector3 velDir = rb.velocity;
        if (velDir.sqrMagnitude > 0.01f)
        {
            velDir = velDir.normalized * 1.5f;
            Gizmos.DrawLine(transform.position, transform.position + velDir);
            Gizmos.DrawSphere(transform.position + velDir, 0.08f);
        }
        // 마지막 충돌 지점 + 노말 (red/magenta)
        if (lastContactNormal.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastContactPoint, 0.18f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastContactPoint, lastContactPoint + lastContactNormal * 1.0f);
        }
    }

    private void OnGUI()
    {
        if (!dbgShowOverlay || rb == null) return;
        int slot = GetInstanceID() & 0xFF;
        float y = 10f + (slot % 4) * 110f;
        var rect = new Rect(10f, y, 320f, 105f);
        GUI.Box(rect, GUIContent.none);
        var inner = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f);
        Vector3 v = rb.velocity;
        string label =
            $"<b>Ball [{slot:X2}]</b>\n" +
            $"Pos: ({transform.position.x:F2}, {transform.position.z:F2})\n" +
            $"Vel: ({v.x:F2}, {v.z:F2})  |v|={v.magnitude:F2}\n" +
            $"Speed: {speed:F2}  Stuck: {consecutiveCollisions}/{StuckThreshold}\n" +
            $"Total Hits: {totalCollisions}\n" +
            $"Last: {lastCollisionDesc}";
        var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
        GUI.Label(inner, label, style);
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        int otherLayer = other.layer;

        // 물리 노말 (벽/낙하블록 충돌에 사용)
        Vector3 contactNormal = Vector3.zero;
        Vector3 contactPoint = transform.position;
        if (collision.contactCount > 0)
        {
            var c0 = collision.GetContact(0);
            contactNormal = c0.normal;
            contactPoint = c0.point;
            contactNormal.y = 0f;
        }
        if (contactNormal.sqrMagnitude < 0.001f)
            contactNormal = -moveDirection;
        contactNormal.Normalize();

        // --- 갇힘 감지 ---
        float now = Time.time;
        if (now - lastCollisionTime < RapidCollisionWindow)
            consecutiveCollisions++;
        else
            consecutiveCollisions = 1;
        lastCollisionTime = now;
        totalCollisions++;
        lastContactPoint = contactPoint;
        lastContactNormal = contactNormal;

        if (consecutiveCollisions >= StuckThreshold)
        {
            lastCollisionDesc = $"STUCK→ESCAPE ({consecutiveCollisions} hits in window)";
            if (dbgLogCollisions) Debug.Log($"[Ball:{GetInstanceID() & 0xFF:X2}] {lastCollisionDesc}");
            EscapeStuck();
            return;
        }

        // --- 벽 충돌 ---
        if (otherLayer == wallLayer)
        {
            lastCollisionDesc = $"WALL n=({contactNormal.x:F2},{contactNormal.z:F2})";
            if (dbgLogCollisions) Debug.Log($"[Ball:{GetInstanceID() & 0xFF:X2}] {lastCollisionDesc}");
            PlayHitPunch(1.25f);
            ApplyReflection(contactNormal);
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
                lastCollisionDesc = "FALLING_BLOCK → RANDOMIZE";
                if (dbgLogCollisions) Debug.Log($"[Ball:{GetInstanceID() & 0xFF:X2}] {lastCollisionDesc}");
                if (gameManager != null)
                    gameManager.RandomizeCurrentBlock();
                PlayHitPunch(1.4f);
                ApplyReflection(contactNormal);
                return;
            }

            // 고정 블록 → 셀 파괴 + 점수 (그리드 기반 노말 사용 — 물리 노말은 모서리 충돌 시 대각선이라 옆 셀로 침투됨)
            Vector3 cubePos = other.transform.position;
            int gx = Mathf.RoundToInt(cubePos.x);
            int gz = Mathf.RoundToInt(cubePos.z);
            Vector3 gridNormal = ComputeGridBasedNormal(transform.position, cubePos);
            lastContactNormal = gridNormal;
            lastCollisionDesc = $"FIXED_CELL ({gx},{gz}) gridN=({gridNormal.x:F0},{gridNormal.z:F0})";
            if (dbgLogCollisions) Debug.Log($"[Ball:{GetInstanceID() & 0xFF:X2}] {lastCollisionDesc}");

            if (gameManager != null)
                gameManager.TryDestroyAndRemoveCubeAt(gx, gz, giveScore: true);
            PlayHitPunch(1.5f);
            ApplyReflection(gridNormal);

            // 인접 셀과 즉시 재충돌 방지: 노말 방향으로 살짝 밀어냄
            transform.position += gridNormal * PostCollisionPush;
            return;
        }

        // 기타 충돌
        lastCollisionDesc = $"OTHER layer={otherLayer}";
        if (dbgLogCollisions) Debug.Log($"[Ball:{GetInstanceID() & 0xFF:X2}] {lastCollisionDesc}");
        PlayHitPunch(1.2f);
        ApplyReflection(contactNormal);
    }

    private void OnDestroy()
    {
        if (trailLine != null)
            Destroy(trailLine.gameObject);
    }

    /// <summary>볼 위치 - 셀 위치의 우세 축으로 그리드 기반 노말 계산. 항상 축 정렬 (±X 또는 ±Z).</summary>
    private static Vector3 ComputeGridBasedNormal(Vector3 ballPos, Vector3 cellPos)
    {
        float dx = ballPos.x - cellPos.x;
        float dz = ballPos.z - cellPos.z;
        if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            return new Vector3(dx >= 0f ? 1f : -1f, 0f, 0f);
        return new Vector3(0f, 0f, dz >= 0f ? 1f : -1f);
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

    private void PlayHitPunch(float punchMult)
    {
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(HitPunchRoutine(punchMult));
    }

    private IEnumerator HitPunchRoutine(float punchMult)
    {
        float punchScale = originalScale * punchMult;
        float duration = 0.14f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = t / duration;
            float scale = ratio < 0.3f
                ? Mathf.Lerp(originalScale, punchScale, ratio / 0.3f)
                : Mathf.Lerp(punchScale, originalScale, (ratio - 0.3f) / 0.7f);
            if (meshRoot != null) meshRoot.localScale = Vector3.one * scale;
            yield return null;
        }
        if (meshRoot != null) meshRoot.localScale = Vector3.one * originalScale;
        punchCoroutine = null;
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
