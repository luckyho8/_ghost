using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX 파티클 풀링 및 카메라 셰이크 관리.
/// 씬의 VFX_Manager 오브젝트에 부착.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("풀 초기 생성 수")]
    [SerializeField] private int defaultPoolSize = 10;

    [Header("Camera Shake")]
    [Tooltip("셰이크 대상 카메라 (비워두면 Camera.main)")]
    [SerializeField] private Camera shakeCamera;
    [Tooltip("기본 셰이크 강도")]
    [SerializeField] private float defaultShakeIntensity = 0.15f;
    [Tooltip("기본 셰이크 지속 시간")]
    [SerializeField] private float defaultShakeDuration = 0.2f;

    // 프리팹별 풀
    private Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> _prefabLookup = new Dictionary<GameObject, GameObject>();

    // 카메라 셰이크
    private Coroutine _shakeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (shakeCamera == null)
            shakeCamera = Camera.main;
    }

    // ── Pool ────────────────────────────────────────────────────────

    /// <summary>프리팹의 풀을 미리 생성</summary>
    public void Warmup(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        var pool = _pools[prefab];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            pool.Enqueue(go);
            _prefabLookup[go] = prefab;
        }
    }

    /// <summary>풀에서 파티클을 꺼내 위치에 스폰. 재생 완료 후 자동 반환.</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Color? color = null)
    {
        if (prefab == null) return null;

        if (!_pools.ContainsKey(prefab))
            Warmup(prefab, defaultPoolSize);

        var pool = _pools[prefab];
        GameObject go;

        if (pool.Count > 0)
        {
            go = pool.Dequeue();
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
        }
        else
        {
            go = Instantiate(prefab, position, Quaternion.identity, transform);
            _prefabLookup[go] = prefab;
        }

        // 색상 적용 (cube 파티클의 startColor)
        if (color.HasValue)
            ApplyColor(go, color.Value);

        go.SetActive(true);

        // 모든 파티클 시스템 재시작
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        StartCoroutine(ReturnAfterFinished(go, prefab));
        return go;
    }

    /// <summary>특정 자식 파티클에만 색상 적용</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, string childName, Color color)
    {
        if (prefab == null) return null;

        if (!_pools.ContainsKey(prefab))
            Warmup(prefab, defaultPoolSize);

        var pool = _pools[prefab];
        GameObject go;

        if (pool.Count > 0)
        {
            go = pool.Dequeue();
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
        }
        else
        {
            go = Instantiate(prefab, position, Quaternion.identity, transform);
            _prefabLookup[go] = prefab;
        }

        // 지정된 자식의 startColor만 변경
        ApplyColorToChild(go, childName, color);

        go.SetActive(true);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        StartCoroutine(ReturnAfterFinished(go, prefab));
        return go;
    }

    private void ApplyColor(GameObject go, Color color)
    {
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>자식 파티클 렌더러의 머티리얼 _BaseColor를 변경</summary>
    private void ApplyColorToChild(GameObject go, string childName, Color color)
    {
        Transform child = go.transform.Find(childName);
        if (child == null) return;
        var pr = child.GetComponent<ParticleSystemRenderer>();
        if (pr == null) return;
        // 인스턴스 머티리얼의 _BaseColor 변경
        Material mat = pr.material;
        mat.SetColor(BaseColorId, color);
        mat.SetColor(ColorId, color);
    }

    private IEnumerator ReturnAfterFinished(GameObject go, GameObject prefab)
    {
        // 파티클이 재생 완료될 때까지 대기
        yield return new WaitForSeconds(0.1f); // 최소 대기
        var particles = go.GetComponentsInChildren<ParticleSystem>(true);
        while (true)
        {
            bool anyAlive = false;
            foreach (var ps in particles)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }
            if (!anyAlive) break;
            yield return null;
        }

        go.SetActive(false);
        if (_pools.ContainsKey(prefab))
            _pools[prefab].Enqueue(go);
    }

    // ── Camera Shake ────────────────────────────────────────────────

    /// <summary>기본 설정으로 카메라 셰이크</summary>
    public void Shake()
    {
        Shake(defaultShakeIntensity, defaultShakeDuration);
    }

    /// <summary>강도와 지속시간을 지정하여 카메라 셰이크</summary>
    public void Shake(float intensity, float duration)
    {
        if (shakeCamera == null) return;
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        Vector3 originalPos = shakeCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration); // 감쇠
            float offsetX = Random.Range(-1f, 1f) * intensity * t;
            float offsetZ = Random.Range(-1f, 1f) * intensity * t;
            shakeCamera.transform.localPosition = originalPos + new Vector3(offsetX, 0f, offsetZ);
            yield return null;
        }

        shakeCamera.transform.localPosition = originalPos;
        _shakeCoroutine = null;
    }
}
