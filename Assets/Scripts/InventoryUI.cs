using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [Header("References")]
    public GameObject      inventoryPanel;
    public GameObject      slotPrefab;
    public Transform       slotParent;
    public PlayerMovement  player;

    [Header("Icons")]
    public List<ItemIconEntry> itemIcons = new List<ItemIconEntry>();

    [Header("Bandage")]
    public float healAmount = 30f;

    [Header("Energy Drink")]
    public float energyDuration  = 10f;   // saniye
    public float energySpeedBonus = 2f;   // ekstra hız
    public float energyStaminaBonus = 999f; // stamina bitmiyor

    [Header("Weapon")]
    public bool hasWeapon = false;

    private bool isOpen = false;
    private List<InventoryItem> items = new List<InventoryItem>();
    private List<GameObject>    slots = new List<GameObject>();

    void Start()
    {
        inventoryPanel.SetActive(false);

        if (player == null)
            player = player = Object.FindFirstObjectByType<PlayerMovement>();
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
        Cursor.lockState = isOpen ? CursorLockMode.None  : CursorLockMode.Locked;
        Cursor.visible   = isOpen;
        if (isOpen) RefreshUI();
    }

    public void AddItem(ItemType type, int amount = 1)
    {
        if(type == ItemType.Ammo)
        {
            if(player != null)
            {
                player.currentAmmo += amount;
                Debug.Log("Mermi toplandı! Yeni mermi: " + player.currentAmmo);
            }
            return;
        }

        if(type == ItemType.Weapon)
        {
            hasWeapon = true;
            if(player != null) player.EquipWeapon();
        }

        InventoryItem existing = items.Find(i => i.itemType == type);
        if (existing != null)
            existing.count += amount;
        else
            items.Add(new InventoryItem { itemType = type, count = amount, displayName = GetDisplayName(type) });

        if (isOpen) RefreshUI();
    }

    void RefreshUI()
    {
        foreach (GameObject s in slots) Destroy(s);
        slots.Clear();

        foreach (InventoryItem item in items)
        {
            InventoryItem captured = item; // closure için

            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.SetActive(true);

            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(90, 90);

            // İkon
            Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                Sprite sp = GetSprite(item.itemType);
                if (sp != null) { icon.sprite = sp; icon.color = Color.white; icon.preserveAspect = true; }
            }

            // Yazı
            Text label = slot.GetComponentInChildren<Text>();
            if (label != null)
                label.text = $"{item.displayName}\nx{item.count}";

            // Tıklama
            Button btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(captured, slot));

            slots.Add(slot);
        }
    }

    void OnSlotClicked(InventoryItem item, GameObject slot)
    {
        if (item.count <= 0) return;

        // Tıklama animasyonu
        StartCoroutine(SlotClickAnimation(slot));

        switch (item.itemType)
        {
            case ItemType.Bandage:
                player.Heal(healAmount);
                if(player.animator != null) player.animator.SetBool("IsHealing", true);
                Debug.Log($"Bandaj kullanıldı: +{healAmount} HP");
                Invoke(nameof(StopHealing), 2f); // 2 saniye sonra animasyonu bitir
                break;

            case ItemType.EnergyDrink:
                StartCoroutine(EnergyEffect());
                Debug.Log($"Enerji içeceği kullanıldı! {energyDuration}sn sürecek.");
                break;
        }

        // Adedi azalt
        item.count--;
        if (item.count <= 0)
            items.Remove(item);

        RefreshUI();
    }

    // ─── Slot tıklama animasyonu (küçül → büyü) ───
    IEnumerator SlotClickAnimation(GameObject slot)
    {
        if (slot == null) yield break;

        Vector3 original = slot.transform.localScale;
        Vector3 small    = original * 0.85f;
        float   duration = 0.08f;
        float   t        = 0f;

        // Küçül
        while (t < duration)
        {
            if (slot == null) yield break;
            slot.transform.localScale = Vector3.Lerp(original, small, t / duration);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;

        // Büyü
        while (t < duration)
        {
            if (slot == null) yield break;
            slot.transform.localScale = Vector3.Lerp(small, original, t / duration);
            t += Time.deltaTime;
            yield return null;
        }

        if (slot != null)
            slot.transform.localScale = original;
    }

    // ─── Enerji efekti ───
    IEnumerator EnergyEffect()
    {
        // Efekti uygula
        player.walkSpeed  += energySpeedBonus;
        player.runSpeed   += energySpeedBonus;
        player.staminaDrain = 0f;   // Stamina bitmiyor

        yield return new WaitForSeconds(energyDuration);

        // Efekti geri al
        player.walkSpeed  -= energySpeedBonus;
        player.runSpeed   -= energySpeedBonus;
        player.staminaDrain = 25f;  // Normal değere dön
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
            case ItemType.Ammo:        return "Mermi";
            case ItemType.Weapon:      return "Silah";
            default:                   return "Eşya";
        }
    }

    void StopHealing()
    {
        if(player.animator != null) player.animator.SetBool("IsHealing", false);
    }

    public List<InventoryItem> GetItems() => items;

}
