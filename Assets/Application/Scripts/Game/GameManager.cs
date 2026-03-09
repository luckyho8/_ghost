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

    private const int MIN_GRID_Z = -19;

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

    [Tooltip("다음 블록 대기 위치")]
    [SerializeField] private Transform nextPoint;

    private GameObject nextBlockObject;
    private BlockDataContents nextBlockData;

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

    private float elapsedTime;
    private float gaugeTimer;
    private float currentFallSpeed;
    private float fallAccumulator;
    private int currentScore;
    private int currentCombo;
    private float currentBlockSpawnTime;

    [Header("Level System")]
    public int currentLevel = 1;

    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, Transform> cellToCube = new Dictionary<Vector2Int, Transform>();
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

        if (currentBlock != null)
        {
            fallAccumulator += dt;
            if (fallAccumulator >= currentFallSpeed)
            {
                fallAccumulator -= currentFallSpeed;
                if (!currentBlock.TryMoveDown())
                    FreezeCurrentBlock();
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
    }

    public void MoveRight()
    {
        if (currentBlock != null) currentBlock.TryMoveRight();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
    }

    public void MoveDown()
    {
        if (currentBlock != null && !currentBlock.TryMoveDown())
            FreezeCurrentBlock();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
    }

    public void Rotate()
    {
        if (currentBlock != null) currentBlock.TryRotateWithWallKick();
        if (itemManager != null && itemManager.isGhostActive && currentBlock != null) UpdateGhostPosition();
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

        int pivotX = Mathf.RoundToInt(spawnPoint.position.x);
        int pivotY = Mathf.RoundToInt(spawnPoint.position.y);
        int pivotZ = Mathf.RoundToInt(spawnPoint.position.z);

        if (nextBlockObject == null)
        {
            BlockDataContents data = GetRandomBlockData();
            if (data == null) return;
            GameObject container = CreateBlockContainer(data, spawnPoint.position, spawnPoint.rotation);
            if (container == null) return;
            currentBlock = container.AddComponent<FallingBlock>();
            currentBlock.Init(this, data, pivotX, pivotZ);
        }
        else
        {
            nextBlockObject.transform.position = spawnPoint.position;
            nextBlockObject.transform.rotation = spawnPoint.rotation;
            currentBlock = nextBlockObject.AddComponent<FallingBlock>();
            currentBlock.Init(this, nextBlockData, pivotX, pivotZ);
            nextBlockObject = null;
        }

        if (nextPoint != null)
        {
            nextBlockData = GetRandomBlockData();
            if (nextBlockData != null)
                nextBlockObject = CreateBlockContainer(nextBlockData, nextPoint.position, nextPoint.rotation);
        }

        fallAccumulator = 0f;
        currentBlockSpawnTime = Time.time;

        if (itemManager != null && itemManager.isGhostActive && ghostBlockRoot != null)
        {
            RebuildGhostFromCurrentBlock();
            ghostBlockRoot.SetActive(true);
        }
    }

    public void FreezeCurrentBlock()
    {
        if (currentBlock == null) return;

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
            cube.SetParent(null);
        }
    }

    private System.Collections.IEnumerator LineClearThenSpawn()
    {
        yield return new WaitForSeconds(0.1f);

        List<int> fullLines = new List<int>();
        for (int z = gridMinZ; z <= gridMaxZ; z++)
        {
            bool full = true;
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                if (!occupiedCells.Contains(new Vector2Int(x, z)))
                {
                    full = false;
                    break;
                }
            }
            if (full) fullLines.Add(z);
        }

        if (fullLines.Count > 0)
        {
            foreach (int z in fullLines)
            {
                for (int x = gridMinX; x <= gridMaxX; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (cellToCube.TryGetValue(cell, out Transform t))
                    {
                        if (t != null) Destroy(t.gameObject);
                        cellToCube.Remove(cell);
                        occupiedCells.Remove(cell);
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
        }
        else
        {
            currentCombo = 0;
        }
        if (clearedLineCount > 0)
            UpdateScoreUI();

        SpawnBlock();
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

                int newZ = z - shiftCount;
                var newCell = new Vector2Int(x, newZ);
                cellToCube[newCell] = t;
                occupiedCells.Add(newCell);

                t.position = new Vector3(x, t.position.y, newZ);
            }
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
        Color ghostColor = new Color(1f, 1f, 1f, 0.3f);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material ghostMat = itemManager != null ? itemManager.ghostMaterial : null;
            if (ghostMat != null)
            {
                r.material = ghostMat;
            }
            else
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, ghostColor);
                block.SetColor(ColorId, ghostColor);
                r.SetPropertyBlock(block);
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
