using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ItemSlotUI
{
    public GameObject plusObj;
    public GameObject adsObj;
    public GameObject plusNumObj;
    public TextMeshProUGUI txtNum;
}

/// <summary>
/// 아이템 옵션 및 버튼 관리. 수량 제한(최대 9), UI 상태, 고스트 1회용 발동.
/// </summary>
public class ItemManager : MonoBehaviour
{
    private const int MaxItemCount = 9;
    private const int GhostItemIndex = 5;

    [Header("참조")]
    public GameManager gameManager;

    [Header("아이템 수량 및 UI (0~5: Bomb, TimeStop, LevelDown, Reroll, Gravity, Ghost)")]
    public int[] itemCounts = new int[6];
    public ItemSlotUI[] itemUIs = new ItemSlotUI[6];

    [Header("1. Bomb Item")]
    [SerializeField] private int bombClearRows = 3;
    public Button btn_Bomb;

    [Header("2. Time Stop Item")]
    [SerializeField] private float timeStopDuration = 10f;
    public Button btn_TimeStop;

    [Header("3. Level Down Item")]
    [SerializeField] private int levelDecreaseAmount = 2;
    [SerializeField] private int minLevelLimit = 1;
    public Button btn_LevelDown;

    [Header("4. I-Block Reroll Item")]
    [Tooltip("AllBlockData.blockList 내 일자 블록 인덱스")]
    public int targetIBlockIndex = 0;
    public Button btn_Reroll;

    [Header("5. Gravity Item")]
    [SerializeField] private float gravitySettleDelay = 0.1f;
    public Button btn_Gravity;

    [Header("6. Ghost Piece (1회용 발동)")]
    public Material ghostMaterial;
    public Button btn_Ghost;
    public bool isGhostActive = false;

    private void Start()
    {
        if (btn_Bomb != null) btn_Bomb.onClick.AddListener(() => TryUseItem(0, UseBombItem));
        if (btn_TimeStop != null) btn_TimeStop.onClick.AddListener(() => TryUseItem(1, UseTimeStopItem));
        if (btn_LevelDown != null) btn_LevelDown.onClick.AddListener(() => TryUseItem(2, UseLevelDownItem));
        if (btn_Reroll != null) btn_Reroll.onClick.AddListener(() => TryUseItem(3, UseRerollItem));
        if (btn_Gravity != null) btn_Gravity.onClick.AddListener(() => TryUseItem(4, UseGravityItem));
        if (btn_Ghost != null) btn_Ghost.onClick.AddListener(() => TryUseItem(GhostItemIndex, ActivateGhostPiece));

        UpdateAllItemUI();
    }

    public void UpdateAllItemUI()
    {
        for (int i = 0; i < itemUIs.Length && i < itemCounts.Length; i++)
            UpdateItemUI(i);
    }

    public void UpdateItemUI(int index)
    {
        if (index < 0 || index >= itemUIs.Length || index >= itemCounts.Length) return;
        ItemSlotUI ui = itemUIs[index];
        if (ui == null) return;

        int count = itemCounts[index];
        if (count <= 0)
        {
            if (ui.plusObj != null) ui.plusObj.SetActive(true);
            if (ui.adsObj != null) ui.adsObj.SetActive(true);
            if (ui.plusNumObj != null) ui.plusNumObj.SetActive(false);
        }
        else
        {
            if (ui.plusObj != null) ui.plusObj.SetActive(false);
            if (ui.adsObj != null) ui.adsObj.SetActive(false);
            if (ui.plusNumObj != null) ui.plusNumObj.SetActive(true);
            if (ui.txtNum != null) ui.txtNum.text = count.ToString();
        }
    }

    public void TryUseItem(int index, Action onUseItem)
    {
        if (index < 0 || index >= itemCounts.Length) return;

        if (itemCounts[index] <= 0)
        {
            Debug.Log("광고 시청 팝업 출력 (추후 구현)");
            itemCounts[index] = Mathf.Min(itemCounts[index] + 2, MaxItemCount);
            UpdateItemUI(index);
            return;
        }

        if (index == GhostItemIndex && isGhostActive)
        {
            Debug.Log("이미 이번 스테이지에서 고스트를 사용했습니다.");
            return;
        }

        itemCounts[index]--;
        UpdateItemUI(index);
        onUseItem?.Invoke();
    }

    private void ActivateGhostPiece()
    {
        isGhostActive = true;
        if (gameManager != null)
            gameManager.RefreshGhostState();
    }

    public void UseBombItem()
    {
        if (gameManager == null) return;

        int minZ = gameManager.GridMinZ;
        int minX = gameManager.GridMinX;
        int maxX = gameManager.GridMaxX;

        for (int z = minZ; z < minZ + bombClearRows; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                gameManager.TryDestroyAndRemoveCubeAt(x, z);
            }
        }

        var clearedLines = new List<int>();
        for (int z = minZ; z < minZ + bombClearRows; z++)
            clearedLines.Add(z);

        gameManager.ShiftRowsDown(clearedLines);
    }

    public void UseTimeStopItem()
    {
        if (timeStopDuration <= 0f) return;
        Debug.Log("아이템 사용됨");
    }

    public void UseLevelDownItem()
    {
        if (levelDecreaseAmount < 0 || minLevelLimit < 0) return;
        Debug.Log("아이템 사용됨");
    }

    public void UseRerollItem()
    {
        Debug.Log("아이템 사용됨");
    }

    public void UseGravityItem()
    {
        if (gravitySettleDelay <= 0f) return;
        Debug.Log("아이템 사용됨");
    }
}
