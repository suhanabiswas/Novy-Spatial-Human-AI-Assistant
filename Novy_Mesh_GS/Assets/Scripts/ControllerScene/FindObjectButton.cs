using UnityEngine;
using UnityEngine.UI;

public class FindObjectButton : MonoBehaviour
{
    public GameObject targetObject;

    private Button button;

    //public DashboardControllerConnection dashboardConnection;

    private ObjectPointer objectPointerScript;
    private PointerArrow pointerArrowObject;
    private CurveGenerator curveGenerator;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        //dashboardConnection = FindObjectOfType<DashboardControllerConnection>();

        // Find all components in the scene
        objectPointerScript = FindObjectOfType<ObjectPointer>();
        pointerArrowObject = FindObjectOfType<PointerArrow>();
        curveGenerator = FindObjectOfType<CurveGenerator>();
    }

    void OnClick()
    {
        if (targetObject == null) return;

        // Set pointers and curve targets
        if (objectPointerScript != null)
            objectPointerScript.SetTarget(targetObject);

        if (pointerArrowObject != null)
            pointerArrowObject.SetObjectToPoint(targetObject);

        if (curveGenerator != null)
            curveGenerator.SetSingleTarget(targetObject.transform, null);
        
       // dashboardConnection?.LogToDashboard($"Finding started on {targetObject.name}");
    }
}
