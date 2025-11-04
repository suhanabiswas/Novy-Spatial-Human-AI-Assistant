using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LLMInteractionModeManager : MonoBehaviour
{
    public enum InteractionMode
    {
        Manipulate,
        Recolor,
        Find,
        Add_Delete
    }

    [Header("Enable this only in structured mode (via Inspector)")]
    public bool isStructuredMode = true;

    [Header("Instruction Canvas that holds text + image (optional)")]
    public GameObject instructionCanvas;  

    public InteractionMode currentMode = InteractionMode.Add_Delete;

    public static LLMInteractionModeManager Instance { get; private set; }

    [Header("Optional: Assign your object catalog UI here")]
    public GameObject objectCatalogUI;

    [Header("Assign the icon image component (e.g., on a UI Image)")]
    public Image interactionModeIcon;
    public Image instructionImage;

    public TextMeshProUGUI feedbackText;

    public TextMeshProUGUI instructionText;

    [Header("Assign sprites for each interaction mode")]
    public Sprite manipulateIcon;
    public Sprite recolorIcon;
    public Sprite findIcon;
    public Sprite addIcon;
    public Sprite addDeleteImage;
    public Sprite manipulateImage;
    public GameObject deleteIcon;

    private InteractionMode previousMode;

    private Vector2 originalTextAnchoredPosition;
    private Vector2 originalTextAnchorMin;
    private Vector2 originalTextAnchorMax;
    private TextAlignmentOptions originalTextAlignment;
    private bool originalTextLayoutCached = false;
    private RectTransform canvasRT;
    private float originalCanvasHeight;
    private bool canvasLayoutCached = false;
    public float minimizedCanvasHeight = 80f;
    public float centeredTextYOffset = 50f;         // Padding from bottom when centered

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;

        UpdateCatalogVisibility();
        UpdateIcon();
        UpdateInstructionCanvasVisibility();
    }

    private void Start()
    {
        UpdateCatalogVisibility();
        UpdateIcon();
        UpdateInstructionCanvasVisibility();
    }

    private void Update()
    {
        if (currentMode != previousMode)
        {
            previousMode = currentMode;
            UpdateCatalogVisibility();
            UpdateIcon();
            UpdateInstructionCanvasVisibility();
        }
    }

    public string GetCurrentModeAsString()
    {
        return currentMode.ToString().ToLower();
    }

    public InteractionMode GetCurrentMode()
    {
        return currentMode; 
    }


    public void SetInteractionMode(InteractionMode newMode)
    {
        currentMode = newMode;
        UpdateCatalogVisibility();
        UpdateIcon();
        UpdateTexts();
        UpdateInstructionCanvasVisibility();
    }

    private void UpdateInstructionCanvasVisibility()
    {
        if (instructionCanvas != null)
        {
            instructionCanvas.SetActive(isStructuredMode);

            if (!canvasLayoutCached)
            {
                canvasRT = instructionCanvas.GetComponent<RectTransform>();
                if (canvasRT != null)
                {
                    originalCanvasHeight = canvasRT.sizeDelta.y;
                    canvasLayoutCached = true;
                }
            }
        }

        if (instructionText != null)
        {
            instructionText.enabled = isStructuredMode;

            RectTransform textRT = instructionText.GetComponent<RectTransform>();

            if (!originalTextLayoutCached)
            {
                originalTextAnchoredPosition = textRT.anchoredPosition;
                originalTextAlignment = instructionText.alignment;
                originalTextLayoutCached = true;
            }

            bool hideImage = (instructionImage == null || !instructionImage.enabled);

            if (isStructuredMode && hideImage)
            {
                // Center text with padding at bottom
                textRT.anchoredPosition = new Vector2(originalTextAnchoredPosition.x, centeredTextYOffset);
                instructionText.alignment = TextAlignmentOptions.Center;

                // Shrink canvas height
                if (canvasRT != null)
                {
                    Vector2 size = canvasRT.sizeDelta;
                    canvasRT.sizeDelta = new Vector2(size.x, minimizedCanvasHeight);
                }
            }
            else
            {
                // Restore text layout
                textRT.anchoredPosition = originalTextAnchoredPosition;
                instructionText.alignment = originalTextAlignment;

                // Restore canvas height
                if (canvasRT != null)
                {
                    Vector2 size = canvasRT.sizeDelta;
                    canvasRT.sizeDelta = new Vector2(size.x, originalCanvasHeight);
                }
            }
        }

        if (instructionImage != null)
        {
            instructionImage.enabled = isStructuredMode &&
                (currentMode == InteractionMode.Manipulate || currentMode == InteractionMode.Add_Delete);
        }
    }

    private void UpdateTexts()
    {
        if (feedbackText == null) return;

        switch (currentMode)
        {
            case InteractionMode.Add_Delete:
                feedbackText.text = "Add objects wherever you want in the room, or delete objects you don't want. Once added, objects cannot be moved in this mode";
                instructionText.text = "Add a small sofa and a frame to the room (see image below). Delete the coffee machine.";
                break;
            case InteractionMode.Manipulate:
                feedbackText.text = "Manipulate objects: move it, scale it, or rotate it however you want.";
                instructionText.text = "Move the smallest plant to the top of the small table and make it (the plant) twice bigger. Ensure correct placement and orientation of the frame and small sofa as you see in this picture.";
                break;
            case InteractionMode.Find:
                feedbackText.text = "Find objects in the room.";
                instructionText.text = "Find smallest plant in the room.";
                break;
            case InteractionMode.Recolor:
                feedbackText.text = "Change the color of objects in the room.";
                instructionText.text = "Make all the sofa and dining chairs in the room dark green.";
                break;
        }
    }

    private void UpdateCatalogVisibility()
    {
        if (objectCatalogUI != null)
        {
            objectCatalogUI.SetActive(currentMode == InteractionMode.Add_Delete);
        }
    }

    private void UpdateIcon()
    {
        if (interactionModeIcon == null) return;

        switch (currentMode)
        {
            case InteractionMode.Manipulate:
                interactionModeIcon.sprite = manipulateIcon;
                instructionImage.enabled = true;
                instructionImage.sprite = manipulateImage;
                deleteIcon.SetActive(false);
                break;
            case InteractionMode.Recolor:
                interactionModeIcon.sprite = recolorIcon;
                instructionImage.sprite = null;
                instructionImage.enabled = false;
                deleteIcon.SetActive(false);
                break;
            case InteractionMode.Find:
                interactionModeIcon.sprite = findIcon;
                instructionImage.sprite = null;
                instructionImage.enabled = false;
                deleteIcon.SetActive(false);
                break;
            case InteractionMode.Add_Delete:
                interactionModeIcon.sprite = addIcon;
                instructionImage.enabled = true;
                instructionImage.sprite = addDeleteImage;
                deleteIcon.SetActive(true);
                break;
        }
    }
}
