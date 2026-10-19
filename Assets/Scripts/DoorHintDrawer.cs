using System.Collections;
using UnityEngine;

public class DoorHintDrawer : MonoBehaviour
{
    [Header("Drawer Parts (Otomatik Bulunur)")]
    public GameObject drawerSlider;   // Drawer_Slider
    public GameObject scrollItem;     // Drawer_Scroll

    [Header("Movement Settings")]
    public float slideDistance = 0.35f; // İdeal açılma mesafesi
    public float openDuration = 0.4f;

    [Header("Audio (Opsiyonel / Yedek)")]
    public AudioClip customDrawerSound;

    public bool IsOpen { get; private set; }

    private Vector3 sliderClosedLocalPos;
    private Vector3 scrollInitialLocalPos;
    private Quaternion scrollInitialLocalRot;
    private Coroutine currentRoutine;

    private void OnValidate()
    {
        AutoFindParts();
    }

    void Awake()
    {
        AutoFindParts();

        if (drawerSlider != null)
        {
            // Kapalı pozisyonu garantiye al (Z ekseni sıfırda durmalı)
            Vector3 currentPos = drawerSlider.transform.localPosition;
            sliderClosedLocalPos = new Vector3(currentPos.x, currentPos.y, 0f);
            drawerSlider.transform.localPosition = sliderClosedLocalPos;
        }

        if (scrollItem != null)
        {
            scrollInitialLocalPos = scrollItem.transform.localPosition;
            scrollInitialLocalRot = scrollItem.transform.localRotation;
        }
    }

    public void AutoFindParts()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t.name == "Drawer_Slider" && drawerSlider == null)
            {
                drawerSlider = t.gameObject;
            }
            if (t.name == "Drawer_Scroll" && scrollItem == null)
            {
                scrollItem = t.gameObject;
            }
        }
    }

    public void OpenDrawerCinematic(System.Action onComplete = null)
    {
        OpenDrawerCinematic(null, onComplete);
    }

    public void OpenDrawerCinematic(Transform playerCamera, System.Action onComplete)
    {
        if (IsOpen) return;
        IsOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(OpenRoutine(playerCamera, onComplete));
    }

    private IEnumerator OpenRoutine(Transform playerCamera, System.Action onComplete)
    {
        if (drawerSlider == null) AutoFindParts();

        if (drawerSlider == null)
        {
            Debug.LogWarning("[DoorHintDrawer] Drawer Slider bulunamadı! Flama direkt açılıyor.");
            onComplete?.Invoke();
            yield break;
        }

        Transform sliderTrans = drawerSlider.transform;

        // 1. Ses Çalma Garantisi
        PlayHintSound();

        // 2. Oyuncunun Gerçek Pozisyonunu Bul (Kameradan ziyade karakterin gövdesi)
        Transform target = FindPlayerTarget(playerCamera);

        // 3. Çift Yön Tespiti (Dünya ekseninde kusursuz Dot Product)
        float direction = 1f;
        if (target != null)
        {
            // Çekmecenin yerel Z ekseninin dünya uzayındaki yönü
            Vector3 drawerForward = sliderTrans.parent != null ? sliderTrans.parent.forward : sliderTrans.forward;
            
            // Çekmeceden oyuncuya giden doğrultu
            Vector3 toPlayer = target.position - sliderTrans.position;

            // Yükseklik farkını yok sayıp sadece zemin açısını kıyasla
            drawerForward.y = 0f;
            toPlayer.y = 0f;

            float dot = Vector3.Dot(drawerForward.normalized, toPlayer.normalized);
            direction = (dot >= 0f) ? 1f : -1f;

            Debug.Log($"[DoorHintDrawer] Kapı Yön Tespiti -> Hedef: {target.name}, Dot: {dot:F2}, Yön: {(direction > 0 ? "Öne (+Z)" : "Arkaya (-Z)")}");
        }
        else
        {
            Debug.LogWarning("[DoorHintDrawer] Hedef oyuncu bulunamadı! Varsayılan yön kullanılıyor.");
        }

        Vector3 sliderTargetPos = sliderClosedLocalPos + new Vector3(0, 0, slideDistance * direction);
        float elapsed = 0f;

        // Çekmecenin kayma animasyonu
        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            sliderTrans.localPosition = Vector3.Lerp(sliderClosedLocalPos, sliderTargetPos, elapsed / openDuration);
            yield return null;
        }
        sliderTrans.localPosition = sliderTargetPos;

        // Rulo kâğıdın kameraya doğru süzülmesi
        Transform camTarget = GetCameraTarget(playerCamera);
        if (scrollItem != null && camTarget != null)
        {
            Transform scrollTrans = scrollItem.transform;
            elapsed = 0f;
            float scrollDuration = 0.35f;
            Vector3 scrollStartWorld = scrollTrans.position;
            Vector3 targetWorldPos = camTarget.position + (camTarget.forward * 0.4f) - (camTarget.up * 0.05f);

            while (elapsed < scrollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                scrollTrans.position = Vector3.Lerp(scrollStartWorld, targetWorldPos, elapsed / scrollDuration);
                yield return null;
            }
        }

        onComplete?.Invoke();
    }

    public void CloseDrawerInstant()
    {
        IsOpen = false;
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        if (scrollItem != null)
        {
            scrollItem.transform.localPosition = scrollInitialLocalPos;
            scrollItem.transform.localRotation = scrollInitialLocalRot;
        }

        if (drawerSlider != null)
        {
            drawerSlider.transform.localPosition = sliderClosedLocalPos;
        }
    }

    private Transform FindPlayerTarget(Transform preferred)
    {
        if (preferred != null) return preferred;

        // 1. Player Tag'i ile ara
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) return playerObj.transform;

        // 2. Hierarchy'deki "Player" ismi ile ara
        playerObj = GameObject.Find("Player");
        if (playerObj != null) return playerObj.transform;

        // 3. Kamera referansları
        if (Camera.main != null) return Camera.main.transform;

        return null;
    }

    private Transform GetCameraTarget(Transform preferred)
    {
        if (preferred != null) return preferred;
        if (Camera.main != null) return Camera.main.transform;

        Camera anyCam = Object.FindFirstObjectByType<Camera>();
        return anyCam != null ? anyCam.transform : null;
    }

    private void PlayHintSound()
    {
        // AudioManager üzerinden parşömen/açılma sesi
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDrawerOpen();
        }

        // Varsa kapı üzerindeki yerel AudioSource
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            if (customDrawerSound != null)
            {
                audioSource.PlayOneShot(customDrawerSound);
            }
            else if (audioSource.clip != null)
            {
                audioSource.Play();
            }
        }
    }
}