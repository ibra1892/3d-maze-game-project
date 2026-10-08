using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Labirent Boyutlari")]
    public int genislik = 25; 
    public int uzunluk = 25;

    [Header("Prefablar")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject kapiPrefab; 
    public GameObject mesalePrefab; 
    
    [Header("Özel Objeler")]
    public GameObject cikisKapisiPrefab; 

    [Header("Ayarlar")]
    public int kapiSayisi = 20; 
    public float kapiMesafeSiniri = 8f; 
    [Range(0, 100)]
    public int mesaleYogunlugu = 10; 

    private int[,] mazeGrid;
    private List<Vector2Int> yolListesi = new List<Vector2Int>(); 

    void Start()
    {
        if (genislik % 2 == 0) genislik++;
        if (uzunluk % 2 == 0) uzunluk++;

        mazeGrid = new int[genislik, uzunluk];
        
        for (int x = 0; x < genislik; x++)
        {
            for (int z = 0; z < uzunluk; z++)
            {
                mazeGrid[x, z] = 1; 
            }
        }

        KostebekKazisi(1, 1);

        mazeGrid[1, 0] = 0; 
        mazeGrid[genislik - 2, uzunluk - 1] = 0; 

        EkstraYollarAc();

        LabirentiSahneyeCiz();
        
        CikisKapisiniYerlestir(); 
        
        KapilariYerlestir(); 
    }

    void KostebekKazisi(int startX, int startZ)
    {
        mazeGrid[startX, startZ] = 0;
        yolListesi.Add(new Vector2Int(startX, startZ));

        int[] yonX = { 0, 0, 2, -2 };
        int[] yonZ = { 2, -2, 0, 0 };
        
        List<int> yonSirasi = new List<int> { 0, 1, 2, 3 };
        for (int i = 0; i < yonSirasi.Count; i++)
        {
            int temp = yonSirasi[i];
            int randomIndex = Random.Range(i, yonSirasi.Count);
            yonSirasi[i] = yonSirasi[randomIndex];
            yonSirasi[randomIndex] = temp;
        }

        foreach (int i in yonSirasi)
        {
            int yeniX = startX + yonX[i];
            int yeniZ = startZ + yonZ[i];

            if (yeniX > 0 && yeniX < genislik - 1 && yeniZ > 0 && yeniZ < uzunluk - 1 && mazeGrid[yeniX, yeniZ] == 1)
            {
                mazeGrid[startX + yonX[i] / 2, startZ + yonZ[i] / 2] = 0;
                yolListesi.Add(new Vector2Int(startX + yonX[i] / 2, startZ + yonZ[i] / 2));
                KostebekKazisi(yeniX, yeniZ);
            }
        }
    }

    void EkstraYollarAc()
    {
        int kirilacakSayi = (genislik * uzunluk) / 30; 
        int kirilan = 0;
        int maxDeneme = 5000;

        while (kirilan < kirilacakSayi && maxDeneme > 0)
        {
            maxDeneme--;
            int x = Random.Range(1, genislik - 1);
            int z = Random.Range(1, uzunluk - 1);

            if (mazeGrid[x, z] == 1)
            {
                bool ustYol = mazeGrid[x, z + 1] == 0;
                bool altYol = mazeGrid[x, z - 1] == 0;
                bool sagYol = mazeGrid[x + 1, z] == 0;
                bool solYol = mazeGrid[x - 1, z] == 0;

                bool yatayBaglanti = solYol && sagYol && !ustYol && !altYol;
                bool dikeyBaglanti = ustYol && altYol && !solYol && !sagYol;

                if (yatayBaglanti || dikeyBaglanti)
                {
                    if (!BoslukYaratirMi(x, z))
                    {
                        mazeGrid[x, z] = 0; 
                        yolListesi.Add(new Vector2Int(x, z));
                        kirilan++;
                    }
                }
            }
        }
    }

    bool BoslukYaratirMi(int x, int z)
    {
        if (mazeGrid[x - 1, z] == 0 && mazeGrid[x, z - 1] == 0 && mazeGrid[x - 1, z - 1] == 0) return true; 
        if (mazeGrid[x + 1, z] == 0 && mazeGrid[x, z - 1] == 0 && mazeGrid[x + 1, z - 1] == 0) return true; 
        if (mazeGrid[x - 1, z] == 0 && mazeGrid[x, z + 1] == 0 && mazeGrid[x - 1, z + 1] == 0) return true; 
        if (mazeGrid[x + 1, z] == 0 && mazeGrid[x, z + 1] == 0 && mazeGrid[x + 1, z + 1] == 0) return true; 
        
        return false;
    }

    void LabirentiSahneyeCiz()
    {
        for (int x = 0; x < genislik; x++)
        {
            for (int z = 0; z < uzunluk; z++)
            {
                if (mazeGrid[x, z] == 1)
                {
                    Vector3 duvarPozisyonu = new Vector3(x * 2f, 2f, z * 2f);
                    Instantiate(wallPrefab, duvarPozisyonu, Quaternion.identity, transform);
                }
                else
                {
                    Vector3 zeminPozisyonu = new Vector3(x * 2f, 0, z * 2f);
                    Instantiate(floorPrefab, zeminPozisyonu, Quaternion.identity, transform);

                    // ---> AKILLI DUVAR MEŞALESİ SİSTEMİ <---
                    if (mesalePrefab != null)
                    {
                        if ((x > 1 || z > 1) && (x < genislik - 2 || z < uzunluk - 2)) 
                        {
                            if (Random.Range(0, 100) < mesaleYogunlugu)
                            {
                                bool ustDuvar = (z + 1 < uzunluk) && mazeGrid[x, z + 1] == 1;
                                bool altDuvar = (z - 1 >= 0) && mazeGrid[x, z - 1] == 1;
                                bool sagDuvar = (x + 1 < genislik) && mazeGrid[x + 1, z] == 1;
                                bool solDuvar = (x - 1 >= 0) && mazeGrid[x - 1, z] == 1;

                                Vector3 mesalePos = Vector3.zero;
                                Quaternion mesaleYon = Quaternion.identity;
                                bool duvaraAsildi = false;

                                float yukseklik = 1.8f;
                                float offset = 0.9f;

                                if (ustDuvar)
                                {
                                    mesalePos = new Vector3(x * 2f, yukseklik, z * 2f + offset);
                                    mesaleYon = Quaternion.Euler(0, 0, 0); 
                                    duvaraAsildi = true;
                                }
                                else if (altDuvar)
                                {
                                    mesalePos = new Vector3(x * 2f, yukseklik, z * 2f - offset);
                                    mesaleYon = Quaternion.Euler(0, 180, 0);
                                    duvaraAsildi = true;
                                }
                                else if (sagDuvar)
                                {
                                    mesalePos = new Vector3(x * 2f + offset, yukseklik, z * 2f);
                                    mesaleYon = Quaternion.Euler(0, 90, 0);
                                    duvaraAsildi = true;
                                }
                                else if (solDuvar)
                                {
                                    mesalePos = new Vector3(x * 2f - offset, yukseklik, z * 2f);
                                    mesaleYon = Quaternion.Euler(0, -90, 0);
                                    duvaraAsildi = true;
                                }

                                if (duvaraAsildi)
                                {
                                    Instantiate(mesalePrefab, mesalePos, mesaleYon, transform);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void CikisKapisiniYerlestir()
    {
        if (cikisKapisiPrefab != null)
        {
            Vector3 cikisPozisyonu = new Vector3((genislik - 2) * 2f, 0, (uzunluk - 1) * 2f);
            Quaternion kapiYonu = Quaternion.Euler(0, 0, 0); 
            Instantiate(cikisKapisiPrefab, cikisPozisyonu, kapiYonu, transform);
        }
    }

    void KapilariYerlestir()
    {
        yolListesi.Remove(new Vector2Int(1, 1));
        yolListesi.Remove(new Vector2Int(1, 0));
        yolListesi.Remove(new Vector2Int(genislik - 2, uzunluk - 1));
        yolListesi.Remove(new Vector2Int(genislik - 2, uzunluk - 2));

        // Yerleştirilen kapıların DoorController scriptlerini toplayacağımız liste
        List<DoorController> yerlesenKapilarListesi = new List<DoorController>();

        int yerlestirilenKapi = 0;
        int maxDeneme = 2500;
        
        List<Vector3> yerlesenKapilar = new List<Vector3>();
        
        while (yerlestirilenKapi < kapiSayisi && yolListesi.Count > 0 && maxDeneme > 0)
        {
            maxDeneme--;
            int rastgeleIndex = Random.Range(0, yolListesi.Count);
            Vector2Int secilenYol = yolListesi[rastgeleIndex];
            int x = secilenYol.x;
            int z = secilenYol.y;

            bool ustteDuvarVar = (z + 1 < uzunluk) && mazeGrid[x, z + 1] == 1;
            bool alttaDuvarVar = (z - 1 >= 0) && mazeGrid[x, z - 1] == 1;
            bool sagdaDuvarVar = (x + 1 < genislik) && mazeGrid[x + 1, z] == 1;
            bool soldaDuvarVar = (x - 1 >= 0) && mazeGrid[x - 1, z] == 1;

            bool dikeyKoridor = soldaDuvarVar && sagdaDuvarVar && !ustteDuvarVar && !alttaDuvarVar;
            bool yatayKoridor = ustteDuvarVar && alttaDuvarVar && !soldaDuvarVar && !sagdaDuvarVar;

            if (dikeyKoridor || yatayKoridor)
            {
                Vector3 kapiPozisyonu = new Vector3(x * 2f, 0, z * 2f);
                
                float gecerliMesafe = (maxDeneme > 500) ? kapiMesafeSiniri : 2f; 

                bool cokYakinKapiVar = false;
                foreach (Vector3 yerlesenKapi in yerlesenKapilar)
                {
                    if (Vector3.Distance(kapiPozisyonu, yerlesenKapi) < gecerliMesafe)
                    {
                        cokYakinKapiVar = true;
                        break;
                    }
                }

                if (!cokYakinKapiVar)
                {
                    Quaternion kapiYonu = Quaternion.identity;
                    if (yatayKoridor) kapiYonu = Quaternion.Euler(0, 90, 0);

                    GameObject olusanKapi = Instantiate(kapiPrefab, kapiPozisyonu, kapiYonu, transform);
                    DoorController dc = olusanKapi.GetComponentInChildren<DoorController>();
                    if (dc != null)
                    {
                        yerlesenKapilarListesi.Add(dc);
                    }

                    yerlesenKapilar.Add(kapiPozisyonu);
                    yerlestirilenKapi++;
                }
                
                yolListesi.RemoveAt(rastgeleIndex);
            }
        }

        // 🎯 RASTGELE SORU DAĞITIMI
        // Kaç soru çözülmesi gerekiyorsa o kadar kapıya soru veriyoruz, kalanlar boş kapı oluyor
        int gerekenSoruSayisi = (SoruManager.instance != null) ? SoruManager.instance.toplamKapiSayisi : yerlesenKapilarListesi.Count;

        // Kapıları rastgele karıştırıyoruz
        for (int i = 0; i < yerlesenKapilarListesi.Count; i++)
        {
            DoorController temp = yerlesenKapilarListesi[i];
            int rnd = Random.Range(i, yerlesenKapilarListesi.Count);
            yerlesenKapilarListesi[i] = yerlesenKapilarListesi[rnd];
            yerlesenKapilarListesi[rnd] = temp;
        }

        // İlk gerekenSoruSayisi kadar kapı soruları taşır, kalanı boş kapıdır
        for (int i = 0; i < yerlesenKapilarListesi.Count; i++)
        {
            if (i < gerekenSoruSayisi)
            {
                yerlesenKapilarListesi[i].soruVarMi = true;
            }
            else
            {
                yerlesenKapilarListesi[i].soruVarMi = false;
            }
        }
    }
}