using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;

public class PinchSelection : MonoBehaviour
{
    public SelectionVisualization visualization; // Reference to the visualization script
    public XRHandTrackingEvents handTrackingEvents; // Reference to XR Hand Tracking Events

    public QueryInputHandler queryHandler;
    public LLMResponseHandler responseHandler;

    private Coroutine reenableConeCoroutine;

    private bool isPinching = false;
    private const float pinchThreshold = 0.015f; // 1.5 cm in world units (adjust if needed)

    void Start()
    {
        if (visualization == null)
        {
            visualization = GetComponent<SelectionVisualization>();
            if (visualization == null)
            {
                Debug.LogWarning("SelectionVisualization not assigned and not found on this GameObject.");
            }
        }

        if (handTrackingEvents != null)
        {
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
        }
        else
        {
            Debug.LogWarning("XRHandTrackingEvents reference not set.");
        }
    }

    void OnDisable()
    {
        if (handTrackingEvents != null)
        {
            handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
        }
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
    {
        var hand = args.hand;
        var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (!thumbTip.TryGetPose(out var thumbPose) || !indexTip.TryGetPose(out var indexPose))
            return;

        float distance = Vector3.Distance(thumbPose.position, indexPose.position);

        if (distance < pinchThreshold)
        {
            if (!isPinching)
            {
                isPinching = true;
                Debug.Log($"Pinch START detected with {hand.handedness} hand on " + visualization.circleInstance.name);

                visualization?.HighlightObject(true);
                queryHandler.pointedTargetObject = visualization.circleInstance.name;
                responseHandler.pointedTargetObject = visualization.circleInstance.name;

                this.enabled = false;

                // Start coroutine to re-enable after 5 seconds
                if (reenableConeCoroutine != null)
                    StopCoroutine(reenableConeCoroutine); // Cancel if already running

                reenableConeCoroutine = StartCoroutine(ReenableConeCastingAfterDelay(5f));
            }
        }
        else
        {
            if (isPinching)
            {
                isPinching = false;
                visualization?.HighlightObject(false);
            }
        }
    }

    private IEnumerator ReenableConeCastingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        this.enabled = false;
        Debug.Log("ConeCasting re-enabled after delay.");
    }

}
