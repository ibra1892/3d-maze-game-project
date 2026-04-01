using UnityEngine;

public class TPSCameraRoot : MonoBehaviour
{
    public Transform player;
    public Transform cam;

    [Header("Camera Offset")]
    public float distance = 4f;
    public float height = 0.9f;

    [Header("Mouse")]
    public float mouseSensitivity = 100f;
    public float minY = -30f;
    public float maxY = 60f;

    float yaw;
    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!cam)
            cam = GetComponentInChildren<Camera>().transform;

        yaw = player.eulerAngles.y;
        pitch = 10f;

        cam.localPosition = new Vector3(0f, height, -distance);
        cam.localRotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (!player) return;

        yaw   += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minY, maxY);

        transform.position = player.position;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public float GetYaw() => yaw;
}
