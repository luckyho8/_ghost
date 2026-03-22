using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 테트리스 코어: 시간/게이지/속도 상승, 블록 스폰, 그리드 경계, UI 연동.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private const int MIN_GRID_Z = -12;

    [Header("Game Settings (Test Friendly)")]
    [Tooltip("기본 낙하 속도 (한 칸 이동까지의 초)")]
    public float baseFallSpeed = 1f;

    [Tooltip("속도 상승 시 적용되는 배율 (예: 0.9 = 더 빨라짐)")]
    [Range(0.5f, 1f)]
    public float speedMultiplier = 0.9f;

    [Tooltip("속도 상승 주기 (초)")]
    public float speedUpInterval = 40f;

    [Header("Data & Spawn")]
    [Tooltip("블록 데이터 SO")]
    public AllBlockData allBlockData;

    [Tooltip("블록 생성 위치")]
    public Transform spawnPoint;

    [Tooltip("다음 블록 대기 위치 (01=다음, 04=먼 미래)")]
    [SerializeField] private Transform[] nextPoints = new Transform[4];

    private List<BlockDataContents> nextQueue = new List<BlockDataContents>();
    private List<GameObject> nextQueueObjects = new List<GameObject>();

    [Header("Grid Bounds (Side_Wall)")]
    [Tooltip("그리드 X 최소 (좌측 벽)")]
    [SerializeField] private int gridMinX = -7;

    [Tooltip("그리드 X 최대 (우측 벽)")]
    [SerializeField] private int gridMaxX = 7;

    [Header("UI (Time & Gauge)")]
    [Tooltip("경과 시간 텍스트 (00:00:00 분:초:밀리초2자리)")]
    [SerializeField] private TextMeshProUGUI time_Txt;

    [Tooltip("속도 상승 게이지 (Slider.value가 speedUpInterval에 맞춰 감소)")]
    [SerializeField] private Slider gauge_Bar;

    [Tooltip("점수 UI (같은 오브젝트에 TextMeshProUGUI + UIBounceEffect)")]
    public UIBounceEffect scoreTextEffect;

    [Tooltip("레벨 UI (같은 오브젝트에 TextMeshProUGUI + UIBounceEffect)")]
    public UIBounceEffect levelTextEffect;

    [Header("UI Buttons")]
    [SerializeField] private Button Btn_Left;
    [SerializeField] private Button Btn_Right;
    [SerializeField] private Button Btn_Down;
    [SerializeField] private Button Btn_Rotate;
    [SerializeField] private Button Btn_HardDrop;

    [Header("Item (Ghost 등)")]
    [SerializeField] private ItemManager itemManager;

    [Header("Gimmick Ball")]
    [SerializeField] private GimmickBallManager gimmickBallManager;

    // ── 연출: FX 프리팹 슬롯 ──────────────────────────────────────────────
    // 파티클 프리팹은 흰색(1,1,1) 기준으로 제작하면 코드가 런타임에 색상을 주입합니다.
    [Header("연출 - FX 프리팹 (추후 파티클 연결)")]
    [Tooltip("블록을 내려놓을 때 발생하는 파티클 (블록 색상 자동 적용)")]
    [SerializeField] private GameObject fx_BlockPlace;

    [Tooltip("완료 예고 줄 하이라이트 파티클 (완료 색상 자동 적용)")]
    [SerializeField] private GameObject fx_LineHighlight;

    [Tooltip("줄 클리어 순간 발생하는 파티클 (클리어 색상 자동 적용)")]
    [SerializeField] private GameObject fx_LineClear;

    [Tooltip("셀레브레이션 다다닥 연출에 사용할 큐브 프리팹. 비워두면 기본 큐브로 대체됩니다.")]
    [SerializeField] private GameObject celebrationCubePrefab;

    [Tooltip("콤보 발생 시 파티클 (추후 연결)")]
    [SerializeField] private GameObject fx_Combo;

    [Header("연출 - 셀레브레이션 타이밍")]
    [Tooltip("다다닥 큐브 스폰 간격 (초)")]
    [SerializeField] private float celebrationSpawnInterval = 0.04f;
    [Tooltip("다다닥 큐브 축소 시작 간격 (초)")]
    [SerializeField] private float celebrationShrinkInterval = 0.03f;
    [Tooltip("큐브 하나가 축소되어 사라지는 시간 (초)")]
    [SerializeField] private float celebrationShrinkDuration = 0.12f;
    // ─────────────────────────────────────────────────────────────────────

    private float elapsedTime;
    private float gaugeTimer;
    private float currentFallSpeed;
    private float fallAccumulator;
    private int currentScore;
    private int currentCombo;
    private float currentBlockSpawnTime;
    private bool isTimeStopped = false;

    [Header("Level System")]
    public int currentLevel = 1;

    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, Transform> cellToCube = new Dictionary<Vector2Int, Transform>();

    // 각 셀의 원본 색상 (하이라이트 복원 및 셀레브레이션 색상에 사용)
    private Dictionary<Vector2Int, Color> cellBaseColors = new Dictionary<Vector2Int, Color>();

    // 현재 하이라이트된 셀과 복원용 원본 색상
    private List<int> highlightedRows = new List<int>();
    private Dictionary<Vector2Int, Color> highlightedOriginalColors = new Dictionary<Vector2Int, Color>();

    // 마지막으로 고정된 블록의 색상 (셀레브레이션에 사용)
    private Color lastFrozenBlockColor = Color.white;

    private FallingBlock currentBlock;
    private bool gameRunning = true;
    private GameObject ghostBlockRoot;
    private int gridMinZ => MIN_GRID_Z;
    private int gridMaxZ => spawnPoint != null ? Mathf.RoundToInt(spawnPoint.position.z) + 4 : 25;

    public int GridMinX => gridMinX;
    public int GridMaxX => gridMaxX;
    public int GridMinZ => gridMinZ;

    private void Start()
    {
        currentFallSpeed = baseFallSpeed;
        gaugeTimer = 0f;
        elapsedTime = 0f;
        fallAccumulator = 0f;
        currentScore = 0;
        currentCombo = 0;
        currentLevel = 1;
        if (scoreTextEffect != null)
        {
            var st = scoreTextEffect.GetComponent<TMPro.TextMeshProUGUI>();
            if (st != null) st.text = currentScore.ToString("D7");
        }
        UpdateLevelUI();
        SyncGaugeSliderRange();
        SetButtonNavigationToNone();
        BindButtonEvents();
        if (allBlockData != null && spawnPoint != null)
            SpawnBlock();
        if (itemManager != null && itemManager.isGhostActive)
            RefreshGhostState();
    }

    private void SetButtonNavigationToNone()
    {
        Button[] buttons = { Btn_Left, Btn_Right, Btn_Down, Btn_Rotate, Btn_HardDrop };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
        }
    }

    private void SyncGaugeSliderRange()
    {
        if (gauge_Bar != null)
        {
            gauge_Bar.minValue = 0f;
            gauge_Bar.maxValue = speedUpInterval;
            gauge_Bar.value = speedUpInterval;
        }
    }

    private void BindButtonEvents()
    {
        if (Btn_Left != null) Btn_Left.onClick.AddListener(Btn_Left_Press);
        if (Btn_Right != null) Btn_Right.onClick.AddListener(Btn_Right_Press);
        if (Btn_Down != null) Btn_Down.onClick.AddListener(Btn_Down_Press);
        if (Btn_Rotate != null) Btn_Rotate.onClick.AddListener(Btn_Rotate_Press);
        if (Btn_HardDrop != null) Btn_HardDrop.onClick.AddListener(Btn_HardDrop_Press);
    }

    private void Update()
    {
        if (!gameRunning) return;

        if (currentBlock != null)
        {
            if (Input.GetKeyDown(KeyCode.A)) MoveLeft();
            if (Input.GetKeyDown(KeyCode.D)) MoveRight();
            if (Input.GetKeyDown(KeyCode.S)) MoveDown();
            if (Input.GetKeyDown(KeyCode.W)) Rotate();
            if (Input.GetKeyDown(KeyCode.G)) HardDrop();
        }

        float dt = Time.deltaTime;
        elapsedTime += dt;
        UpdateTimeUI();
        UpdateGaugeAndSpeedUp(dt);

        if (currentBlock != null && !isTimeStopped)
        {
            fallAccumulator += dt;
            if (fallAccumulator >= currentFallSpeed)
            {
                fallAccumulator -= currentFallSpeed;
                if (!currentBlock.TryMoveDown())
                    FreezeCurrentBlock();
                else
                    UpdateLinePreview();
            }
        }

        if (itemManager != null && itemManager.isGhostActive && currentBlock != null)
            UpdateGhostPosition();
    }

    private void UpdateTimeUI()
    {
        if (time_Txt == null) return;
        int totalMs = Mathf.FloorToInt(elapsedTime * 1000f);
        int milliseconds = totalMs % 1000;
        int totalSec = totalMs / 1000;
        int seconds = totalSec % 60;
        int minutes = totalSec / 60;
        time_Txt.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds / 10);
    }

    private void UpdateScoreUI()
    {
        if (scoreTextEffect == null) return;
        var st = scoreTextEffect.GetComponent<TMPro.TextMeshProUGUI>();
        if (st != null) st.text = currentScore.ToString("D7");
        scoreTextEffect.PlayPopEffect();
    }

    private void UpdateLevelUI()
    {
        if (levelTextEffect == null) return;
        var lt = levelTextEffect.GetComponent<TMPro.TextMeshProUGUI>();
        if (lt != null) lt.text = $"Level\n{currentLevel}";
        levelTextEffect.PlayPopEffect();
    }

    private void UpdateGaugeAndSpeedUp(float dt)
    {
        gaugeTimer += dt;
        if (gauge_Bar != null)
        {
            gauge_Bar.maxValue = speedUpInterval;
            gauge_Bar.value = Mathf.Clamp(speedUpInterval - gaugeTimer, 0f, speedUpInterval);
        }

        if (gaugeTimer >= speedUpInterval)
        {
            gaugeTimer = 0f;
            currentFallSpeed *= speedMultiplier;
            if (currentFallSpeed < 0.05f) currentFallSpeed = 0.05f;
            if (gauge_Bar != null) gauge_Bar.value = speedUpInterval;
            currentLevel++;
            UpdateLevelUI();
            if (gimmickBallManager != null)
                gimmickBallManager.OnLevelChanged(currentLevel);
        }
    }

    private void ReleaseButtonFocus()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // --- 독립 입력 로직 (버튼/키보드 공통) ---
    public void MoveLeft()
    {
        if (currentBlock != null) currentBlock.TryMoveLeft();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
        UpdateLinePreview();
    }

    public void MoveRight()
    {
        if (currentBlock != null) currentBlock.TryMoveRight();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
        UpdateLinePreview();
    }

    public void MoveDown()
    {
        if (currentBlock != null && !currentBlock.TryMoveDown())
            FreezeCurrentBlock();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
        UpdateLinePreview();
    }

    public void Rotate()
    {
        if (currentBlock != null) currentBlock.TryRotateWithWallKick();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
        UpdateLinePreview();
    }

    public void HardDrop()
    {
        if (currentBlock != null) currentBlock.HardDrop();
    }

    // --- 버튼 OnClick: 동일 로직 호출 후 포커스 해제 ---
    public void Btn_Left_Press()
    {
        MoveLeft();
        ReleaseButtonFocus();
    }

    public void Btn_Right_Press()
    {
        MoveRight();
        ReleaseButtonFocus();
    }

    public void Btn_Down_Press()
    {
        MoveDown();
        ReleaseButtonFocus();
    }

    public void Btn_Rotate_Press()
    {
        Rotate();
        ReleaseButtonFocus();
    }

    public void Btn_HardDrop_Press()
    {
        HardDrop();
        ReleaseButtonFocus();
    }

    /// <summary>ItemManager에서 고스트 온/오프 시 호출. 씬의 고스트 표시 상태를 갱신한다.</summary>
    public void RefreshGhostState()
    {
        if (itemManager == null) return;
        if (itemManager.isGhostActive)
        {
            EnsureGhostRoot();
            RebuildGhostFromCurrentBlock();
            if (ghostBlockRoot != null) ghostBlockRoot.SetActive(true);
        }
        else
        {
            if (ghostBlockRoot != null) ghostBlockRoot.SetActive(false);
        }
    }

    public bool IsCellOccupied(int gx, int gz)
    {
        return occupiedCells.Contains(new Vector2Int(gx, gz));
    }

    /// <summary>해당 셀의 큐브를 파괴하고 occupiedCells/cellToCube에서 제거. (폭탄 등 아이템용)</summary>
    public bool TryDestroyAndRemoveCubeAt(int gx, int gz)
    {
        var cell = new Vector2Int(gx, gz);
        if (!cellToCube.TryGetValue(cell, out Transform t)) return false;
        if (t != null) Destroy(t.gameObject);
        cellToCube.Remove(cell);
        occupiedCells.Remove(cell);
        cellBaseColors.Remove(cell);
        return true;
    }

    public bool IsInBounds(int gx, int gz)
    {
        if (gz < MIN_GRID_Z) return false;
        return gx >= gridMinX && gx <= gridMaxX;
    }

    private static void ApplyBlockColorToCube(GameObject cube, Color blockColor)
    {
        var renderers = cube.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, blockColor);
            block.SetColor(ColorId, blockColor);
            r.SetPropertyBlock(block);
        }
    }

    private BlockDataContents GetRandomBlockData()
    {
        if (allBlockData == null || allBlockData.blockList == null || allBlockData.blockList.Count == 0)
            return null;
        return allBlockData.blockList[Random.Range(0, allBlockData.blockList.Count)];
    }

    private GameObject CreateBlockContainer(BlockDataContents data, Vector3 position, Quaternion rotation)
    {
        if (data == null || data.blockPrefab == null) return null;
        GameObject container = new GameObject("FallingBlock");
        container.transform.position = position;
        container.transform.rotation = rotation;

        var rb = container.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        for (int i = 0; i < 16; i++)
        {
            if (data.shapeData == null || i >= data.shapeData.Length || !data.shapeData[i]) continue;
            int row = i / 4;
            int col = i % 4;
            Vector3 localPos = new Vector3(col, 0f, -row);
            GameObject cube = Instantiate(data.blockPrefab, container.transform);
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = Vector3.one;
            ApplyBlockColorToCube(cube, data.blockColor);
        }
        return container;
    }

    public void SpawnBlock()
    {
        if (allBlockData == null || allBlockData.blockList == null || allBlockData.blockList.Count == 0 || spawnPoint == null)
            return;

        if (nextQueue.Count == 0)
            InitNextQueue();

        // 큐 앞에서 꺼내 현재 블록으로 스폰 (X축 자동 센터링)
        var spawnData = nextQueue[0];
        var spawnObj = nextQueueObjects[0];
        nextQueue.RemoveAt(0);
        nextQueueObjects.RemoveAt(0);

        int pivotX = Mathf.RoundToInt(spawnPoint.position.x) - GetCenterOffsetX(spawnData);
        int pivotZ = Mathf.RoundToInt(spawnPoint.position.z);
        spawnObj.transform.position = new Vector3(pivotX, spawnPoint.position.y, spawnPoint.position.z);
        spawnObj.transform.rotation = spawnPoint.rotation;
        currentBlock = spawnObj.AddComponent<FallingBlock>();
        currentBlock.Init(this, spawnData, pivotX, pivotZ);

        // 남은 3개를 한 칸씩 앞으로 이동 (X축 센터링 유지)
        for (int i = 0; i < nextQueueObjects.Count; i++)
        {
            if (nextQueueObjects[i] != null && i < nextPoints.Length && nextPoints[i] != null)
            {
                Vector3 pos = nextPoints[i].position;
                pos.x -= GetCenterOffsetX(nextQueue[i]);
                nextQueueObjects[i].transform.position = pos;
                nextQueueObjects[i].transform.rotation = nextPoints[i].rotation;
            }
        }

        // 맨 뒤(04번 슬롯)에 새 블록 추가 (X축 센터링)
        var newData = GetRandomBlockData();
        nextQueue.Add(newData);
        int lastIdx = nextQueue.Count - 1;
        GameObject newObj = null;
        if (lastIdx < nextPoints.Length && nextPoints[lastIdx] != null)
        {
            Vector3 newPos = nextPoints[lastIdx].position;
            newPos.x -= GetCenterOffsetX(newData);
            newObj = CreateBlockContainer(newData, newPos, nextPoints[lastIdx].rotation);
        }
        nextQueueObjects.Add(newObj);

        UpdateNextQueueVisibility();

        fallAccumulator = 0f;
        currentBlockSpawnTime = Time.time;

        if (itemManager != null && itemManager.isGhostActive && ghostBlockRoot != null)
        {
            RebuildGhostFromCurrentBlock();
            ghostBlockRoot.SetActive(true);
        }
    }

    private void InitNextQueue()
    {
        for (int i = 0; i < 4; i++)
        {
            var data = GetRandomBlockData();
            nextQueue.Add(data);
            GameObject obj = null;
            if (i < nextPoints.Length && nextPoints[i] != null)
            {
                Vector3 pos = nextPoints[i].position;
                pos.x -= GetCenterOffsetX(data);
                obj = CreateBlockContainer(data, pos, nextPoints[i].rotation);
            }
            nextQueueObjects.Add(obj);
        }
        UpdateNextQueueVisibility();
    }

    private void UpdateNextQueueVisibility()
    {
        int visible = GetVisibleNextCount();
        for (int i = 0; i < nextQueueObjects.Count; i++)
        {
            if (nextQueueObjects[i] != null)
                nextQueueObjects[i].SetActive(i < visible);
        }
    }

    /// <summary>레벨에 따라 넥스트 큐 표시 개수: 1~3레벨=4개, 4~5=3개, 6~7=2개, 8+=1개</summary>
    private int GetVisibleNextCount()
    {
        if (currentLevel <= 3) return 4;
        if (currentLevel <= 5) return 3;
        if (currentLevel <= 7) return 2;
        return 1;
    }

    /// <summary>블록의 실제 셀 범위를 계산해 X축 중앙 정렬에 필요한 오프셋 반환. 피봇 위치 무관하게 항상 중앙 스폰.</summary>
    private int GetCenterOffsetX(BlockDataContents data)
    {
        if (data?.shapeData == null) return 0;
        int minCol = int.MaxValue, maxCol = int.MinValue;
        for (int i = 0; i < 16 && i < data.shapeData.Length; i++)
        {
            if (!data.shapeData[i]) continue;
            int col = i % 4;
            if (col < minCol) minCol = col;
            if (col > maxCol) maxCol = col;
        }
        if (minCol == int.MaxValue) return 0;
        return Mathf.RoundToInt((minCol + maxCol) / 2f);
    }

    public void FreezeCurrentBlock()
    {
        if (currentBlock == null) return;

        // 셀레브레이션 색상 보존 + 하이라이트 해제
        lastFrozenBlockColor = currentBlock.BlockColor;
        ClearLinePreview();

        // 블록 내려놓기 파티클 (추후 연결)
        SpawnParticleWithColor(fx_BlockPlace, currentBlock.transform.position, lastFrozenBlockColor);

        float placementTime = Time.time - currentBlockSpawnTime;
        int placementScore = placementTime <= 0.5f ? 300 : placementTime <= 0.8f ? 200 : placementTime <= 1.2f ? 150 : 100;
        currentScore += placementScore;
        UpdateScoreUI();

        RegisterFrozenBlockToGrid(currentBlock);
        Destroy(currentBlock.gameObject);
        currentBlock = null;
        StartCoroutine(LineClearThenSpawn());
    }

    private void RegisterFrozenBlockToGrid(FallingBlock block)
    {
        Color blockColor = block.BlockColor;
        Transform container = block.transform;
        var children = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
            children.Add(container.GetChild(i));

        foreach (Transform cube in children)
        {
            Vector3 w = cube.position;
            int gx = Mathf.RoundToInt(w.x);
            int gz = Mathf.RoundToInt(w.z);
            var cell = new Vector2Int(gx, gz);
            occupiedCells.Add(cell);
            cellToCube[cell] = cube;
            cellBaseColors[cell] = blockColor;
            cube.SetParent(null);
            cube.gameObject.tag = "FixedCube"; // 기믹 볼 충돌 감지용
        }
    }

    private System.Collections.IEnumerator LineClearThenSpawn()
    {
        yield return new WaitForSeconds(0.1f);
        var clearedRows = ClearFullLines();
        if (clearedRows.Count > 0)
            yield return StartCoroutine(CelebrationEffect(clearedRows));
        SpawnBlock();
    }

    /// <summary>꽉 찬 줄을 탐색·파괴·하강 처리. 클리어된 줄의 (Z좌표, 색상, Y높이) 목록 반환.</summary>
    private List<(int z, Color color, float y)> ClearFullLines()
    {
        var fullLines = new List<int>();
        for (int z = gridMinZ; z <= gridMaxZ; z++)
        {
            bool full = true;
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                if (!occupiedCells.Contains(new Vector2Int(x, z))) { full = false; break; }
            }
            if (full) fullLines.Add(z);
        }

        var clearedRows = new List<(int z, Color color, float y)>();

        if (fullLines.Count > 0)
        {
            foreach (int z in fullLines)
            {
                // 클리어 전 색상과 Y 높이 캡처
                Color rowColor = lastFrozenBlockColor;
                float rowY = 0f;
                for (int x = gridMinX; x <= gridMaxX; x++)
                {
                    var sampleCell = new Vector2Int(x, z);
                    if (cellToCube.TryGetValue(sampleCell, out Transform sample) && sample != null)
                    {
                        rowY = sample.position.y;
                        break;
                    }
                }
                clearedRows.Add((z, rowColor, rowY));

                // 큐브 파괴
                for (int x = gridMinX; x <= gridMaxX; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (cellToCube.TryGetValue(cell, out Transform t))
                    {
                        // 클리어 파티클 (추후 연결)
                        if (t != null)
                        {
                            SpawnParticleWithColor(fx_LineClear, t.position, rowColor);
                            Destroy(t.gameObject);
                        }
                        cellToCube.Remove(cell);
                        occupiedCells.Remove(cell);
                        cellBaseColors.Remove(cell);
                    }
                }
            }
            ShiftRowsDown(fullLines);
        }

        int clearedLineCount = fullLines.Count;
        if (clearedLineCount > 0)
        {
            int lineScore = clearedLineCount == 1 ? 100 : clearedLineCount == 2 ? 300 : clearedLineCount == 3 ? 500 : 800;
            currentScore += lineScore;
            currentScore += currentCombo * 50;
            currentCombo++;
            UpdateScoreUI();
        }
        else
        {
            currentCombo = 0;
        }

        return clearedRows;
    }

    /// <summary>데이터 주도형: 바닥부터 최상단 Z 순회, 삭제된 줄 수(shiftCount) 누적 후 위쪽 라인만 하강. (라인클리어·폭탄 아이템 등에서 호출)</summary>
    public void ShiftRowsDown(List<int> clearedLines)
    {
        if (clearedLines == null || clearedLines.Count == 0) return;

        var clearedSet = new HashSet<int>(clearedLines);

        for (int z = gridMinZ; z <= gridMaxZ; z++)
        {
            if (clearedSet.Contains(z)) continue;

            int shiftCount = 0;
            for (int i = 0; i < clearedLines.Count; i++)
                if (clearedLines[i] < z) shiftCount++;

            if (shiftCount == 0) continue;

            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                var cell = new Vector2Int(x, z);
                if (!cellToCube.TryGetValue(cell, out Transform t) || t == null) continue;

                cellToCube.Remove(cell);
                occupiedCells.Remove(cell);

                Color savedColor = cellBaseColors.TryGetValue(cell, out Color c) ? c : Color.white;
                cellBaseColors.Remove(cell);

                int newZ = z - shiftCount;
                var newCell = new Vector2Int(x, newZ);
                cellToCube[newCell] = t;
                occupiedCells.Add(newCell);
                cellBaseColors[newCell] = savedColor;

                t.position = new Vector3(x, t.position.y, newZ);
            }
        }
    }

    // --- Item Actions ---

    /// <summary>Time Stop: duration 동안 블록 자동 낙하 중단. 유저 조작은 유지.</summary>
    public System.Collections.IEnumerator DoTimeStop(float duration)
    {
        isTimeStopped = true;
        yield return new WaitForSeconds(duration);
        isTimeStopped = false;
    }

    /// <summary>Level Down: 레벨을 amount만큼 낮추고 낙하 속도 재계산.</summary>
    public void DecreaseLevel(int amount, int minLevel)
    {
        currentLevel = Mathf.Max(currentLevel - amount, minLevel);
        currentFallSpeed = baseFallSpeed * Mathf.Pow(speedMultiplier, currentLevel - 1);
        gaugeTimer = 0f;
        UpdateLevelUI();
    }

    /// <summary>Reroll: 현재 블록을 파괴하고 지정 인덱스의 블록으로 즉시 교체 스폰.</summary>
    public void ReplaceCurrentBlockWith(int blockIndex)
    {
        if (currentBlock == null || allBlockData == null) return;
        if (blockIndex < 0 || blockIndex >= allBlockData.blockList.Count) return;

        ClearLinePreview();
        if (ghostBlockRoot != null) ghostBlockRoot.SetActive(false);
        Destroy(currentBlock.gameObject);
        currentBlock = null;

        var data = allBlockData.blockList[blockIndex];
        int pivotX = Mathf.RoundToInt(spawnPoint.position.x) - GetCenterOffsetX(data);
        int pivotZ = Mathf.RoundToInt(spawnPoint.position.z);

        var container = CreateBlockContainer(data, new Vector3(pivotX, spawnPoint.position.y, spawnPoint.position.z), spawnPoint.rotation);
        currentBlock = container.AddComponent<FallingBlock>();
        currentBlock.Init(this, data, pivotX, pivotZ);
        fallAccumulator = 0f;
        currentBlockSpawnTime = Time.time;

        if (itemManager != null && itemManager.isGhostActive)
        {
            EnsureGhostRoot();
            RebuildGhostFromCurrentBlock();
            ghostBlockRoot.SetActive(true);
        }
    }

    /// <summary>기믹 볼: 낙하 중 블록을 현재 위치에서 랜덤 블록으로 교체 (형태+색상 변경).</summary>
    public void RandomizeCurrentBlock()
    {
        if (currentBlock == null || allBlockData == null || allBlockData.blockList.Count == 0) return;

        // 현재 위치/회전/피벗 저장
        Vector3 curPos = currentBlock.transform.position;
        Quaternion curRot = currentBlock.transform.rotation;
        int pivotX = currentBlock.PivotGridX;
        int pivotZ = currentBlock.PivotGridZ;

        // 기존 블록 파괴
        ClearLinePreview();
        if (ghostBlockRoot != null) ghostBlockRoot.SetActive(false);
        Destroy(currentBlock.gameObject);
        currentBlock = null;

        // 랜덤 블록 데이터로 현재 위치에 재생성
        var newData = allBlockData.blockList[Random.Range(0, allBlockData.blockList.Count)];
        var container = CreateBlockContainer(newData, curPos, curRot);
        currentBlock = container.AddComponent<FallingBlock>();
        currentBlock.Init(this, newData, pivotX, pivotZ);

        if (itemManager != null && itemManager.isGhostActive)
        {
            EnsureGhostRoot();
            RebuildGhostFromCurrentBlock();
            ghostBlockRoot.SetActive(true);
        }
    }

    /// <summary>Gravity: 아이템 1회 사용. 각 큐브를 최대 dropCells칸만큼 아래로 내려 빈 공간을 압축.</summary>
    public System.Collections.IEnumerator GravityCompression(float delay, int dropCells = 1)
    {
        for (int z = gridMinZ + 1; z <= gridMaxZ; z++)
        {
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                var cell = new Vector2Int(x, z);
                if (!cellToCube.TryGetValue(cell, out Transform t) || t == null) continue;

                int drop = 0;
                for (int d = 1; d <= dropCells; d++)
                {
                    int targetZ = z - d;
                    if (targetZ < gridMinZ) break;
                    if (occupiedCells.Contains(new Vector2Int(x, targetZ))) break;
                    drop = d;
                }
                if (drop == 0) continue;

                int newZ = z - drop;
                cellToCube.Remove(cell);
                occupiedCells.Remove(cell);

                Color savedColor = cellBaseColors.TryGetValue(cell, out Color c) ? c : Color.white;
                cellBaseColors.Remove(cell);

                var newCell = new Vector2Int(x, newZ);
                cellToCube[newCell] = t;
                occupiedCells.Add(newCell);
                cellBaseColors[newCell] = savedColor;

                t.position = new Vector3(x, t.position.y, newZ);
            }
        }
        yield return new WaitForSeconds(delay);

        var clearedRows = ClearFullLines();
        if (clearedRows.Count > 0)
            yield return StartCoroutine(CelebrationEffect(clearedRows));
    }

    // ── 라인 완료 예고 하이라이트 ────────────────────────────────────────

    /// <summary>현재 블록을 하드드롭했을 때 완성될 줄을 시뮬레이션해 반환.</summary>
    private List<int> GetWouldCompleteRows()
    {
        if (currentBlock == null) return new List<int>();

        // 하드드롭 위치 계산 (고스트 피스와 동일한 로직)
        Transform block = currentBlock.transform;
        Quaternion rot = block.rotation;
        Vector3 pos = block.position;
        pos.x = Mathf.RoundToInt(pos.x);
        pos.y = Mathf.RoundToInt(pos.y);
        pos.z = Mathf.RoundToInt(pos.z);

        var cells = GetCellsForPose(block, pos, rot);
        while (AllCellsValid(cells))
        {
            pos.z -= 1f;
            cells = GetCellsForPose(block, pos, rot);
        }
        pos.z += 1f;
        cells = GetCellsForPose(block, pos, rot);

        // 하드드롭 위치에서 완성되는 줄 시뮬레이션
        var simulated = new HashSet<Vector2Int>(occupiedCells);
        foreach (var c in cells) simulated.Add(c);

        var result = new List<int>();
        for (int z = gridMinZ; z <= gridMaxZ; z++)
        {
            bool full = true;
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                if (!simulated.Contains(new Vector2Int(x, z))) { full = false; break; }
            }
            if (full) result.Add(z);
        }
        return result;
    }

    /// <summary>완료 예고 하이라이트 갱신. 블록이 이동할 때마다 호출.</summary>
    private void UpdateLinePreview()
    {
        ClearLinePreview();
        if (currentBlock == null) return;

        var rows = GetWouldCompleteRows();
        if (rows.Count == 0) return;

        Color highlightColor = currentBlock.BlockColor;
        highlightedRows = rows;

        foreach (int z in rows)
        {
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                var cell = new Vector2Int(x, z);
                if (!cellToCube.TryGetValue(cell, out Transform t) || t == null) continue;

                // 원본 색상 백업
                if (cellBaseColors.TryGetValue(cell, out Color orig))
                    highlightedOriginalColors[cell] = orig;

                // 완료 색상으로 변경
                ApplyBlockColorToCube(t.gameObject, highlightColor);

                // 하이라이트 파티클 (추후 연결) — 위치당 1회만 스폰하려면 별도 관리 필요
                // SpawnParticleWithColor(fx_LineHighlight, t.position, highlightColor);
            }
        }
    }

    /// <summary>하이라이트된 줄을 원래 색상으로 복원.</summary>
    private void ClearLinePreview()
    {
        foreach (var kvp in highlightedOriginalColors)
        {
            if (cellToCube.TryGetValue(kvp.Key, out Transform t) && t != null)
                ApplyBlockColorToCube(t.gameObject, kvp.Value);
        }
        highlightedOriginalColors.Clear();
        highlightedRows.Clear();
    }

    // ── 셀레브레이션 연출 ────────────────────────────────────────────────

    /// <summary>줄 클리어 후 다다닥~ 나타났다가 다다닥~ 사라지는 셀레브레이션 연출.</summary>
    private System.Collections.IEnumerator CelebrationEffect(List<(int z, Color color, float y)> clearedRows)
    {
        int totalCols = gridMaxX - gridMinX + 1;
        var spawnedCubes = new List<(GameObject go, int x)>();

        // 오른쪽 → 왼쪽으로 순차 스폰
        for (int col = 0; col < totalCols; col++)
        {
            int x = gridMaxX - col;
            foreach (var (z, color, y) in clearedRows)
            {
                var go = CreateCelebrationCube(new Vector3(x, y, z), color);
                spawnedCubes.Add((go, x));
            }
            yield return new WaitForSeconds(celebrationSpawnInterval);
        }

        yield return new WaitForSeconds(0.08f);

        // 왼쪽 → 오른쪽으로 순차 축소
        for (int x = gridMinX; x <= gridMaxX; x++)
        {
            foreach (var (go, gx) in spawnedCubes)
            {
                if (gx == x && go != null)
                    StartCoroutine(ShrinkAndDestroy(go, celebrationShrinkDuration));
            }
            yield return new WaitForSeconds(celebrationShrinkInterval);
        }

        yield return new WaitForSeconds(celebrationShrinkDuration);
    }

    /// <summary>셀레브레이션용 임시 큐브 생성. 프리팹 미할당 시 기본 큐브로 대체.</summary>
    private GameObject CreateCelebrationCube(Vector3 pos, Color color)
    {
        GameObject go;
        if (celebrationCubePrefab != null)
        {
            go = Instantiate(celebrationCubePrefab, pos, Quaternion.identity);
            ApplyBlockColorToCube(go, color);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }
        return go;
    }

    private System.Collections.IEnumerator ShrinkAndDestroy(GameObject go, float duration)
    {
        if (go == null) yield break;
        float elapsed = 0f;
        Vector3 startScale = go.transform.localScale;
        while (elapsed < duration)
        {
            if (go == null) yield break;
            elapsed += Time.deltaTime;
            go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ── 파티클 공통 헬퍼 ────────────────────────────────────────────────

    /// <summary>
    /// 파티클 프리팹을 지정 위치에 스폰하고 색상을 주입합니다.
    /// 프리팹의 모든 ParticleSystem.main.startColor에 color가 적용됩니다.
    /// 프리팹은 흰색(1,1,1) 기준으로 제작하세요.
    /// </summary>
    private void SpawnParticleWithColor(GameObject prefab, Vector3 pos, Color color)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.identity);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    // --- Ghost Piece ---
    private void EnsureGhostRoot()
    {
        if (ghostBlockRoot != null) return;
        ghostBlockRoot = new GameObject("GhostBlock");
    }

    private void RebuildGhostFromCurrentBlock()
    {
        if (ghostBlockRoot == null || currentBlock == null) return;
        for (int i = ghostBlockRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(ghostBlockRoot.transform.GetChild(i).gameObject);

        Transform src = currentBlock.transform;
        for (int i = 0; i < src.childCount; i++)
        {
            Transform srcCube = src.GetChild(i);
            GameObject copy = Instantiate(srcCube.gameObject, ghostBlockRoot.transform);
            copy.transform.localPosition = srcCube.localPosition;
            copy.transform.localRotation = srcCube.localRotation;
            copy.transform.localScale = srcCube.localScale;
            ApplyGhostMaterial(copy);
            SetGhostCollidersNonBlocking(copy);
        }
    }

    private void ApplyGhostMaterial(GameObject cube)
    {
        var renderers = cube.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material ghostMat = itemManager != null ? itemManager.ghostMaterial : null;
            if (ghostMat != null)
            {
                r.material = ghostMat;
            }
        }
    }

    /// <summary>고스트 큐브는 물리/판정에 관여하지 않도록 Collider를 모두 isTrigger로 설정.</summary>
    private static void SetGhostCollidersNonBlocking(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.isTrigger = true;
    }

    private List<Vector2Int> GetCellsForPose(Transform blockRoot, Vector3 worldPos, Quaternion worldRot)
    {
        var list = new List<Vector2Int>();
        for (int i = 0; i < blockRoot.childCount; i++)
        {
            Transform c = blockRoot.GetChild(i);
            Vector3 w = worldPos + worldRot * c.localPosition;
            list.Add(new Vector2Int(Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.z)));
        }
        return list;
    }

    private bool AllCellsValid(List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            if (!IsInBounds(c.x, c.y)) return false;
            if (IsCellOccupied(c.x, c.y)) return false;
        }
        return true;
    }

    private void UpdateGhostPosition()
    {
        if (ghostBlockRoot == null || currentBlock == null) return;
        if (itemManager == null || !itemManager.isGhostActive) return;

        Transform block = currentBlock.transform;
        Quaternion rot = block.rotation;

        Vector3 pos = block.position;
        pos.x = Mathf.RoundToInt(pos.x);
        pos.y = Mathf.RoundToInt(pos.y);
        pos.z = Mathf.RoundToInt(pos.z);

        List<Vector2Int> cells = GetCellsForPose(block, pos, rot);
        while (AllCellsValid(cells))
        {
            pos.z -= 1f;
            cells = GetCellsForPose(block, pos, rot);
        }
        pos.z += 1f;

        int snapX = Mathf.RoundToInt(pos.x);
        int snapY = Mathf.RoundToInt(pos.y);
        int snapZ = Mathf.RoundToInt(pos.z);
        ghostBlockRoot.transform.position = new Vector3(snapX, snapY, snapZ);
        ghostBlockRoot.transform.rotation = rot;
    }
}
