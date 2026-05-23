using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class InventoryItem
{
    public ItemType itemType;
    public int      count;
    public string   displayName;
}

[System.Serializable]
public class ItemIconEntry
{
    public ItemType itemType;
    public Sprite   icon;
}

public class InventoryUI : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject     inventoryPanel;
    public GameObject     slotPrefab;
    public Transform      slotParent;
    public PlayerMovement player;

    [Header("İkonlar")]
    public List<ItemIconEntry> itemIcons = new List<ItemIconEntry>();

    [Header("Bandaj")]
    public float healAmount = 30f;

    [Header("Enerji İçeceği")]
    public float energyDuration  = 10f;
    public float energySpeedBonus = 2f;

    private bool isOpen = false;
    private List<InventoryItem> items = new List<InventoryItem>();
    private List<GameObject>    slots = new List<GameObject>();

    void Start()
    {
        inventoryPanel.SetActive(false);
        if (player == null)
            player = Object.FindFirstObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleInventory();
    }

    void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel.SetActive(isOpen);
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = isOpen;
        if (isOpen) RefreshUI();
    }

    public void AddItem(ItemType type, int amount = 1)
    {
        InventoryItem existing = items.Find(i => i.itemType == type);
        if (existing != null)
            existing.count += amount;
        else
            items.Add(new InventoryItem
            {
                itemType    = type,
                count       = amount,
                displayName = GetDisplayName(type)
            });

        if (isOpen) RefreshUI();
    }

    void RefreshUI()
    {
        foreach (GameObject s in slots) Destroy(s);
        slots.Clear();

        foreach (InventoryItem item in items)
        {
            InventoryItem captured = item;

            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.SetActive(true);

            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.sizeDelta  = new Vector2(90, 90);

            Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                Sprite sp = GetSprite(item.itemType);
                if (sp != null) { icon.sprite = sp; icon.color = Color.white; icon.preserveAspect = true; }
            }

            Text label = slot.GetComponentInChildren<Text>();
            if (label != null)
                label.text = $"{item.displayName}\nx{item.count}";

            Button btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(captured, slot));

            slots.Add(slot);
        }
    }

    void OnSlotClicked(InventoryItem item, GameObject slot)
    {
        if (item.count <= 0) return;

        StartCoroutine(SlotClickAnimation(slot));

        switch (item.itemType)
        {
            case ItemType.Bandage:
                player.Heal(healAmount);
                if (player.animator != null) player.animator.SetBool("IsHealing", true);
                AudioManager.Instance.PlayBandage();
                Invoke(nameof(StopHealing), 2f);
                Debug.Log($"Bandaj kullanıldı: +{healAmount} HP");
                break;

            case ItemType.EnergyDrink:
                player.BoostSpeed(energyDuration);
                AudioManager.Instance.PlayEnergyDrink();
                Debug.Log($"Enerji içeceği kullanıldı! {energyDuration}sn sürecek.");
                break;
        }

        item.count--;
        if (item.count <= 0) items.Remove(item);
        RefreshUI();
    }

    IEnumerator SlotClickAnimation(GameObject slot)
    {
        if (slot == null) yield break;
        Vector3 original = slot.transform.localScale;
        Vector3 small    = original * 0.85f;
        float   dur      = 0.08f;
        float   t        = 0f;

        while (t < dur) { if (slot == null) yield break; slot.transform.localScale = Vector3.Lerp(original, small, t / dur); t += Time.deltaTime; yield return null; }
        t = 0f;
        while (t < dur) { if (slot == null) yield break; slot.transform.localScale = Vector3.Lerp(small, original, t / dur); t += Time.deltaTime; yield return null; }
        if (slot != null) slot.transform.localScale = original;
    }

    void StopHealing()
    {
        if (player.animator != null) player.animator.SetBool("IsHealing", false);
    }

    Sprite GetSprite(ItemType type)
    {
        ItemIconEntry entry = itemIcons.Find(e => e.itemType == type);
        return entry?.icon;
    }

    string GetDisplayName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Bandage:     return "Bandaj";
            case ItemType.EnergyDrink: return "Enerji";
            default:                   return "Eşya";
        }
    }

    public List<InventoryItem> GetItems() => items;
}