using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using HSVPicker;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using TMPro;


[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class ModeBasedInteractable : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private GameObject modeUI;
    private XRGeneralGrabTransformer grabTransformer;

    [Header("Outline Settings")]
    private Outline outline;
    public Color hoverColor = Color.white;
    public Color selectColor = Color.green;
    public Color deleteColor = Color.red;
    public float outlineWidth = 10f;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private static ModeBasedInteractable currentlySelectedInteractable;
    private GameObject colorPickerGO;
    private GameObject objectCatalogue;
    private Image colorPickerInitialColor;
    private ColorPicker colorPicker;
    private ObjectMetadata objectMetadata;
    private static List<ModeBasedInteractable> allInteractables = new List<ModeBasedInteractable>();

    [SerializeField]
    private Button deleteButton;
    private TextMeshProUGUI deleteUIText;

    //public DashboardControllerConnection dashboardConnection;

    private void OnEnable()
    {
        allInteractables.Add(this);

        if (ModeManager.Instance != null)
        {
            ModeManager.Instance.OnModeChanged += HandleModeChanged;
            HandleModeChanged(ModeManager.Instance.CurrentMode);
        }
    }

    private void OnDisable()
    {
        allInteractables.Remove(this);

        if (ModeManager.Instance != null)
        {
            ModeManager.Instance.OnModeChanged -= HandleModeChanged;
        }
    }

    public static void UpdateAllInteractables(InteractionMode newMode)
    {
        foreach (var interactable in allInteractables)
        {
            interactable.HandleModeChanged(newMode);
        }
    }

    private bool IsBuilding()
    {
        return objectMetadata != null && objectMetadata.category == ObjectCategory.Building;
    }

    private bool CanManipulate()
    {
        return !IsBuilding();
    }

    private bool CanDelete()
    {
        return !IsBuilding();
    }

    private bool CanRecolor()
    {
        return true; // Always allowed, even for buildings
    }

    private bool CanFind()
    {
        return true; // Always allowed, even for buildings
    }

    private void Awake()
    {
        objectMetadata = GetComponent<ObjectMetadata>();
        //dashboardConnection = FindObjectOfType<DashboardControllerConnection>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabTransformer = GetComponent<XRGeneralGrabTransformer>();
        rb = GetComponent<Rigidbody>();
        modeUI = GameObject.Find("ModeUI");



        if (modeUI != null)
        {
            colorPickerGO = modeUI.transform.Find("ColorPicker")?.gameObject;
            objectCatalogue = modeUI.transform.Find("AddObjectMenu")?.gameObject;
            deleteButton = modeUI.transform.Find("DeleteSelectedObjButton")?.gameObject.GetComponent<Button>();
            Transform deleteTextTransform = modeUI.transform.Find("DeleteSelectedObjButton/DeleteText");

            if (deleteTextTransform != null)
            {
                deleteUIText = deleteTextTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogWarning("DeleteText (child of DeleteSelectedObjButton) not found.");
            }

            if (colorPickerGO != null)
            {
                colorPicker = colorPickerGO.GetComponent<ColorPicker>();

                // Navigate to Fill: ColorPicker > ColorField > Color > Fill
                Transform colorField = colorPickerGO.transform.Find("ColorField");
                if (colorField != null)
                {
                    Transform color = colorField.Find("Color");
                    if (color != null)
                    {
                        Transform fill = color.Find("Fill");
                        if (fill != null)
                        {
                            colorPickerInitialColor = fill.GetComponent<Image>();
                            Button fillButton = fill.GetComponent<Button>();

                            if (fillButton != null && colorPickerInitialColor != null && colorPicker != null)
                            {
                                fillButton.onClick.AddListener(() =>
                                {
                                    colorPicker.AssignColor(colorPickerInitialColor.color);
                                    Debug.Log("Assigned color from Fill to ColorPicker: " + colorPickerInitialColor.color);
                                });
                            }
                            else
                            {
                                Debug.LogWarning("Missing Button, Image, or ColorPicker reference.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("Fill child not found under Color.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Color child not found under ColorField.");
                    }
                }
                else
                {
                    Debug.LogWarning("ColorField child not found under ColorPicker.");
                }
            }
            else
            {
                Debug.LogWarning("ColorPicker GameObject not found in ModeUI.");
            }
        }
    }


    private void Start()
    {
        //grabInteractable.enabled = true;
        rb.isKinematic = true;
        // Store original transform before any manipulation
        originalParent = GameObject.Find("OfficeForController").transform;

        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;

        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
        grabInteractable.selectEntered.AddListener(OnSelected);
        grabInteractable.selectExited.AddListener(OnDeselected);
    }

    public void HandleModeChanged(InteractionMode newMode)
    {
        bool enableGrab = (newMode == InteractionMode.Manipulate && CanManipulate()) ||
                     (newMode == InteractionMode.Recolor && CanRecolor()) ||
                     (newMode == InteractionMode.Add_Delete && CanDelete());

        grabInteractable.enabled = enableGrab;

        if (newMode == InteractionMode.Add_Delete)
        {
            // Explicitly disable movement in Add_Delete mode
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            rb.isKinematic = true;
        }

        Debug.Log($"{gameObject.name} Mode changed to {newMode}, grab enabled: {enableGrab}");

        if (newMode == InteractionMode.Find)
        {
            RemoveOutline();
            rb.isKinematic = true;
        }

        if (newMode != InteractionMode.Add_Delete && currentlySelectedInteractable == this)
        {
            currentlySelectedInteractable = null;
            if (deleteUIText != null)
                deleteUIText.text = "Select object to delete";
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        var mode = ModeManager.Instance.CurrentMode;
        if ((mode == InteractionMode.Manipulate && CanManipulate()) ||
            (mode == InteractionMode.Recolor && CanRecolor()))
        {
            AddOutline(hoverColor);
        }
        else if (mode == InteractionMode.Manipulate && IsBuilding())
        {
            AddOutline(Color.yellow); // Different color to indicate restricted object
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        RemoveOutline();
    }

    private void OnDeselected(SelectExitEventArgs args)
    {
        var mode = ModeManager.Instance.CurrentMode;
        grabInteractable.useDynamicAttach = true;
        // Only reparent if we're in Add/Delete mode
        if (mode == InteractionMode.Add_Delete && originalParent != null)
        {
            transform.SetParent(originalParent);
        }

        RemoveOutline();
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        var mode = ModeManager.Instance.CurrentMode;
        Debug.Log(gameObject.name + " selected");
        //dashboardConnection.LogToDashboard($"Object selected: {gameObject.name} in {mode} mode", true);

        if (IsBuilding() && mode == InteractionMode.Manipulate)
        {
            //dashboardConnection.LogToDashboard($"Buildings cannot be manipulated", true);
            return;
        }

        if (mode == InteractionMode.Add_Delete)
        {
            if (IsBuilding())
            {
                //dashboardConnection.LogToDashboard($"Buildings cannot be deleted", true);
                return;
            }

            // Explicitly disable movement
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            rb.isKinematic = true;
            AddOutline(deleteColor);

            currentlySelectedInteractable = this;
            if (deleteUIText != null)
            {
                deleteUIText.text = "Delete " + gameObject.name;
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() =>
                {
                    if (currentlySelectedInteractable != null)
                    {
                        currentlySelectedInteractable.gameObject.SetActive(false);
                        //dashboardConnection?.LogToDashboard($"Object deleted: {currentlySelectedInteractable.gameObject.name}", true);
                        currentlySelectedInteractable = null;
                        deleteUIText.text = "Delete: Select desired object with grab button";
                    }
                });
            }
            return;
        }

        if (mode == InteractionMode.Manipulate || mode == InteractionMode.Recolor || mode == InteractionMode.Add_Delete)
        {
            if (mode == InteractionMode.Add_Delete) AddOutline(deleteColor);
            else AddOutline(selectColor);

            modeUI.SetActive(mode == InteractionMode.Recolor ||mode == InteractionMode.Add_Delete);
            //modeUI.SetActive(true);

            rb.isKinematic = false;
            rb.useGravity = false;
            grabInteractable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            grabInteractable.useDynamicAttach = true;

            if (grabTransformer != null)
            {
                grabTransformer.allowTwoHandedScaling = false;
            }

            switch (mode)
            {
                case InteractionMode.Manipulate:
                    if (grabTransformer != null)
                    {
                        grabTransformer.allowTwoHandedScaling = true;
                    }
                    grabInteractable.trackPosition = true;
                    grabInteractable.trackRotation = true;
                    //dashboardConnection?.LogToDashboard($"Manipulation started on {gameObject.name}");
                    if (colorPickerGO != null) colorPickerGO.SetActive(false);
                    break;

                case InteractionMode.Recolor:
                    grabInteractable.trackPosition = false;
                    grabInteractable.trackRotation = false;
                    rb.isKinematic = true; // prevents physics jank
                    
                    if (colorPickerGO != null && colorPicker != null)
                    {
                        colorPickerGO.SetActive(true);

                        colorPicker.onValueChanged.RemoveAllListeners();

                        colorPicker.onValueChanged.AddListener(color =>
                        {
                            var renderer = GetComponent<Renderer>();
                            if (renderer == null)
                                renderer = GetComponentInChildren<Renderer>();
                            if (renderer)
                                renderer.material.color = color;
                            //dashboardConnection?.LogToDashboard($"Color changed to {ColorUtility.ToHtmlStringRGB(color)} on {gameObject.name}");
                        });

                        var currentRenderer = GetComponent<Renderer>();
                        if (currentRenderer == null)
                            currentRenderer = GetComponentInChildren<Renderer>();
                        if (currentRenderer != null)
                        {
                            colorPicker.AssignColor(currentRenderer.material.color);
                            if (colorPickerInitialColor != null)
                                colorPickerInitialColor.color = currentRenderer.material.color;
                        }
                    }
                    break;

                case InteractionMode.Add_Delete:
                    grabInteractable.trackPosition = false;
                    grabInteractable.trackRotation = false;
                    rb.isKinematic = true; // prevents physics jank
                    currentlySelectedInteractable = this;

                    if (deleteUIText != null)
                    {
                        deleteUIText.text = "Delete " + gameObject.name;
                    }

                    if (deleteButton != null)
                    {
                        deleteButton.onClick.RemoveAllListeners();
                        deleteButton.onClick.AddListener(() =>
                        {
                            if (currentlySelectedInteractable != null)
                            {
                                currentlySelectedInteractable.gameObject.SetActive(false);
                                //dashboardConnection?.LogToDashboard($"Object deleted: {currentlySelectedInteractable.gameObject.name}", true);
                                currentlySelectedInteractable = null;
                                deleteUIText.text = "Delete: Select desired object with grab button";
                            }
                        });
                    }
                    break;
            }
        }
    }

    private void AddOutline(Color color)
    {
        if (outline == null)
        {
            outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineWidth = outlineWidth;
        }

        outline.OutlineColor = color;
        outline.enabled = true;
    }

    private void SetOutlineColor(Color color)
    {
        if (outline != null)
            outline.OutlineColor = color;
    }

    private void RemoveOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }
}

public enum InteractionMode
{
    None,
    Manipulate,
    Recolor,
    Add_Delete,
    Find
}

