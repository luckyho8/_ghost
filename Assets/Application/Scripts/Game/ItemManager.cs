using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
/// 아이템 옵션 및 버튼 관리. 수량 제한(최대 9), UI 상태.
/// 아이템 5종: Bomb, TimeStop, LevelDown, Reroll, GhostToggle
/// </summary>
public class ItemManager : MonoBehaviour
{
    private const int MaxItemCount = 9;

    [Header("참조")]
    public GameManager gameManager;

    [Header("아이템 수량 및 슬롯 UI (0~3: Bomb, TimeStop, LevelDown, Reroll)")]
    public int[] itemCounts = new int[4];
    [FormerlySerializedAs("itemSlots")]
    public ItemSlotUI[] itemSlots = new ItemSlotUI[4];

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

    [Header("5. Ghost Piece Toggle (소모 없음 — 영구 토글)")]
    public Material ghostMaterial;
    public Button btn_Ghost;

    private void Start()
    {
        if (btn_Bomb != null) btn_Bomb.onClick.AddListener(() => TryUseItem(0, UseBombItem));
        if (btn_TimeStop != null) btn_TimeStop.onClick.AddListener(() => TryUseItem(1, UseTimeStopItem));
        if (btn_LevelDown != null) btn_LevelDown.onClick.AddListener(() => TryUseItem(2, UseLevelDownItem));
        if (btn_Reroll != null) btn_Reroll.onClick.AddListener(() => TryUseItem(3, UseRerollItem));
        if (btn_Ghost != null) btn_Ghost.onClick.AddListener(OnGhostButtonPressed);

        UpdateAllItemUI();
    }

    public void UpdateAllItemUI()
    {
        for (int i = 0; i < itemSlots.Length && i < itemCounts.Length; i++)
            UpdateItemUI(i);
    }

    public void UpdateItemUI(int index)
    {
        if (index < 0 || index >= itemSlots.Length || index >= itemCounts.Length) return;
        ItemSlotUI ui = itemSlots[index];
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

        itemCounts[index]--;
        UpdateItemUI(index);
        onUseItem?.Invoke();
    }

    private void OnGhostButtonPressed()
    {
        if (gameManager != null)
            gameManager.ToggleGhostPiece();
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
        if (gameManager == null || timeStopDuration <= 0f) return;
        gameManager.StartCoroutine(gameManager.DoTimeStop(timeStopDuration));
    }

    public void UseLevelDownItem()
    {
        if (gameManager == null || levelDecreaseAmount < 0 || minLevelLimit < 0) return;
        gameManager.DecreaseLevel(levelDecreaseAmount, minLevelLimit);
    }

    public void UseRerollItem()
    {
        if (gameManager == null) return;
        gameManager.ReplaceCurrentBlockWith(targetIBlockIndex);
    }

}
