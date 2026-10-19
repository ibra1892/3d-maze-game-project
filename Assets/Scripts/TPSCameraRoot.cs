using UnityEngine;

public class TPSCameraRoot : MonoBehaviour
{
    public Transform player;
    public Transform cam;

    [Header("Kamera Mesafesi")]
    public float distance = 2.2f;       // Normal takip mesafesi
    public float height = 1.0f;         // Baş/Omuz hizası

    [Header("Mouse")]
    public float mouseSensitivity = 100f;
    public float minY = -25f;
    public float maxY = 60f;

    [Header("Duvar Koruması")]
    public LayerMask collisionMask = ~0;   // Sadece duvar/zemin layer'larını seç (oyuncu, küçük eşyalar, düşmanlar hariç)
    public float minDistance = 0.4f;
    public float cameraRadius = 0.08f;
    public float wallBuffer = 0.12f;       // Duvarla kamera arası tampon

    [Header("Yumuşatma (titremeyi önler)")]
    public float shrinkTime = 0.05f;       // Duvara yaklaşınca kameranın yaklaşma süresi (kısa = hızlı)
    public float growTime = 0.35f;         // Engel kalkınca geri uzaklaşma süresi (uzun = yavaş, rahat)
    public float growDelay = 0.4f;         // Engel kalktıktan sonra bu kadar bekle, sonra uzaklaş

    [Header("Zemin Emniyeti")]
    public float minHeightAboveGround = 0.25f;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private float distVelocity;
    private float growTimer;

    private readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!cam)
        {
            Camera c = GetComponentInChildren<Camera>();
            if (c != null) cam = c.transform;
            else if (Camera.main != null) cam = Camera.main.transform;
        }

        if (!player)
        {
            if (transform.parent != null && transform.parent.CompareTag("Player"))
                player = transform.parent;
            else
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
        }

        if (player != null)
            yaw = player.eulerAngles.y;

        pitch = 10f;
        currentDistance = distance;
    }

    void LateUpdate()
    {
        if (!player) return;
        if (Time.timeScale == 0f) return;

        // Fare
        yaw   += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minY, maxY);

        transform.position = player.position;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Kameranın dönme noktası (omuz hizası). Hem ray hem kamera aynı noktadan hesaplanır.
        Vector3 pivot = player.position + Vector3.up * height;
        Vector3 backDir = -transform.forward;

        // En yakın GERÇEK engeli bul (oyuncunun kendi collider'larını atla)
        float nearest = distance;
        int count = Physics.SphereCastNonAlloc(pivot, cameraRadius, backDir, hits, distance,
                                               collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;
            if (col.transform == player || col.transform.IsChildOf(player)) continue;
            if (hits[i].distance < 0.001f) continue; // başlangıçta içinde kaldığımız collider
            if (hits[i].distance < nearest) nearest = hits[i].distance;
        }

        float targetDist = (nearest < distance)
            ? Mathf.Clamp(nearest - wallBuffer, minDistance, distance)
            : distance;

        // Yaklaşma hızlı, uzaklaşma yavaş ve gecikmeli -> "pompalama" olmaz
        if (targetDist < currentDistance)
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDist, ref distVelocity, shrinkTime);
            growTimer = growDelay;
        }
        else
        {
            growTimer -= Time.deltaTime;
            if (growTimer <= 0f)
                currentDistance = Mathf.SmoothDamp(currentDistance, targetDist, ref distVelocity, growTime);
            else
                distVelocity = 0f;
        }

        if (cam)
        {
            Vector3 camPos = pivot + backDir * currentDistance;

            // Kamera ayak tabanının altına inmesin
            float minY_World = player.position.y + minHeightAboveGround;
            if (camPos.y < minY_World) camPos.y = minY_World;

            cam.position = camPos;
            cam.rotation = transform.rotation;
        }
    }

    public float GetYaw() => yaw;
}