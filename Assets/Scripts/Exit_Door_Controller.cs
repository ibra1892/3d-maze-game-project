using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using System.Collections;

public class Exit_Door_Controller : MonoBehaviour
{
    private Transform player;
    private bool isOpening = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    // [FormerlySerializedAs] keeps the values you already set in the Inspector
    // after the fields were renamed from Turkish to English.
    [Header("Door Movement")]
    [FormerlySerializedAs("yukselmeMiktari")] public float riseAmount = 8f;
    [FormerlySerializedAs("acilmaHizi")] public float openSpeed = 2.5f;

    [Header("UI")]
    [FormerlySerializedAs("mesajPaneli")] public GameObject messagePanel;
    private TextMeshProUGUI messageText;

    [Header("Distance")]
    [FormerlySerializedAs("algilamaMesafesi")] public float detectionDistance = 5f;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseAmount;

        // If the panel reference is lost, look for it under the Canvas when the game starts
        if (messagePanel == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform panel = canvas.transform.Find("ExitMessagePanel");
                if (panel != null) messagePanel = panel.gameObject;
            }
        }

        // Hide the panel and grab its text component
        if (messagePanel != null)
        {
            messageText = messagePanel.GetComponentInChildren<TextMeshProUGUI>();
            messagePanel.SetActive(false);
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    void Update()
    {
        if (player == null) return;

        if (isOpening)
        {
            // Keep the door animation running even when time is paused
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, openSpeed * Time.unscaledDeltaTime);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionDistance && Input.GetKeyDown(KeyCode.E))
        {
            TryExit();
        }
    }

    void TryExit()
    {
        if (messageText != null)
        {
            if (QuestionManager.instance != null && QuestionManager.instance.AreAllQuestionsSolved())
            {
                isOpening = true;

                string successMessage = "<b><size=140%><color=#145A32>CONGRATULATIONS!</color></size></b>\n\n" +
                                        "<size=95%><color=#2C1A0C>You escaped the maze.</color></size>";

                messageText.text = successMessage;
                messagePanel.SetActive(true);

                Collider doorCollider = GetComponent<Collider>();
                if (doorCollider != null) doorCollider.enabled = false;

                if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

                Time.timeScale = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                string warningMessage = "<b><size=140%><color=#800000>DOOR LOCKED</color></size></b>\n\n" +
                                        "<size=95%><color=#2C1A0C>You can't leave until you solve all the questions in the maze.</color></size>";

                StartCoroutine(ShowWarning(warningMessage));
            }
        }
    }

    IEnumerator ShowWarning(string message)
    {
        if (messagePanel != null && messageText != null)
        {
            messageText.text = message;
            messagePanel.SetActive(true);
            yield return new WaitForSeconds(3.5f);
            messagePanel.SetActive(false);
        }
    }
}