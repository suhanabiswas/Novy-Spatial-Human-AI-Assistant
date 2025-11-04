using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class CatalogUIManager : MonoBehaviour
{
    public ModeManager modeManager;
    public GameObject vasePrefab;
    public GameObject trashCanPrefab;
    public GameObject framePrefab;
    public GameObject smallSofaPrefab;
    public GameObject wallClockPrefab;
    public GameObject globePrefab;
    public Transform controllerAttachPoint;
    public GameObject officeRoot;
    //public DashboardControllerConnection dashboardConnection;

    private GameObject currentPreview;
    private InputDevice rightController;

    private enum ObjectType { None, Vase, TrashCan, WallClock, Frame, SmallSofa, Globe }
    private ObjectType selectedObjectType = ObjectType.None;

    void Start()
    {
        // Get right-hand controller
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
            rightController = devices[0];
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
                rightController = devices[0];
        }

        if (currentPreview != null && rightController.isValid)
        {
            if (rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool isPressed) && isPressed)
            {
                DropCurrentObject();
            }
        }
    }

    public void OnVaseButtonClicked()
    {
        selectedObjectType = ObjectType.Vase;
        SpawnSelectedObject();
    }

    public void OnFrameButtonClicked()
    {
        selectedObjectType = ObjectType.Frame;
        SpawnSelectedObject();
    }

    public void OnSmallSofaButtonClicked()
    {
        selectedObjectType = ObjectType.SmallSofa;
        SpawnSelectedObject();
    }

    public void OnTrashCanButtonClicked()
    {
        selectedObjectType = ObjectType.TrashCan;
        SpawnSelectedObject();
    }

    public void OnWallClockButtonClicked()
    {
        selectedObjectType = ObjectType.WallClock;
        SpawnSelectedObject();
    }
    public void OnGlobeButtonClicked()
    {
        selectedObjectType = ObjectType.Globe;
        SpawnSelectedObject();
    }

    private void SpawnSelectedObject()
    {
        if (controllerAttachPoint == null) return;

        GameObject prefabToSpawn = null;
        switch (selectedObjectType)
        {
            case ObjectType.Vase:
                prefabToSpawn = vasePrefab;
                break;
            case ObjectType.Frame:
                prefabToSpawn = framePrefab;
                break;
            case ObjectType.SmallSofa:
                prefabToSpawn = smallSofaPrefab;
                break;
            case ObjectType.TrashCan:
                prefabToSpawn = trashCanPrefab;
                break;
            case ObjectType.WallClock:
                prefabToSpawn = wallClockPrefab;
                //rotate by 180degrees
                break;
            case ObjectType.Globe:
                prefabToSpawn = globePrefab;
                break;
        }

        if (prefabToSpawn == null) return;

        // Instantiate and parent to controller first (for preview)
        currentPreview = Instantiate(prefabToSpawn, controllerAttachPoint.position, controllerAttachPoint.rotation);
        currentPreview.transform.SetParent(controllerAttachPoint);

        // Rotate frame and wall clock 180° around Y-axis
        if (selectedObjectType == ObjectType.Frame || selectedObjectType == ObjectType.WallClock)
        {
            currentPreview.transform.Rotate(0f, 180f, 0f);
        }

        //dashboardConnection?.LogToDashboard($"Adding object: {currentPreview.name}");

        var rb = currentPreview.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        var grabInteractable = currentPreview.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable) grabInteractable.enabled = false;
    }


    private void DropCurrentObject()
    {
        if (currentPreview != null)
        {
            currentPreview.transform.SetParent(null);

            // Get the ModeBasedInteractable component and initialize it properly
            var interactable = currentPreview.GetComponent<ModeBasedInteractable>();
            if (interactable != null)
            {
                // Force it to handle the current mode immediately
                interactable.HandleModeChanged(ModeManager.Instance.CurrentMode);
            }

            var rb = currentPreview.GetComponent<Rigidbody>();
            currentPreview.tag = "SpatialObject";

            officeRoot = GameObject.Find("OfficeForController");
            if (officeRoot != null)
            {
                currentPreview.transform.SetParent(officeRoot.transform);
            }

            var grabInteractable = currentPreview.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable) grabInteractable.enabled = true;

            currentPreview = null;
            selectedObjectType = ObjectType.None;
        }
    }


    private void TrySnapToSurface(GameObject obj)
    {
        if (Physics.Raycast(obj.transform.position, Vector3.down, out RaycastHit hit, 5f))
        {
            obj.transform.position = hit.point;
        }
    }
}
