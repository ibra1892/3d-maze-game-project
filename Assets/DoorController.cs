using UnityEngine;

public class DoorController : MonoBehaviour
{
    private bool isPlayerNearby = false;
    private bool isOpen = false;

    public float openAngle = 90f; 
    public float smoothSpeed = 3f; 

    private Quaternion targetRotation;
    private Quaternion startRotation;

    void Start()
    {
        startRotation = transform.rotation;
        targetRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + openAngle, transform.eulerAngles.z);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            // KANKA DEĞİŞEN KISIM BURASI:
            // Eğer kapı kapalıysa direkt açma, git SoruManager'ı çağır!
            if (!isOpen)
            {
                SoruManager.instance.SoruSor(gameObject);
            }
            else
            {
                // Kapı zaten açıksa E'ye basınca normal kapansın brom
                isOpen = false;
            }
        }

        Quaternion currentTarget = isOpen ? targetRotation : startRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, currentTarget, Time.deltaTime * smoothSpeed);
    }

    // YENİ EKLENEN FONKSİYON: 
    // SoruManager'da doğru cevap butonuna basılınca bu fonksiyon dışarıdan tetiklenecek kanka
    public void KapıyıAc()
    {
        isOpen = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
        }
    }
}