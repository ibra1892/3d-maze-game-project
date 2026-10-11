using UnityEngine;
using UnityEngine.Serialization;

public class DoorController : MonoBehaviour
{
    public bool isOpen = false;

    // [FormerlySerializedAs] keeps the values you already set in the Inspector
    // after the fields were renamed from Turkish to English.
    [Header("Question Setting")]
    [FormerlySerializedAs("soruVarMi")] public bool hasQuestion = false; // Does this door have a question, or is it an empty door?

    private bool isPlayerNearby = false;

    [Header("Door Slide Settings")]
    [FormerlySerializedAs("yukselmeMiktari")] public float riseAmount = 3.5f; // How many meters the door slides up
    private Vector3 startPosition;
    private Vector3 targetPosition;

    void Start()
    {
        // Remember the door's starting local position
        startPosition = transform.localPosition;

        // Target position: the same spot, raised on the Y axis
        targetPosition = startPosition + new Vector3(0, riseAmount, 0);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!isOpen)
            {
                if (hasQuestion)
                {
                    // Door with a question: open the parchment question screen
                    QuestionManager.instance.AskQuestion(gameObject);
                }
                else
                {
                    // Empty door: open right away and play the sound effect
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
                    OpenDoor();
                }
            }
        }

        // Smooth sliding animation (Lerp)
        Vector3 currentTarget = isOpen ? targetPosition : startPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, currentTarget, Time.deltaTime * 5f);
    }

    // Called when the question is answered correctly, or when the door has no question
    public void OpenDoor()
    {
        isOpen = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = false;
    }
}