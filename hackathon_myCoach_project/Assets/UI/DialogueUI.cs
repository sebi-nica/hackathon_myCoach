using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance;

    [Header("Wire these in the Inspector")]
    public GameObject panel;
    public Image portraitImage;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerName;

    private Action onCloseCallback;

    void Awake()
    {
        // If an instance already exists, do absolutely nothing and wait to be destroyed
        if (Instance != null && Instance != this) 
        {
            return; 
        }

        Instance = this;
        panel.SetActive(false);
    }

    // ADDED the "string speaker" parameter here
    public void ShowDialogue(Sprite portrait, string speaker, string text, Action onClose = null)
    {
        portraitImage.sprite = portrait;
        speakerName.text = speaker;     // Sets the UI text
        dialogueText.text = text;
        onCloseCallback = onClose;
        
        panel.SetActive(true);
        Time.timeScale = 0f;           // pause game
    }

    void Update()
    {
        if (!panel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            CloseDialogue();
    }

    public void CloseDialogue()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;           // unpause game

        // Fire the callback
        onCloseCallback?.Invoke();
        onCloseCallback = null;
    }

    void OnDestroy()
    {
        // If this specific instance is the one being destroyed, clear the global slot
        if (Instance == this)
        {
            Instance = null;
        }
    }
}