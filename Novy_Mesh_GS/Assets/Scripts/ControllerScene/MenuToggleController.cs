using UnityEngine;
using UnityEngine.InputSystem;

public class MenuToggleController : MonoBehaviour
{
    [Header("Input Action References")]
    [Tooltip("Input Action for toggling the menu (e.g., Y button on left controller)")]
    public InputActionProperty toggleAction;

    [Tooltip("Input Action for undoing the last action (e.g., A button on right controller)")]
    public InputActionProperty undoAction;

    [Header("References")]
    [Tooltip("The UI menu panel to show/hide")]
    public GameObject menuPanel;

    [Tooltip("The transform of the left controller (e.g., XR Controller left)")]
    public Transform leftController;

    private bool menuVisible = true;

    void OnEnable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.started += OnTogglePerformed;
        }

        if (undoAction != null)
        {
            undoAction.action.Enable();
            undoAction.action.started += OnUndoPerformed;
        }
    }

    void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.started -= OnTogglePerformed;
            toggleAction.action.Disable();
        }

        if (undoAction != null)
        {
            undoAction.action.started -= OnUndoPerformed;
            undoAction.action.Disable();
        }
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        menuVisible = !menuPanel.activeSelf;
        menuPanel.SetActive(menuVisible);
    }

    private void OnUndoPerformed(InputAction.CallbackContext ctx)
    {
        UndoLastAction();
    }

    private void UndoLastAction()
    {
        Debug.Log("Undo last action triggered by Button A on Right Controller");
        // TODO: Insert your undo logic here
    }

    void Update()
    {
        if (menuVisible && leftController != null)
        {
            // Follow the left controller's position
            menuPanel.transform.position = leftController.position
                                           + leftController.forward * 0.15f
                                           + leftController.up * 0.07f;

            menuPanel.transform.rotation = Quaternion.LookRotation(leftController.forward);
        }
    }
}
