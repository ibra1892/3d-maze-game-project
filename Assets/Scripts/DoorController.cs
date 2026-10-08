using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isOpen = false;
    
    [Header("Soru Ayarı")]
    public bool soruVarMi = false; // Kapıda soru var mı yoksa boş kapı mı?

    private bool isPlayerNearby = false;

    [Header("Kapı Yukarı Kayma Ayarları")]
    public float yukselmeMiktari = 3.5f; // Kapı yukarı doğru kaç metre çıksın?
    private Vector3 startPosition;
    private Vector3 targetPosition;

    void Start()
    {
        // Kapının durduğu ilk yerel pozisyonu kaydediyoruz
        startPosition = transform.localPosition;
        
        // Hedef pozisyon: Y ekseninde yükselmiş hali
        targetPosition = startPosition + new Vector3(0, yukselmeMiktari, 0);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!isOpen)
            {
                if (soruVarMi)
                {
                    // Sorulu kapı: Parşömen soru ekranını aç
                    SoruManager.instance.SoruSor(gameObject);
                }
                else
                {
                    // Boş kapı: Doğrudan aç ve ses efektini çal
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
                    KapıyıAc();
                }
            }
        }

        // Kapının yukarı doğru yumuşakça kayma animasyonu (Lerp)
        Vector3 currentTarget = isOpen ? targetPosition : startPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, currentTarget, Time.deltaTime * 5f);
    }

    // Soru doğru bilinince veya boş kapı olunca tetiklenir
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