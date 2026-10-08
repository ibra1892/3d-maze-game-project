using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

[System.Serializable]
public class SoruData
{
    public string soruMetni;
    public string dogruCevap;
    public string ipucu;
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
    public TMP_InputField inputCevap;      // Cevap metin kutusu
    public Button cevaplaButonu;           // Cevapla butonu
    public TextMeshProUGUI kapiSkorText; 
    public Image skorBarFill; 

    public static SoruManager instance;
    
    private GameObject suAnkiKapi; 
    private List<SoruData> tumSorular = new List<SoruData>();
    private SoruData aktifSoru;

    private int acilanKapiSayisi = 0;
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

        // Butona tıklandığında cevap kontrolünü çalıştır
        if (cevaplaButonu != null)
        {
            cevaplaButonu.onClick.AddListener(CevapGonder);
        }
    }

    void Update()
    {
        // Panel açıkken klavyeden Enter'a basılırsa da cevabı gönder
        if (soruPaneli.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            CevapGonder();
        }
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
            if (acilanKapiSayisi < toplamKapiSayisi)
            {
                acilanKapiSayisi++; 
                SkorGuncelleUI();   
            }
            
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
            kapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver);
            return;
        }

        suAnkiKapi = kapi; 
        soruPaneli.SetActive(true); 

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPaperOpen();

        Time.timeScale = 0; 
        Cursor.lockState = CursorLockMode.None; 
        Cursor.visible = true;

        int rastgeleIndeks = Random.Range(0, tumSorular.Count);
        aktifSoru = tumSorular[rastgeleIndeks];

        soruText.text = aktifSoru.soruMetni;

        // Metin kutusunu sıfırla ve klavyeyi doğrudan kutuya odakla
        if (inputCevap != null)
        {
            inputCevap.text = "";
            inputCevap.ActivateInputField();
        }
    }

    public void CevapGonder()
    {
        if (inputCevap == null || string.IsNullOrWhiteSpace(inputCevap.text)) return;

        string girilenCevap = inputCevap.text;

        if (CevapDogruMu(girilenCevap, aktifSoru.dogruCevap))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen(); 

            if (acilanKapiSayisi < toplamKapiSayisi)
            {
                acilanKapiSayisi++;
            }
            SkorGuncelleUI();

            tumSorular.Remove(aktifSoru); 
            soruPaneli.SetActive(false); 
            Time.timeScale = 1;          
            
            Cursor.lockState = CursorLockMode.Locked; 
            Cursor.visible = false;
            
            if (suAnkiKapi != null) suAnkiKapi.SendMessage("KapıyıAc", SendMessageOptions.DontRequireReceiver); 
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SallamaEfekti());
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();

            soruText.text = "<color=red>YANLIŞ CEVAP, TEKRAR DENE!</color>\n\n" + aktifSoru.soruMetni;
            
            if (inputCevap != null)
            {
                inputCevap.text = "";
                inputCevap.ActivateInputField();
            }
        }
    }

    // --- METİN FİLTRELEME & LEVENSHTEIN SİSTEMİ ---

    // 1. Rafineri Filtresi: Matematik, küme, noktalama ve boşluk temizliğini yapar
    private string MetniTemizle(string metin)
    {
        if (string.IsNullOrEmpty(metin)) return "";

        // Türkçe kurallarına göre küçük harfe çevir
        CultureInfo trKultur = new CultureInfo("tr-TR");
        string temiz = metin.Trim().ToLower(trKultur);

        // Küme ve parantez işaretlerini temizle ({, }, [, ], (, ))
        temiz = Regex.Replace(temiz, @"[\{\}\[\]\(\)]", "");

        // Soru işareti, ünlem, tırnak, iki nokta gibi sembolleri sil (virgül ve noktayı koru)
        temiz = Regex.Replace(temiz, @"['""!?;:]", "");

        // Cümle sonundaki gereksiz noktaları sil ("ankara." -> "ankara", ama "1.4" bozulmaz)
        temiz = Regex.Replace(temiz, @"\.(?!\d)", "");

        // Virgül veya nokta çevresindeki boşlukları sil ("2, 3" -> "2,3" veya "1 . 4" -> "1.4")
        temiz = Regex.Replace(temiz, @"\s*([,\.])\s*", "$1");

        // Sayı içi ondalık standardı: "1.4" yazılsa bile virgüle çevirerek "1,4" ile eşitler
        temiz = Regex.Replace(temiz, @"(?<=\d)\.(?=\d)", ",");

        // Kelime aralarındaki çift/üçlü boşlukları tek boşluğa indir
        temiz = Regex.Replace(temiz, @"\s+", " ");

        return temiz.Trim();
    }

    // 2. Cevap Doğrulama: Çoklu alternatifleri (|) ve harf hatası toleransını kontrol eder
    private bool CevapDogruMu(string oyuncuMetni, string dogruMetin)
    {
        if (string.IsNullOrEmpty(oyuncuMetni) || string.IsNullOrEmpty(dogruMetin)) return false;

        string temizOyuncu = MetniTemizle(oyuncuMetni);

        // Doğru cevap içindeki olası alternatifleri ayır (|)
        string[] alternatifler = dogruMetin.Split('|');

        foreach (string alternatif in alternatifler)
        {
            string temizHedef = MetniTemizle(alternatif);

            // Filtre sonrası birebir eşitse doğrudan kabul et
            if (temizOyuncu == temizHedef) return true;

            int mesafe = LevenshteinMesafe(temizOyuncu, temizHedef);
            int maxUzunluk = Mathf.Max(temizOyuncu.Length, temizHedef.Length);

            // 3 karakter veya daha kısaysa (örn: "Nil", "1,4") tam eşleşme gerekir
            if (maxUzunluk <= 3)
            {
                if (mesafe == 0) return true;
            }
            // 4-5 karakterli kelimelerde en fazla 1 harf hatasına izin ver
            else if (maxUzunluk <= 5)
            {
                if (mesafe <= 1) return true;
            }
            // Daha uzun kelimelerde %75 benzerlik veya en fazla 2 harf hatası kabul et
            else
            {
                float benzerlikOrani = 1.0f - ((float)mesafe / maxUzunluk);
                if (benzerlikOrani >= 0.75f || mesafe <= 2) return true;
            }
        }

        return false;
    }

    // 3. Karakter işlem mesafesini hesaplayan temel algoritma
    private int LevenshteinMesafe(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    // --- UI VE EFEKTLER ---

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