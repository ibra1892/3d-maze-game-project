using UnityEngine;

public enum ItemType { Bandage, EnergyDrink, Ammo, Weapon }

public class Item : MonoBehaviour
{
    public ItemType itemType  = ItemType.Bandage;
    public float    value     = 30f;   // HP veya Stamina miktarı
    public int      ammoCount = 10;    // Sadece Ammo tipi için

    [Header("Efekt")]
    public float rotateSpeed = 90f;
    public float bobSpeed    = 1f;
    public float bobHeight   = 0.2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
