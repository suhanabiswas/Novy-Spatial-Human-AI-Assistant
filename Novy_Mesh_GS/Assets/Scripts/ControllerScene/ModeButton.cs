using UnityEngine;
using UnityEngine.EventSystems;

public class ModeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public InteractionMode assignedMode;

    public void SetMode()
    {
        ModeManager.Instance.SetMode(assignedMode);
        ModeInstructionUIManager.Instance.ShowSelectedInstruction(assignedMode);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //ModeInstructionUIManager.Instance.ShowHoverInstruction(assignedMode);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Optional: clear or keep current instruction
        // ModeInstructionUIManager.Instance.instructionText.text = "";
    }
}
