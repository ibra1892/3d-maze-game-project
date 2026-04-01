using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("Ayarlar")]
    public float     pickupRadius = 2f;
    public KeyCode   pickupKey    = KeyCode.E;
    public LayerMask itemLayer;

    [HideInInspector] public int  totalAmmo = 0;
    [HideInInspector] public bool hasWeapon = false;

    private PlayerMovement  player;
    private Animator         anim;
    private InventoryUI inventory;
    
    void Start()
    {
        player = GetComponent<PlayerMovement >();
        if(player == null) player = Object.FindFirstObjectByType<PlayerMovement>();
        anim   = GetComponentInChildren<Animator>();
        inventory = Object.FindFirstObjectByType<InventoryUI>();
    }

    void Update()
    {
        if (Input.GetKeyDown(pickupKey))
            TryPickup();
    }

    void TryPickup()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius);
        Debug.Log($"Bulunan obje sayısı: {hits.Length}");

        // Item scripti olan en yakın objeyi bul
        Item nearestItem = null;
        float minDist = float.MaxValue;

        foreach (Collider c in hits)
        {
            Item item = c.GetComponent<Item>();
            if (item == null) continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearestItem = item; }
        }

        if (nearestItem == null)
        {
            Debug.Log("Yakında Item yok!");
            return;
        }

        anim.SetTrigger("Lift");

        player.canMove = false;
        Invoke(nameof(EnableMovement), 1.5f);
        
        if(nearestItem.itemType == ItemType.Ammo)
        {
            inventory.AddItem(nearestItem.itemType, nearestItem.ammoCount);
        } else
        {
            inventory.AddItem(nearestItem.itemType, 1);
        }

        Destroy(nearestItem.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    void EnableMovement()
    {
        player.canMove = true;
    }
}
