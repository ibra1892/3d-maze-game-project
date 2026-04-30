using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("Ayarlar")]
    public float   pickupRadius = 2f;
    public KeyCode pickupKey    = KeyCode.E;

    [HideInInspector] public int  totalAmmo = 0;
    [HideInInspector] public bool hasWeapon = false;

    private PlayerMovement player;
    private Animator       anim;
    private InventoryUI    inventory;
    private WeaponManager  weaponManager;

    void Start()
    {
        player        = GetComponent<PlayerMovement>();
        if (player == null) player = Object.FindFirstObjectByType<PlayerMovement>();
        anim          = GetComponentInChildren<Animator>();
        inventory     = Object.FindFirstObjectByType<InventoryUI>();
        weaponManager = Object.FindFirstObjectByType<WeaponManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(pickupKey))
            TryPickup();
    }

    void TryPickup()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius);

        Item nearestItem = null;
        float minDist    = float.MaxValue;

        foreach (Collider c in hits)
        {
            Item item = c.GetComponent<Item>();
            if (item == null) continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearestItem = item; }
        }

        if (nearestItem == null) return;

        // Alma animasyonu + hareket kilidi
        anim.SetTrigger("Lift");
        player.canMove = false;
        Invoke(nameof(EnableMovement), 1.5f);

        switch (nearestItem.itemType)
        {
            case ItemType.Bandage:
                inventory.AddItem(ItemType.Bandage, 1);
                Debug.Log("Bandaj alındı!");
                break;

            case ItemType.EnergyDrink:
                inventory.AddItem(ItemType.EnergyDrink, 1);
                Debug.Log("Enerji içeceği alındı!");
                break;

            case ItemType.Ammo:
                totalAmmo += nearestItem.ammoCount;
                inventory.AddItem(ItemType.Ammo, nearestItem.ammoCount);
                Debug.Log($"Mermi alındı! Toplam: {totalAmmo}");
                break;

            case ItemType.Weapon:
                hasWeapon = true;
                weaponManager?.OnWeaponPickedUp();
                Debug.Log("Silah alındı! 1 tuşuyla kuşan.");
                break;
        }

        Destroy(nearestItem.gameObject);
    }

    void EnableMovement() => player.canMove = true;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}