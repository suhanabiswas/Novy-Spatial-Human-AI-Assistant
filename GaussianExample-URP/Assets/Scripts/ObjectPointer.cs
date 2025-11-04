using EditorAttributes;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ObjectPointer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("This is where we want users to look at first, like an info board")]
    public GameObject source;
    [Tooltip("This is the real target")]
    public GameObject target;

    [Header("Visuals")]
    [Tooltip("If true, requires CurveGenerator component to draw a line between source and target")]
    public bool useCurveVisuals = true;
    [ShowField(nameof(useCurveVisuals)), Tooltip("The curve generator component that will draw the line between source and target")]
    public CurveGenerator curve;

    [Tooltip("If true, will add an outline automatically to the target")]
    public bool outlineTarget = true;

    [Tooltip("If true, requires a GameObject with PointerArrow component to show an arrow pointing to the target")]
    public bool showArrow = true;
    [ShowField(nameof(showArrow)), Tooltip("The arrow that will point the target")]
    public PointerArrow arrow;
    [ShowField(nameof(showArrow)), Tooltip("If true, when PointerArrow sends userLooking event, arrow will be hidden automatically")]
    public bool hideArrowAutomatically = true;

    [Header("Behaviour")]
    [Tooltip("Seconds between controls"), Suffix("seconds")]
    public float updateFrequency = 0.2f;
    [Tooltip("If true, outline and curve won't be shown until user looks at source first. Arrow will point to source. Requires showArrow to be true."), EnableField(nameof(showArrow))]
    public bool firstLookAtSource = false;
    [Tooltip("Angle between camera forward and target to consider user looking at target")]
    public float lookAngleTarget = 20;
    [Tooltip("Delay before firing userLooking event")]
    public float eventFireDelay = 1f;
    [Tooltip("If true, pointing starts on OnEnable event automatically, otherwise call StartPointing / StopPointing methods to control the behaviour. Note that OnDisable always triggers StopPointing and the state is not saved")]
    public bool pointAtOnEnable = true;

    [Header("Events")]
    public UnityEvent userLooking = new UnityEvent();

    private Outline outline;
    private bool checkIfUserLooking = false;
    private bool userSawSource = false;
    private Coroutine userLookingCoroutine;

    #region Unity Life Cycle
    public void OnEnable()
    {
        if (outlineTarget)
        {
            AddOutline();
        }
        if (pointAtOnEnable)
        {
            StartPointing();
        }
    }

    private void OnDisable()
    {
        if (outlineTarget)
        {
            RemoveOutline();
        }
        StopPointing();
    }

    #endregion

    #region Public Methods
    public void StartPointing()
    {
        if (!checkIfUserLooking)
        {
            userSawSource = false;
            checkIfUserLooking = true;
            UpdateVisualsForState();
        }
    }

    public void StopPointing()
    {
        if (checkIfUserLooking)
        {
            if (outlineTarget)
            {
                RemoveOutline();
            }
            if (useCurveVisuals)
            {
                curve.enabled = false;
            }
            if (showArrow)
            {
                arrow.onUserLooking.RemoveListener(DisableArrowOnLookingEvent);
                arrow.gameObject.SetActive(false);
            }
            if (userLookingCoroutine != null)
            {
                StopCoroutine(userLookingCoroutine);
                userLookingCoroutine = null;
            }
            checkIfUserLooking = false;
        }
    }

    public void SetTarget(GameObject target)
    {
        this.target = target;
        AddOutline();
        StartPointing();
    }

    public void ClearTarget()
    {
        this.target = null;
        RemoveOutline();
        StopPointing();
    }


    #endregion

    private void AddOutline()
    {
        outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
            outline.OutlineColor = Color.white;
            outline.OutlineWidth = 10;
            outline.OutlineMode = Outline.Mode.OutlineAll;
        }
    }

    private void RemoveOutline()
    {
        if (outline != null)
        {
            Destroy(outline);
        }
    }

    private void DisableArrowOnLookingEvent()
    {
        arrow.gameObject.SetActive(false);
    }

    private void OnUserSawSource()
    {
        userSawSource = true;
        UpdateVisualsForState();
    }

    private void UpdateVisualsForState()
    {
        if (!firstLookAtSource || (firstLookAtSource && userSawSource))
        {
            if (outline) { outline.enabled = true; }
            if (curve) { curve.enabled = true; }
            arrow.objectToPoint = target;
            arrow.gameObject.SetActive(true);
            if (firstLookAtSource) { arrow.onUserLooking.RemoveListener(OnUserSawSource); }
            
            arrow.onUserLooking.AddListener(DisableArrowOnLookingEvent);
            arrow.StartPointing();

            userLookingCoroutine = StartCoroutine(CheckIfUserLooking());
        }
        else
        {
            if (outline) { outline.enabled = false; }
            if (curve) { curve.enabled = false; }
            arrow.objectToPoint = source;
            arrow.gameObject.SetActive(true);
            arrow.onUserLooking.AddListener(OnUserSawSource);
            arrow.StartPointing();
        }
    }

    private IEnumerator CheckIfUserLooking()
    {
        while (checkIfUserLooking)
        {
            if (CheckIfUserSawTarget())
            {
                yield return new WaitForSeconds(eventFireDelay);
                StopPointing();
                userLooking.Invoke();
            }
            yield return new WaitForSeconds(updateFrequency);
        }
    }

    private bool CheckIfUserSawTarget()
    {
        Vector3 targetPosition = target.transform.position;
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraToTarget = targetPosition - cameraPosition;
        float angleToTarget = Vector3.Angle(cameraForward, cameraToTarget);
        return angleToTarget < lookAngleTarget;
    }
}
