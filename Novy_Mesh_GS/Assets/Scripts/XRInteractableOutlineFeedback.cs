using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;


[RequireComponent(typeof(XRBaseInteractable))]
public class XRInteractableOutlineFeedback : MonoBehaviour
{
    public bool isSurface = false;
}


/*[Header("Outline Settings")]
public Color hoverColor = Color.white;
public Color selectColor = Color.green;
public float outlineWidth = 10f;

[Header("Surface Marker Settings")]

public string markerPrefabPath = "Prefabs/SelectionMarker"; // Resources path
private GameObject markerInstance;
private GameObject markerPrefab;

private Outline outline;
private XRBaseInteractable interactable;
private bool isSelected = false;

[Header("Auto-Assigned References")]
public QueryInputHandler queryHandler;
public LLMResponseHandler responseHandler;

private Collider cachedCollider;
private Coroutine surfaceHoverCoroutine;
private Vector3 lastSurfaceHoverPoint;
private bool surfaceSelected = false;

private Coroutine hoverCoroutine;*/

/*
private void Awake()
{
    interactable = GetComponent<XRBaseInteractable>();
    cachedCollider = GetComponent<Collider>();

    interactable.hoverEntered.AddListener(OnHoverEntered);
    interactable.hoverExited.AddListener(OnHoverExited);
    interactable.selectEntered.AddListener(OnSelectEntered);
    //interactable.selectExited.AddListener(OnSelectExited);

    // Auto-assign handlers from "3DUI"
    GameObject uiRoot = GameObject.Find("3DUI");
    if (uiRoot != null)
    {
        queryHandler = uiRoot.GetComponent<QueryInputHandler>();
        responseHandler = uiRoot.GetComponent<LLMResponseHandler>();
    }

    // Load marker prefab
    markerPrefab = Resources.Load<GameObject>(markerPrefabPath);
    if (markerPrefab == null)
        Debug.LogWarning("Marker prefab not found at path: Resources/" + markerPrefabPath);
}

private void OnDestroy()
{
    interactable.hoverEntered.RemoveListener(OnHoverEntered);
    interactable.hoverExited.RemoveListener(OnHoverExited);
    interactable.selectEntered.RemoveListener(OnSelectEntered);
    //interactable.selectExited.RemoveListener(OnSelectExited);
}

private void OnHoverEntered(HoverEnterEventArgs args)
{
    if (isSurface)
    {
        // Cancel any previous coroutine just in case
        if (surfaceHoverCoroutine != null)
            StopCoroutine(surfaceHoverCoroutine);

        surfaceHoverCoroutine = StartCoroutine(DelayedSurfaceRaycastRoutine(args.interactorObject));
        return;
    }

    hoverCoroutine = StartCoroutine(HoverSelectionRoutine());
}

private void OnHoverExited(HoverExitEventArgs args)
{
    if (isSurface)
    {
        if (surfaceHoverCoroutine != null)
        {
            StopCoroutine(surfaceHoverCoroutine);
            surfaceHoverCoroutine = null;
        }

        if (markerInstance != null)
        {
            Destroy(markerInstance);
            markerInstance = null;
        }

        surfaceSelected = false;
        return;
    }

    if (hoverCoroutine != null)
        StopCoroutine(hoverCoroutine);

    hoverCoroutine = null;
    isSelected = false;
    RemoveOutline();
}

private IEnumerator DelayedSurfaceRaycastRoutine(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
{
    float delay = 1f;
    float elapsed = 0f;

    while (elapsed < delay)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }

    if (TryGetInteractorHit(interactor, out Vector3 hitPoint, out Vector3 normal))
    {
        StartSurfaceHoverRoutine(hitPoint, normal);
    }

    surfaceHoverCoroutine = null; // Mark coroutine as finished
}


private void StartSurfaceHoverRoutine(Vector3 point, Vector3 normal)
{
    if (surfaceHoverCoroutine != null)
    {
        // Avoid restarting if within radius
        if (Vector3.Distance(point, lastSurfaceHoverPoint) < 0.02f)
            return;

        StopCoroutine(surfaceHoverCoroutine);
    }

    lastSurfaceHoverPoint = point;
    surfaceHoverCoroutine = StartCoroutine(SurfaceHoverCountdown(point, normal));
}

private IEnumerator SurfaceHoverCountdown(Vector3 point, Vector3 normal)
{
    ShowMarker(point, normal, hoverColor); // Show initial marker

    float countdown = 2f;
    float elapsed = 0f;

    while (elapsed < countdown)
    {
        elapsed += Time.deltaTime;
        yield return null;

        // Optional: You could add visual feedback here, e.g. scale or fade ring
    }

    SetMarkerColor(selectColor);
    surfaceSelected = true;

    float[] pointArray = new float[] { point.x, point.y, point.z };
    if (queryHandler != null)
        queryHandler.pointedTargetPos = pointArray;
    if (responseHandler != null)
        responseHandler.pointedTargetPos = pointArray;

    // Note: Marker will remain only if surface hover remains uninterrupted
}


private void SetMarkerColor(Color color)
{
    if (markerInstance == null) return;

    SpriteRenderer sr = markerInstance.GetComponent<SpriteRenderer>();
    if (sr != null)
        sr.color = color;
    else
    {
        Renderer rend = markerInstance.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = color;
    }
}

private IEnumerator HoverSelectionRoutine() // for objects
{
    AddOutline(hoverColor); // Start with white outline

    float countdown = 2f;
    while (countdown > 0f)
    {
        yield return new WaitForSeconds(1f);
        countdown--;

        // Optional: debug log for countdown
        Debug.Log($"Hover selection countdown: {countdown + 1}");
    }

    // Countdown complete, mark as selected
    SetOutlineColor(selectColor);
    isSelected = true;

    if (queryHandler != null)
        queryHandler.pointedTargetObject = gameObject.name;
    if (responseHandler != null)
        responseHandler.pointedTargetObject = gameObject.name;

    // Keep green outline until hover ends
}

private void OnSelectEntered(SelectEnterEventArgs args)
{
    if (!isSurface)
      {
          isSelected = true;
          SetOutlineColor(selectColor);
      }


      // Only assign pointedTargetObject if NOT a surface
      if (!isSurface)
      {
          if (queryHandler != null)
              queryHandler.pointedTargetObject = gameObject.name;
          if (responseHandler != null)
              responseHandler.pointedTargetObject = gameObject.name;
      }

      if (isSurface && TryGetInteractorHit(args.interactorObject, out Vector3 point, out Vector3 normal))
      {
          ShowMarker(point, normal, selectColor);
          Debug.Log("Surface target selected at: " + point);

          float[] pointArray = new float[] { point.x, point.y, point.z };
          queryHandler.pointedTargetPos = pointArray;
          responseHandler.pointedTargetPos = pointArray;
      }
}

private void OnSelectExited(SelectExitEventArgs args)
 {
     if (!isSurface)
     {
         isSelected = false;
         RemoveOutline();
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

/// <summary>
/// Gets the latest raycast hit point and normal from the interactor (must be XRRayInteractor).
/// </summary>
private bool TryGetInteractorHit(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor, out Vector3 hitPoint, out Vector3 hitNormal)
{
    hitPoint = Vector3.zero;
    hitNormal = Vector3.up;

    if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRRayProvider rayProvider)
    {
        Transform rayOrigin = rayProvider.GetOrCreateRayOrigin();
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

        // Raycast manually to get normal
        if (Physics.Raycast(ray, out RaycastHit hit, 15f))
        {
            if (hit.collider == cachedCollider)
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }
        }

        // Fallback: if rayProvider's rayEndTransform matches this object, use rayEndPoint but normal unknown
        if (rayProvider.rayEndTransform == this.transform)
        {
            hitPoint = rayProvider.rayEndPoint;
            // Can't get normal from rayEndPoint directly, so fallback to up vector or zero
            hitNormal = Vector3.up;
            return true;
        }
    }

    return false;
}

public void ShowMarker(Vector3 position, Vector3 normal, Color color)
{
    if (markerPrefab == null)
        return;

    if (markerInstance == null)
        markerInstance = Instantiate(markerPrefab);

    markerInstance.GetComponent<Renderer>().material.renderQueue = 3100;

    markerInstance.transform.position = position + normal * 0.005f;
    markerInstance.transform.rotation = Quaternion.LookRotation(normal);
    markerInstance.transform.localScale = Vector3.one * 0.1f;

    SpriteRenderer sr = markerInstance.GetComponent<SpriteRenderer>();
    if (sr != null)
        sr.color = color;
    else
    {
        Renderer rend = markerInstance.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = color;
    }

    markerInstance.SetActive(true);
}
}*/