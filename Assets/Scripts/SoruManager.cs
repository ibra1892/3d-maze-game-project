using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections;
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
    public TextMeshProUGUI kapiSkorText; 
    public Image skorBarFill; 
    public Color normalButonRengi = Color.white; 

    public static SoruManager instance;
    
    private GameObject suAnkiKapi; 
    private List<SoruData> tumSorular = new List<SoruData>();
    private SoruData aktifSoru;

    private int acilanKapiSayisi = 0;
    
    // 🎯 İŞTE DÜZELTİLEN YER: private yerine public yaptık ki MazeGenerator buraya ulaşabilsin
    public int toplamKapiSayisi = 1; 
    
    private Coroutine barAnimasyonCoroutine; 

    void Awake()
    {
        instance = this;
        JSONDosyasiniOku(); 
    }

    void Start()
    {
        soruPaneli.SetActive(false);
        if (skorBarFill != null) skorBarFill.fillAmount = 0f;
        SkorGuncelleUI(); 
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
                toplamKapiSayisi = tumSorular.Count > 0 ? tumSorular.Count : 1;
            }
        }
    }

    public void SoruSor(GameObject kapi)
    {
        if (soruPaneli.activeSelf) return; 

        if (tumSorular.Count == 0)
        {
            // SAYAÇ FRENİ EKLENDİ
            if (acilanKapiSayisi < toplamKapiSayisi)
            {
                acilanKapiSayisi++; 
                SkorGuncelleUI();   
            }
            
            // UNUTULAN SES EFEKTİ EKLENDİ
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
            
            kapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver);
            return;
        }

        foreach (Button btn in cevapButonlari)
        {
            if (btn != null && btn.image != null) btn.image.color = normalButonRengi; 
        }

        suAnkiKapi = kapi; 
        soruPaneli.SetActive(true); 

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPaperOpen();

        Time.timeScale = 0; 
        // Fareyi görünür ve serbest yapıyoruz
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
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen(); 

            // SAYAÇ FRENİ EKLENDİ
            if (acilanKapiSayisi < toplamKapiSayisi)
            {
                acilanKapiSayisi++;
            }
            SkorGuncelleUI();

            tumSorular.Remove(aktifSoru); 
            soruPaneli.SetActive(false); 
            Time.timeScale = 1;          
            
            // Fareyi tekrar kilitliyor ve gizliyoruz
            Cursor.lockState = CursorLockMode.Locked; 
            Cursor.visible = false;
            
            if (suAnkiKapi != null) suAnkiKapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver); 
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SallamaEfekti());
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();

            int tiklananButonIndeksi = -1;
            if (secilenSık == "A" || secilenSık == "A)" || secilenSık == aktifSoru.secenekA) tiklananButonIndeksi = 0;
            else if (secilenSık == "B" || secilenSık == "B)" || secilenSık == aktifSoru.secenekB) tiklananButonIndeksi = 1;
            else if (secilenSık == "C" || secilenSık == "C)" || secilenSık == aktifSoru.secenekC) tiklananButonIndeksi = 2;
            else if (secilenSık == "D" || secilenSık == "D)" || secilenSık == aktifSoru.secenekD) tiklananButonIndeksi = 3;

            if (tiklananButonIndeksi != -1 && tiklananButonIndeksi < cevapButonlari.Length)
                cevapButonlari[tiklananButonIndeksi].image.color = new Color(1f, 0.25f, 0.25f, 1f); 

            soruText.text = "<color=red> YANLIŞ CEVAP !</color>\n\n" + aktifSoru.soruMetni;
        }
    }

    void SkorGuncelleUI()
    {
        if (kapiSkorText != null)
            kapiSkorText.text = "Açılan Kapı: " + acilanKapiSayisi + " / " + toplamKapiSayisi;

        if (skorBarFill != null && toplamKapiSayisi > 0)
        {
            float hedefDoluluk = (float)acilanKapiSayisi / toplamKapiSayisi;
            if (barAnimasyonCoroutine != null) StopCoroutine(barAnimasyonCoroutine);
            barAnimasyonCoroutine = StartCoroutine(YavascaBariDoldur(hedefDoluluk));
        }
    }

    private IEnumerator YavascaBariDoldur(float hedef)
    {
        float baslangicFill = skorBarFill.fillAmount;
        float gecenSure = 0f;
        float animasyonSuresi = 0.4f; 

        while (gecenSure < animasyonSuresi)
        {
            gecenSure += Time.deltaTime;
            skorBarFill.fillAmount = Mathf.Lerp(baslangicFill, hedef, gecenSure / animasyonSuresi);
            yield return null;
        }
        skorBarFill.fillAmount = float.IsNaN(hedef) ? 0f : hedef;
    }

    private IEnumerator SallamaEfekti()
    {
        Transform panelTransform = soruPaneli.transform;
        Vector3 orijinalPozisyon = panelTransform.localPosition;
        float sure = 0.20f;    
        float hiz = 65f;    
        float miktar = 15f;  
        float gecenSure = 0f;
        while (gecenSure < sure)
        {
            gecenSure += Time.unscaledDeltaTime;
            float yeniX = orijinalPozisyon.x + Mathf.Sin(Time.unscaledTime * hiz) * miktar;
            panelTransform.localPosition = new Vector3(yeniX, orijinalPozisyon.y, orijinalPozisyon.z);
            yield return null;
        }
        panelTransform.localPosition = orijinalPozisyon;
    }

    public bool TumSorularCozulduMu()
    {
        return acilanKapiSayisi >= toplamKapiSayisi;
    }
}