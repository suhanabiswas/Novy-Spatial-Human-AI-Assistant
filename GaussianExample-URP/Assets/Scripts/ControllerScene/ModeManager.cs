using UnityEngine;

public class ModeManager : MonoBehaviour
{
    public static ModeManager Instance { get; private set; }
    public InteractionMode CurrentMode { get; private set; } = InteractionMode.None;

    // Add this event
    public delegate void ModeChangedDelegate(InteractionMode newMode);
    public event ModeChangedDelegate OnModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        SetMode(InteractionMode.Manipulate);
    }

    public void SetMode(InteractionMode newMode)
    {
        if (CurrentMode == newMode) return;

        CurrentMode = newMode;
        Debug.Log("Interaction Mode changed to: " + newMode);

        // Notify subscribers
        OnModeChanged?.Invoke(newMode);

        // Also explicitly update all interactables
        ModeBasedInteractable.UpdateAllInteractables(newMode);
    }

    public string GetCurrentModeAsString()
    {
        return CurrentMode.ToString().ToLower();
    }

    public InteractionMode GetCurrentMode()
    {
        return CurrentMode;
    }

}
