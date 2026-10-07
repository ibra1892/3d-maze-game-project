using UnityEngine;
using TMPro;
using System.Collections;

public class Exit_Door_Controller : MonoBehaviour
{
    private Transform oyuncu;
    private bool kapiAciliyorMu = false;
    private Vector3 baslangicPozisyonu;
    private Vector3 hedefPozisyonu;

    [Header("Kapı Hareket Ayarları")]
    public float yukselmeMiktari = 8f; 
    public float acilmaHizi = 2.5f;     

    [Header("UI Ayarları")]
    public GameObject mesajPaneli; 
    private TextMeshProUGUI mesajText; 

    [Header("Mesafe Ayarı")]
    public float algilamaMesafesi = 5f; 

    void Start()
    {
        baslangicPozisyonu = transform.position;
        hedefPozisyonu = baslangicPozisyonu + Vector3.up * yukselmeMiktari;

        // 🎯 2. SORUNUN ÇÖZÜMÜ: Panel koptuysa oyuna girince Canvas'ı arayıp otomatik bulur
        if (mesajPaneli == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform panel = canvas.transform.Find("ExitMessagePanel");
                if (panel != null) mesajPaneli = panel.gameObject;
            }
        }

        // Bulduğu paneli gizler ve yazı bileşenini alır
        if (mesajPaneli != null)
        {
            mesajText = mesajPaneli.GetComponentInChildren<TextMeshProUGUI>();
            mesajPaneli.SetActive(false); 
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) oyuncu = playerObj.transform;
    }

    void Update()
    {
        if (oyuncu == null) return;

        if (kapiAciliyorMu)
        {
            // Zaman durduğunda bile kapının animasyonu devam etsin
            transform.position = Vector3.MoveTowards(transform.position, hedefPozisyonu, acilmaHizi * Time.unscaledDeltaTime);
            return;
        }

        float mesafe = Vector3.Distance(transform.position, oyuncu.position);

        if (mesafe <= algilamaMesafesi && Input.GetKeyDown(KeyCode.E))
        {
            CikisKontrolEt();
        }
    }

    void CikisKontrolEt()
    {
        if (mesajText != null)
        {
            if (SoruManager.instance != null && SoruManager.instance.TumSorularCozulduMu())
            {
                kapiAciliyorMu = true;
                
                string basariMesaji = "<b><size=140%><color=#145A32>TEBRIKLER!</color></size></b>\n\n" +
                                      "<size=95%><color=#2C1A0C>Labirentten basariyla ciktiniz.</color></size>";
                
                mesajText.text = basariMesaji;
                mesajPaneli.SetActive(true);
                
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
                
                Time.timeScale = 0; 
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                string uyariMesaji = "<b><size=140%><color=#800000>KAPI KILITLI</color></size></b>\n\n" +
                                     "<size=95%><color=#2C1A0C>Labirentteki tum sorulari cozmeden buradan çikamazsin.</color></size>";
                
                StartCoroutine(UyariGoster(uyariMesaji));
            }
        }
    }

    IEnumerator UyariGoster(string mesaj)
    {
        if (mesajPaneli != null && mesajText != null)
        {
            mesajText.text = mesaj;
            mesajPaneli.SetActive(true); 
            yield return new WaitForSeconds(3.5f);
            mesajPaneli.SetActive(false); 
        }
    }
}