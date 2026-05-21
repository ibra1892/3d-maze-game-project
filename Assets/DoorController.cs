using UnityEngine;

public class DoorController : MonoBehaviour
{
    private bool isPlayerNearby = false;
    private bool isOpen = false;

    public float openAngle = 90f; 
    public float smoothSpeed = 3f; 

    private Quaternion targetRotation;
    private Quaternion startRotation;

    void Start()
    {
        startRotation = transform.rotation;
        targetRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + openAngle, transform.eulerAngles.z);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
        }

        Quaternion currentTarget = isOpen ? targetRotation : startRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, currentTarget, Time.deltaTime * smoothSpeed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
        }
    }
}