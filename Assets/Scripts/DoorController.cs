using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isOpen = false;
    private bool isPlayerNearby = false;

    [Header("Kapı Yukarı Kayma Ayarları")]
    public float yukselmeMiktari = 3.5f; // Kapı yukarı doğru kaç metre çıksın?
    private Vector3 startPosition;
    private Vector3 targetPosition;

    void Start()
    {
        // KANKA MANTIĞI DEĞİŞTİRDİK: 
        // Açıyı değil, kapının durduğu POZİSYONU (Yerel yerini) kaydediyoruz.
        startPosition = transform.localPosition;
        
        // Hedef pozisyon: İlk durduğu yerin 'Y' ekseninde (yukarısında) belirlenen miktar kadar üstü
        targetPosition = startPosition + new Vector3(0, yukselmeMiktari, 0);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!isOpen)
            {
                SoruManager.instance.SoruSor(gameObject);
            }
        }

        // Kapının yukarı doğru yumuşakça kayma animasyonu (Lerp ile)
        Vector3 currentTarget = isOpen ? targetPosition : startPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, currentTarget, Time.deltaTime * 5f);
    }

    // Soru doğru bilinince SoruManager burayı tetikler
    public void KapıyıAc()
    {
        isOpen = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = false;
    }
}