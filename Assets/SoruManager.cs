using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SoruData
{
    public string soruMetni;
    public string secenekA;
    public string secenekB;
    public string secenekC;
    public string secenekD;
    public string dogruCevap;
}

public class SoruListesiWrapper
{
    public SoruData[] sorular;
}

public class SoruManager : MonoBehaviour
{
    [Header("UI Elemanları")]
    public GameObject soruPaneli;     
    public TextMeshProUGUI soruText;  
    public Button[] cevapButonlari;   

    public static SoruManager instance;
    
    private GameObject suAnkiKapi; 
    private List<SoruData> tumSorular = new List<SoruData>();
    private SoruData aktifSoru;

    void Awake()
    {
        instance = this;
        JSONDosyasiniOku(); 
    }

    void Start()
    {
        soruPaneli.SetActive(false); 
    }

    void JSONDosyasiniOku()
    {
        string dosyaYolu = Path.Combine(Application.streamingAssetsPath, "sorular.json");

        if (File.Exists(dosyaYolu))
        {
            string jsonIcerigi = File.ReadAllText(dosyaYolu);
            string sarmalJson = "{\"sorular\":" + jsonIcerigi + "}";
            SoruListesiWrapper yuklenenSorular = JsonUtility.FromJson<SoruListesiWrapper>(sarmalJson);

            if (yuklenenSorular != null && yuklenenSorular.sorular != null)
            {
                tumSorular.AddRange(yuklenenSorular.sorular);
            }
        }
    }

    public void SoruSor(GameObject kapi)
    {
        if (tumSorular.Count == 0)
        {
            Debug.LogWarning("Tüm sorular çözüldü! Kapı direkt açılıyor.");
            kapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver);
            return;
        }

        suAnkiKapi = kapi; 
        soruPaneli.SetActive(true); 
        Time.timeScale = 0; 
        
        Cursor.lockState = CursorLockMode.None; 
        Cursor.visible = true;                  

        int rastgeleIndeks = Random.Range(0, tumSorular.Count);
        aktifSoru = tumSorular[rastgeleIndeks];

        soruText.text = aktifSoru.soruMetni;
        cevapButonlari[0].GetComponentInChildren<TextMeshProUGUI>().text = "A) " + aktifSoru.secenekA;
        cevapButonlari[1].GetComponentInChildren<TextMeshProUGUI>().text = "B) " + aktifSoru.secenekB;
        cevapButonlari[2].GetComponentInChildren<TextMeshProUGUI>().text = "C) " + aktifSoru.secenekC;
        cevapButonlari[3].GetComponentInChildren<TextMeshProUGUI>().text = "D) " + aktifSoru.secenekD;
    }

    public void CevapVer(string secilenSık)
    {
        if (secilenSık == aktifSoru.dogruCevap)
        {
            Debug.Log("DOĞRU CEVAP!");
            
            tumSorular.Remove(aktifSoru); // Doğru bilinen soruyu havuzdan sil

            soruPaneli.SetActive(false); 
            Time.timeScale = 1;          
            Cursor.lockState = CursorLockMode.Locked; 
            
            if (suAnkiKapi != null)
            {
                suAnkiKapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver); 
            }
        }
        else
        {
            Debug.Log("YANLIŞ CEVAP!");
            
            // KANKA DEĞİŞEN TEK YER BURASI:
            // Yanlış şıkka basınca sorunun üstüne kıpkırmızı uyarıyı çakıyoruz brom!
            soruText.text = "<color=red>⚠️ YANLIŞ CEVAP! Tekrar dene brom! ⚠️</color>\n\n" + aktifSoru.soruMetni;
        }
    }
}