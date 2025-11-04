using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PointingEventLogger : MonoBehaviour
{
    [Serializable]
    public class HoverObjectRecord
    {
        public string objectName;
        public GameObject selectedObject;
        public float timestamp;
    }

    [Serializable]
    public class HoverSurfaceRecord
    {
        public Vector3 position;
        public Vector3 normal;
        public float timestamp;
        public GameObject surfaceObject;
    }

    [Header("Raycasting Setup")]
    public MonoBehaviour interactorSource; // Should implement IXRRayProvider
    public float rayLength = 20f;
    public float pollInterval = 0.2f;

    [Header("Feedback Settings")]
    public GameObject markerPrefab;
    public float markerOffset = 0.005f;
    public float markerScale = 0.1f;
    public Color surfaceColor = Color.green;
    public Color objectColor = Color.yellow;

    private GameObject markerInstance;
    private Outline currentOutline;
    public float outlineWidth = 3f;

    private bool isTracking = false;
    private float trackingStartTime;
    private Coroutine trackingCoroutine;

    private List<HoverObjectRecord> objectLogs = new();
    private List<HoverSurfaceRecord> surfaceLogs = new();

    public IReadOnlyList<HoverObjectRecord> ObjectLogs => objectLogs;
    public IReadOnlyList<HoverSurfaceRecord> SurfaceLogs => surfaceLogs;

    public void StartRecordingTracking()
    {
        objectLogs.Clear();
        surfaceLogs.Clear();
        isTracking = true;
        trackingStartTime = Time.time;
        trackingCoroutine = StartCoroutine(LogHoverCoroutine());
    }

    public void StopRecordingTracking()
    {
        isTracking = false;
        if (trackingCoroutine != null)
            StopCoroutine(trackingCoroutine);

        HideMarker();
        ClearAllOutlines();
    }

    private IEnumerator LogHoverCoroutine()
    {
        while (isTracking)
        {
            if (interactorSource is IXRRayProvider rayProvider)
            {
                Transform rayOrigin = rayProvider.GetOrCreateRayOrigin();
                Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

                if (Physics.Raycast(ray, out RaycastHit hit, rayLength))
                {
                    GameObject hitObject = hit.collider?.gameObject;

                    if (hitObject != null && hitObject.CompareTag("SpatialObject"))
                    {
                        var feedback = hitObject.GetComponent<XRInteractableOutlineFeedback>();
                        float timestamp = Time.time - trackingStartTime;

                        if (feedback != null)
                        {
                            if (feedback.isSurface)
                            {
                                surfaceLogs.Add(new HoverSurfaceRecord
                                {
                                    position = hit.point,
                                    normal = hit.normal,
                                    timestamp = timestamp,
                                    surfaceObject = hitObject
                                });

                                ShowMarker(hit.point, hit.normal, surfaceColor);
                                //Debug.Log($"[Surface Log] Position: {hit.point}, Timestamp: {timestamp}");
                                ClearAllOutlines();
                            }
                            else
                            {
                                objectLogs.Add(new HoverObjectRecord
                                {
                                    objectName = hitObject.name,
                                    selectedObject = hitObject,
                                    timestamp = timestamp
                                });

                                ShowOutline(hitObject, objectColor);
                                HideMarker();
                                Debug.Log($"[Object Log] Name: {hitObject.name}, Timestamp: {timestamp}");
                            }
                        }
                    }
                    else
                    {
                        HideMarker();
                        ClearAllOutlines();
                    }
                }
                else
                {
                    HideMarker();
                    ClearAllOutlines();
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    public void ShowMarker(Vector3 position, Vector3 normal, Color color)
    {
        if (markerPrefab == null) return;

        if (markerInstance == null)
            markerInstance = Instantiate(markerPrefab);

        markerInstance.GetComponent<Renderer>().material.renderQueue = 3100;

        markerInstance.transform.position = position + normal * markerOffset;
        markerInstance.transform.rotation = Quaternion.LookRotation(normal);
        markerInstance.transform.localScale = Vector3.one * markerScale;

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

    public void HideMarker()
    {
        if (markerInstance != null)
            markerInstance.SetActive(false);
    }

    public void ShowOutline(GameObject hitObject, Color color)
    {
        Outline outline = hitObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = hitObject.AddComponent<Outline>();
        }

        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = color;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;

        // Store reference to current outline so we can disable it later
        currentOutline = outline;
    }

    public void ClearCurrentOutline()
    {
        if (currentOutline != null)
        {
            currentOutline.enabled = false;
            currentOutline = null;
        }
    }

    public void ClearOutline(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null) return;

        Outline outline = target.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        if (currentOutline == outline)
            currentOutline = null;
    }

    public void ClearAllOutlines()
    {
        Outline[] allOutlines = FindObjectsOfType<Outline>();

        foreach (var outline in allOutlines)
        {
            outline.enabled = false;
        }

        currentOutline = null;
    }


}
