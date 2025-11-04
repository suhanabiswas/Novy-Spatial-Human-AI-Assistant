using EditorAttributes;
using PrimeTween;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PointerArrow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Which object this arrow should point to")]
    public GameObject objectToPoint;
    [Tooltip("This child will be rotated left and right, different than the parent object")]
    public Transform pointer;

    [Header("Behaviour")]
    [Tooltip("Seconds between position update"), Suffix("seconds")]
    public float updateFrequency = 0.2f;
    [Tooltip("The angle in degrees that the user should look at the object to trigger the userLooking event")]
    public float lookAngleTarget;
    [Tooltip("If true, pointing starts on OnEnable event automatically, otherwise call StartPointing / StopPointing methods to control the behaviour. Note that OnDisable always triggers StopPointing and the state is not saved")]
    public bool startOnEnable = true;

    [Tooltip("Event that is triggered when the user is looking at the object")]
    public UnityEvent onUserLooking = new UnityEvent();

    [FoldoutGroup("Advanced Setings", nameof(animationDuration), nameof(horizontalToVerticalControlLimit), nameof(horizontalUpdateLimit), nameof(verticalUpdateLimit), nameof(arrowAnimationEase) )]
    [SerializeField] private Void advancedSettingsGroupHolder;

    [Tooltip("The duration of the animation when the arrow is pointing to the object"), HideProperty, Suffix("seconds")]
    public float animationDuration = 0.5f;
    [Tooltip("The horizontal angle limit before arrow starts to point out the object directly"), HideProperty]
    public float horizontalToVerticalControlLimit = 30;
    [Tooltip("The horizontal angle change before the arrow position is updated"), HideProperty]
    public float horizontalUpdateLimit = 35;
    [Tooltip("The vertical angle change before the arrow position is updated"), HideProperty]
    public float verticalUpdateLimit = 20;
    [Tooltip("Sets the ease function for the animation"), HideProperty]
    public Ease arrowAnimationEase = Ease.InOutSine;

    private Vector3 targetPosition;
    private Vector3 targetRotation;
    private Vector3 visualTargetRotation;
    private float latestHorizontalAngle;
    private float latestVerticalAngle;

    private bool updateArrow = false;

    private Sequence tween;
    private Coroutine updateArrowCoroutine;

    #region Unity Life Cycle
    private void OnDestroy()
    {
        onUserLooking.RemoveAllListeners();
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartPointing();
        }
    }

    private void OnDisable()
    {
        StopPointing();
    }

    #endregion

    #region Public Methods

    public void StartPointing()
    {
        if (!updateArrow)
        {
            latestHorizontalAngle = 1000;
            latestVerticalAngle = 1000;
            updateArrow = true;
            updateArrowCoroutine = StartCoroutine(UpdateArrow());
        }
    }

    public void StopPointing()
    {
        if (updateArrow)
        {
            updateArrow = false;
            if (updateArrowCoroutine != null)
            {
                StopCoroutine(updateArrowCoroutine);
                updateArrowCoroutine = null;
            }
        }
    }

    public void SetObjectToPoint(GameObject target)
    {
        this.objectToPoint = target;
    }

    public void ClearTarget()
    {
        this.objectToPoint = null;
    }

    #endregion

    #region Pointing and Logic

    IEnumerator UpdateArrow()
    {
        while (updateArrow)
        {
            UpdateTargetPositionsAndAnimateArrow();
            yield return new WaitForSeconds(updateFrequency);
        }
    }


    // We check first if the user is looking at the object, then horizontal angle, then vertical angle
    private void UpdateTargetPositionsAndAnimateArrow()
    {
        var position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
        var horizontalAngle = CalculateHorizontalAngle();
        var verticalAngle = CalculateVerticalAngle();

        if (lookAngleTarget > Mathf.Abs(horizontalAngle) && lookAngleTarget > Mathf.Abs(verticalAngle))
        {
            onUserLooking.Invoke();
        }
        else
        {
            // User first needs to look at the general direction of the object
            if (Mathf.Abs(horizontalAngle) > horizontalToVerticalControlLimit)
            {
                // If the change since the last update is not much, no need to update
                if (Mathf.Abs(Mathf.Abs(latestHorizontalAngle) - Mathf.Abs(horizontalAngle)) > horizontalUpdateLimit)
                {
                    targetPosition = position;
                    // Rotate the arrow itself to left or right
                    visualTargetRotation = new Vector3(0, horizontalAngle > 0 ? 90 : 270, 0);
                    targetRotation = Quaternion.LookRotation(Camera.main.transform.position - targetPosition).eulerAngles;
                    AnimateArrow();
                    latestHorizontalAngle = horizontalAngle;
                }
                latestVerticalAngle = 3000;
            }
            else
            {
                // If the change since the last update is not much, no need to update
                if (Mathf.Abs(Mathf.Abs(latestVerticalAngle) - Mathf.Abs(horizontalAngle)) > verticalUpdateLimit)
                {
                    targetPosition = position;
                    visualTargetRotation = Vector3.zero;
                    targetRotation = Quaternion.LookRotation(objectToPoint.transform.position - targetPosition).eulerAngles;

                    AnimateArrow();
                    latestVerticalAngle = verticalAngle;
                }
            }
        }
    }

    private void AnimateArrow()
    {
        if (tween.isAlive)
        {
            tween.Stop();
        }
        tween = Sequence.Create();
        if (Vector3.Distance(targetPosition, transform.position) > 0.1f)
        {
            tween.Group(Tween.Position(transform, endValue: targetPosition, duration: animationDuration, ease: Ease.InOutSine));
        }
        if (Vector3.Distance(visualTargetRotation, pointer.localEulerAngles) > 0.1f)
        {
            tween.Group(Tween.LocalRotation(pointer, endValue: visualTargetRotation, duration: animationDuration, ease: Ease.InOutSine));
        }
        if (Vector3.Distance(targetRotation, transform.eulerAngles) > 0.1f)
        {
            tween.Group(Tween.Rotation(transform, endValue: targetRotation, duration: animationDuration, ease: Ease.InOutSine));
        }
    }

    private float CalculateHorizontalAngle()
    {
        var camera2D = new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z);
        var cameraForward2D = new Vector2(Camera.main.transform.forward.x, Camera.main.transform.forward.z);
        var target2D = new Vector2(objectToPoint.transform.position.x, objectToPoint.transform.position.z);
        var angle = Vector2.SignedAngle(cameraForward2D, target2D - camera2D);
        return angle;
    }

    private float CalculateVerticalAngle()
    {
        var camera2D = new Vector2(Camera.main.transform.position.y, Camera.main.transform.position.z);
        var cameraForward2D = new Vector2(Camera.main.transform.forward.y, Camera.main.transform.forward.z);
        var target2D = new Vector2(objectToPoint.transform.position.y, objectToPoint.transform.position.z);
        var angle = Vector2.SignedAngle(cameraForward2D, target2D - camera2D);
        return angle;
    }

    #endregion

}
