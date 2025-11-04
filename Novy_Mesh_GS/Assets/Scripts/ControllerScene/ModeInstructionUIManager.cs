using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModeInstructionUIManager : MonoBehaviour
{
    public static ModeInstructionUIManager Instance;

    [Header("UI Reference")]
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI headerText;
    public GameObject colorPicker;
    public GameObject deleteButton;
    public GameObject objectCatalogue;
    public GameObject findObjMenu;

    [Header("Enable this only in structured mode (via Inspector)")]
    public bool isStructuredMode = true;

    [Header("Instruction Canvas that holds text + image (optional)")]
    public GameObject instructionCanvas;
    public Sprite addDeleteImage;
    public Sprite manipulateImage;
    public Image instructionImage;
    public TextMeshProUGUI instructionText;

    private Vector2 originalTextAnchoredPosition;
    private Vector2 originalTextAnchorMin;
    private Vector2 originalTextAnchorMax;
    private TextAlignmentOptions originalTextAlignment;
    private bool originalTextLayoutCached = false;
    private RectTransform canvasRT;
    private float originalCanvasHeight;
    private bool canvasLayoutCached = false;
    public float minimizedCanvasHeight = 80f;
    public float centeredTextYOffset = 50f;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else Instance = this;

        if (colorPicker != null)
            colorPicker.SetActive(false);

        if (objectCatalogue != null)
            objectCatalogue.SetActive(false);

        if (deleteButton != null)
            deleteButton.SetActive(false);

        if (findObjMenu != null)
            findObjMenu.SetActive(false);
    }

    public void ShowSelectedInstruction(InteractionMode mode)
    {
        // Disable all menus by default
        if (colorPicker != null) colorPicker.SetActive(false);
        if (objectCatalogue != null) objectCatalogue.SetActive(false);
        if (deleteButton != null) deleteButton.SetActive(false);
        if (findObjMenu != null) findObjMenu.SetActive(false);

        // Enable UI panels based on mode
        switch (mode)
        {
            /*case InteractionMode.Recolor:
                if (colorPicker != null) colorPicker.SetActive(true);
                break;*/
            case InteractionMode.Add_Delete:
                if (objectCatalogue != null) objectCatalogue.SetActive(true);
                if (deleteButton != null) deleteButton.SetActive(true);
                break;
            case InteractionMode.Find:
                if (findObjMenu != null) findObjMenu.SetActive(true);
                break;
        }

        // Instruction text per mode
        string confirm = mode switch
        {
            InteractionMode.Manipulate =>
                "To move an object, grab it with the grip button on one controller and move your hand.\n" +
                "To rotate an object, grab it with both controllers using the grip buttons and twist your hands.\n" +
                "To scale an object, grab it with both controllers and move your hands apart to enlarge or closer to shrink.",

            InteractionMode.Recolor =>
                "First select an object with the grip button. Then use the color picker to choose a new color.",

            InteractionMode.Add_Delete =>
                "To add an object, browse the catalog and select an object with the trigger button. Then place it into the scene with another trigger press.\n" +
                "To delete an object, select it with the grip button. Then press the 'Delete' button that appears in the UI.",

            InteractionMode.Find =>
                "Select an object name from the list. Its location will be highlighted in the scene.",

            _ => "Select an interaction mode to begin."
        };

        string header = mode switch
        {
            InteractionMode.Manipulate => "MANIPULATING OBJECTS",
            InteractionMode.Recolor => "CHANGING COLORS",
            InteractionMode.Add_Delete => "ADD/DELETE OBJECTS",
            InteractionMode.Find => "FINDING OBJECTS",
            _ => "INTERACTION MODE"
        };

        headerText.text = header;
        descriptionText.text = confirm;

        // Show/hide image, resize canvas, center text if needed
        UpdateInstructionCanvasVisibility(mode);

        // Instruction image + text description (Structured mode)
        if (instructionText != null)
        {
            instructionText.text = mode switch
            {
                InteractionMode.Add_Delete =>
                    "Add a small sofa and a frame to the room (see image below). Delete the coffee machine.",

                InteractionMode.Manipulate =>
                    "Move the smallest plant to the top of the small table and make it (the plant) twice bigger. Ensure correct placement and orientation of the frame and small sofa as you see in this picture.",

                InteractionMode.Find =>
                    "Find smallest plant in the room.",

                InteractionMode.Recolor =>
                    "Make all the sofa and dining chairs in the room dark green.",

                _ => ""
            };
        }
    }

    public void UpdateInstructionCanvasVisibility(InteractionMode mode)
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
                originalTextAnchorMin = textRT.anchorMin;
                originalTextAnchorMax = textRT.anchorMax;
                originalTextAlignment = instructionText.alignment;
                originalTextLayoutCached = true;
            }

            bool showImage = (mode == InteractionMode.Manipulate || mode == InteractionMode.Add_Delete);
            bool hideImage = !showImage;

            if (isStructuredMode && hideImage)
            {
                // Center the instruction text with bottom padding
                textRT.anchoredPosition = new Vector2(originalTextAnchoredPosition.x, centeredTextYOffset);
                instructionText.alignment = TextAlignmentOptions.Center;

                // Shrink the canvas height
                if (canvasRT != null)
                {
                    Vector2 size = canvasRT.sizeDelta;
                    canvasRT.sizeDelta = new Vector2(size.x, minimizedCanvasHeight);
                }
            }
            else
            {
                // Restore original text position
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
                (mode == InteractionMode.Manipulate || mode == InteractionMode.Add_Delete);

            if (instructionImage.enabled)
            {
                instructionImage.sprite = mode == InteractionMode.Manipulate ? manipulateImage : addDeleteImage;
            }
        }
    }

}
