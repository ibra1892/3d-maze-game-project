using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Stamina")]
    public Image staminaBarFill;
    public TMP_Text staminaText;


    private PlayerMovement player;
    private float staminaBarMaxWidth;

    void Start()
    {
        player = Object.FindFirstObjectByType<PlayerMovement>();
        if(player == null) Debug.LogError("HUDManager: Oyuncu (PlayerMovement) bulunamadı!");
        if (staminaBarFill == null) Debug.LogError("HUDManager: StaminaBarFill atanmamış!");
        staminaBarMaxWidth = staminaBarFill.rectTransform.sizeDelta.x;
        UpdateStamina();
    }

    void Update()
    {
        if (player == null) return;

        UpdateStamina();
    }

    private float logTimer = 0f;
    void UpdateStamina()
    {
        float ratio = player.Stamina / player.maxStamina;

        Vector2 size = staminaBarFill.rectTransform.sizeDelta;
        size.x = staminaBarMaxWidth * ratio;
        staminaBarFill.rectTransform.sizeDelta = size;

        // Stamina rengi: Sarıdan yeşile
        staminaBarFill.color = Color.Lerp(Color.yellow, new Color(0.4f, 1f, 0.4f), ratio);

        if (staminaText != null)
            staminaText.text = $" {Mathf.CeilToInt(player.Stamina)} / {player.maxStamina}";

        logTimer += Time.deltaTime; // Her saniye artar
        if (logTimer >= 5f) // 5 saniye dolunca içeri girer
        {
            Debug.Log("Sistem Raporu - Güncel Stamina: " + Mathf.CeilToInt(player.Stamina));
            logTimer = 0f; // Sayacı sıfırla ki tekrar saysın
        }
    }
}