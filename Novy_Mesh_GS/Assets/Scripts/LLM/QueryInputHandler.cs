using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QueryInputHandler : MonoBehaviour
{

    public Button submitButton;
    public SpatialExporter exporter;
    public Whisper.Samples.STTManager sTTManager;
    public LLMResponseHandler llmResponseHandler;
    public GameObject userTransform;
    public string pointedTargetObject;
    public string pointedSurfaceObject;
    public float[] pointedTargetPos;

    private GameObject prevTargetObject;
    private string prevTargetObjectName;

    void Start()
    {
        prevTargetObject = null;
    }

    public void OnSubmit(string userQuery)
    {

        var (userPos, userForward, userRight) = GetUserTransform();
        prevTargetObject = llmResponseHandler.previousTargetObject;
        if (prevTargetObject != null) prevTargetObjectName = prevTargetObject.name; 
        else prevTargetObjectName = "Not applicable";
        exporter.SendQuery(userQuery, userPos, userForward, userRight, pointedTargetObject, pointedTargetPos, pointedSurfaceObject, prevTargetObjectName);

        // Send user context to the LLMResponseHandler
        llmResponseHandler.SetUserContext(userPos, userForward, userRight);

        submitButton.gameObject.SetActive(false);

        pointedTargetObject = null;
        pointedTargetPos = null;
        pointedSurfaceObject = null;

        sTTManager.ClearOutput();
    }


    private (float[] position, float[] forward, float[] right) GetUserTransform()
    {
        Vector3 pos = userTransform.transform.position;
        Vector3 forward = userTransform.transform.forward;
        Vector3 right = userTransform.transform.right;

        float[] userPos = new float[] { pos.x, pos.y, pos.z };
        float[] userForward = new float[] { forward.x, forward.y, forward.z };
        float[] userRight = new float[] { right.x, right.y, right.z };

        return (userPos, userForward, userRight);
    }

}
