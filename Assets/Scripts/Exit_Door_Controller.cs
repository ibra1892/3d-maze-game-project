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
    public float yukselmeMiktari = 3.5f; 
    public float acilmaHizi = 2.5f;     

    [Header("UI Ayarları")]
    // 🎯 KANKA BURASI DEĞİŞTİ: Artık sadece yazıyı değil, tüm paneli kontrol etmesi için GameObject yaptık!
    public GameObject mesajPaneli; 
    private TextMeshProUGUI mesajText; 

    [Header("Mesafe Ayarı")]
    public float algilamaMesafesi = 5f; 

    void Start()
    {
        baslangicPozisyonu = transform.position;
        hedefPozisyonu = baslangicPozisyonu + Vector3.up * yukselmeMiktari;

        // Oyun başlar başlamaz o parşömen panelini kod yardımıyla tamamen gizliyoruz kanka
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
            transform.position = Vector3.MoveTowards(transform.position, hedefPozisyonu, acilmaHizi * Time.deltaTime);
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
                
                string basariMesaji = "<b><size=140%><color=#145A32>TEBRİKLER!</color></size></b>\n\n" +
                                      "<size=95%><color=#2C1A0C>Labirentten basarıyla cıktınız</color></size>";
                
                StartCoroutine(MesajGoster(basariMesaji));
                
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            else
            {
                string uyariMesaji = "<b><size=140%><color=#800000>KAPI KİLİTLİ</color></size></b>\n\n" +
                                     "<size=95%><color=#2C1A0C>Labirentteki tüm soruları çözmeden buradan çıkamazsın.</color></size>";
                
                StartCoroutine(MesajGoster(uyariMesaji));
            }
        }
    }

    IEnumerator MesajGoster(string mesaj)
    {
        if (mesajPaneli != null && mesajText != null)
        {
            mesajText.text = mesaj;
            mesajPaneli.SetActive(true); // 🎯 E'ye basınca tüm parşömeni ve yazıyı aynı anda havaya kaldırır!
            yield return new WaitForSeconds(3.5f);
            mesajPaneli.SetActive(false); // Süre bitince paneli komple tekrar pusuya yatırır.
        }
    }
}