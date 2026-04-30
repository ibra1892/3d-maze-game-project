using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject  gunObject;
    public Animator    animator;
    public ItemPickup  itemPickup;
    public Transform   firePoint;      // Silahın namlusu (boş obje ekle)

    [Header("Ayarlar")]
    public int   weaponLayerIndex = 1;
    public float fireRange        = 50f;
    public int   maxAmmoPerClip   = 30;

    private bool hasWeapon   = false;
    private bool gunEquipped = false;
    private int  currentAmmo = 0;

    void Start()
    {
        if (gunObject != null)
            gunObject.SetActive(false);

        animator.SetLayerWeight(weaponLayerIndex, 0f);
    }

    void Update()
    {
        // Silah alındı mı kontrol et
        if (itemPickup != null && itemPickup.hasWeapon && !hasWeapon)
            hasWeapon = true;

        // 1 → Silah kuşan
        if (Input.GetKeyDown(KeyCode.Alpha1) && hasWeapon && !gunEquipped)
            EquipGun();

        // 2 → Silahı kaldır
        if (Input.GetKeyDown(KeyCode.Alpha2) && gunEquipped)
            UnequipGun();

        // Ateş et
        if (Input.GetMouseButtonDown(0) && gunEquipped)
            TryShoot();
    }

    void EquipGun()
    {
        gunEquipped = true;
        gunObject.SetActive(true);
        animator.SetLayerWeight(weaponLayerIndex, 1f);
        animator.SetBool("HasWeapon", true);

        // Mevcut mermiyi itemPickup'tan al
        if (itemPickup != null)
            currentAmmo = itemPickup.totalAmmo;

        Debug.Log($"Silah kuşanıldı! Mermi: {currentAmmo}");
    }

    void UnequipGun()
    {
        gunEquipped = false;
        gunObject.SetActive(false);
        animator.SetLayerWeight(weaponLayerIndex, 0f);
        animator.SetBool("HasWeapon", false);
        animator.SetBool("IsShooting", false);
        Debug.Log("Silah kaldırıldı.");
    }

    void TryShoot()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("Mermi yok!");
            animator.SetBool("IsShooting", false);
            return;
        }

        currentAmmo--;
        if (itemPickup != null) itemPickup.totalAmmo = currentAmmo;

        animator.SetBool("IsShooting", true);
        Invoke(nameof(StopShooting), 0.2f);

        // Raycast ile ateş
        Ray ray = new Ray(firePoint != null ? firePoint.position : transform.position,
                          firePoint != null ? firePoint.forward  : transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, fireRange))
        {
            Debug.Log($"İsabet: {hit.collider.name} | Kalan mermi: {currentAmmo}");

            // Düşmana hasar ver (düşman scripti varsa)
            // hit.collider.GetComponent<Enemy>()?.TakeDamage(25f);
        }
    }

    void StopShooting() => animator.SetBool("IsShooting", false);

    // ItemPickup tarafından çağrılır
    public void OnWeaponPickedUp()
    {
        hasWeapon = true;
        Debug.Log("Silah envantere eklendi! 1 ile kuşan.");
    }

    public int  GetAmmo()        => currentAmmo;
    public bool IsGunEquipped()  => gunEquipped;
}