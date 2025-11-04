// Based on 5GT guideline interface 
// https://gitlab.fit.fraunhofer.de/coop/project/5g-industrie-stadtpark/ap3-mixed-reality/unity-industriestadtpark/-/blob/main/Assets/Scripts/Guidelines/Utilities/CurveGenerator.cs?ref_type=heads

using AYellowpaper.SerializedCollections;
using EditorAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Some parts of this code, specifically bezier curve generation part is created by ChatGPT.
public class CurveGenerator : MonoBehaviour
{
    [Tooltip("The start point of the curve")]
    public Transform anchorPoint;

    [Tooltip("Targets we want to draw this line to.")]
    [SerializeField, DisableInPlayMode] private List<Transform> targetPoints = new List<Transform>();

    [Tooltip("For each target, line will fit into given waypoints. Note that each part of the line uses same segmentCount")]
    [SerializeField, DisableInPlayMode] SerializedDictionary <Transform, List<Transform>> waypoints = new SerializedDictionary<Transform, List<Transform>>();

    [Tooltip("How really curvy the curve is")]
    public int segmentCount = 50;

    [Tooltip("If true, script will only draw one curve, can be controlled with NextTarget and PreviousTarget functions")]
    public bool singleCurveMode = false;

    public Material lineRendererMaterial;

    private Dictionary<Transform, LineRenderer> lineRenderers = new Dictionary<Transform, LineRenderer>();
    /// <summary>
    /// Our parts might be animating, this dictionary keeps track of their original positions
    /// so we can show the real world object consistently 
    /// </summary>
    private Dictionary<Transform, Vector3> targetPositions = new Dictionary<Transform, Vector3>();
    private Vector3 anchorLatestPosition = Vector3.zero;

    private bool targetsChanged = false;

    private int targetIndex = 0;

    #region Unity Lifecycle
    private void OnDisable()
    {
        foreach (LineRenderer lineRenderer in lineRenderers.Values)
        {
            if (lineRenderer != null) { lineRenderer.enabled = false; }
        }
    }

    private void OnEnable()
    {
        foreach (LineRenderer lineRenderer in lineRenderers.Values)
        {
            lineRenderer.enabled = true;
        }
    }

    private void Awake()
    {
        if (targetPoints.Count > 0 && targetPositions.Count == 0)
        {
            SetTargets(new List<Transform>(targetPoints), new Dictionary<Transform, List<Transform>>(waypoints));
        }
    }

    private void Update()
    {
        if (targetsChanged || anchorLatestPosition != anchorPoint.position)
        {
            DrawCurves();
            anchorLatestPosition = anchorPoint.position;
            targetsChanged = false;
        }
        // Updates the offset of the texture to make it look like it's moving
        foreach (LineRenderer lineRenderer in lineRenderers.Values)
        {
            lineRenderer.material.mainTextureOffset -= new Vector2(0.4f, 0) * Time.deltaTime;
            if (lineRenderer.material.mainTextureOffset.x < -1080)
            {
                lineRenderer.material.mainTextureOffset = new Vector2(0, 0);
            }
        }
    }

    #endregion
    #region Public interface

    public void SetTargets(List<Transform> targets, Dictionary<Transform, List<Transform>> waypoints = null)
    {
        foreach (LineRenderer lineRenderer in lineRenderers.Values)
        {
            Destroy(lineRenderer.gameObject);
        }
        lineRenderers.Clear();
        targetPoints.Clear();
        targetPositions.Clear();
        this.waypoints.Clear();

        foreach (Transform target in targets)
        {
            targetPositions.Add(target, target.position);
            if (waypoints != null && waypoints.ContainsKey(target))
            {
                this.waypoints.Add(target, waypoints[target]);
                foreach (Transform waypoint in waypoints[target])
                {
                    targetPositions.Add(waypoint, waypoint.position);
                }
            }
        }

        targetPoints = targets;
        targetsChanged = true;
        targetIndex = 0;
    }

    public void SetSingleTarget(Transform target, List<Transform> waypoints = null)
    {
        SetTargets(new List<Transform> { target }, waypoints != null ? new Dictionary<Transform, List<Transform>> { { target, waypoints } } : null);
        //DrawCurves();
    }


    [ContextMenu("NextTarget")]
    public void NextTarget()
    {
        SetTargetIndex(targetIndex + 1);
    }

    [ContextMenu("PreviousTarget")]
    public void PreviousTarget()
    {
        SetTargetIndex(targetIndex - 1);
    }

    public void SetTargetIndex(int newIndex)
    {
        if (targetIndex < 0 || targetIndex >= targetPoints.Count) { return; }

        RemoveCurveForTarget(targetPoints[targetIndex]);
        targetIndex = newIndex;
        targetsChanged = true;
    }

    #endregion
    #region Drawing

    private void DrawCurves()
    {
        if (singleCurveMode)
        {
            DrawCurveToTarget(targetPoints[targetIndex]);
        }
        else
        {
            foreach (Transform target in targetPoints)
            {
                DrawCurveToTarget(target);
            }
        }
    }

    private void DrawCurveToTarget(Transform target)
    {
        if (waypoints.ContainsKey(target))
        {
            var targetWaypoints = waypoints[target];
            for (var i = -1; i < targetWaypoints.Count; i++)
            {
                DrawCurveToTarget(i == -1 ? anchorPoint : targetWaypoints[i], i == targetWaypoints.Count - 1 ? target : targetWaypoints[i +1 ]);
            }
        } else
        {
            DrawCurveToTarget (anchorPoint, target);
        }
    }

    private void DrawCurveToTarget(Transform source, Transform target)
    {
        if (!lineRenderers.ContainsKey(target))
        {
            var newLine = new GameObject("LineRenderer to " + target.name);
            newLine.transform.SetParent(anchorPoint);
            LineRenderer lineRenderer = newLine.AddComponent<LineRenderer>();
            lineRenderer.material = lineRendererMaterial;
            lineRenderer.widthCurve = new AnimationCurve(new Keyframe(0, 0.005f), new Keyframe(1, 0.005f));
            lineRenderers.Add(target, lineRenderer);
        }
        DrawCurve(source.position, targetPositions[target], lineRenderers[target]);
    }


    private void DrawCurve(Vector3 startPoint, Vector3 endPoint, LineRenderer lineRenderer)
    {
        // We dynamically adjust the curvature based on height difference for nicer curve

        float heightDifference = Mathf.Abs(endPoint.y - startPoint.y);
        Vector3 directionToEnd = (endPoint - startPoint).normalized;
        Vector3 controlPointOffset = Quaternion.Euler(0, 0, 90) * directionToEnd * heightDifference;

        Vector3 controlPoint1 = startPoint + controlPointOffset;
        Vector3 controlPoint2 = endPoint - controlPointOffset;

        lineRenderer.positionCount = segmentCount;

        // This generates the curve
        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 position = CalculateCubicBezierPoint(t, startPoint, controlPoint1, controlPoint2, endPoint);
            lineRenderer.SetPosition(i, position);
        }
    }

    // ChatGPT wrote this
    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0; //first term
        p += 3 * uu * t * p1; //second term
        p += 3 * u * tt * p2; //third term
        p += ttt * p3; //fourth term

        return p;
    }

    private void RemoveCurveForTarget(Transform target)
    {
        var lineRenderer = lineRenderers[target];
        lineRenderers.Remove(target);
        Destroy(lineRenderer.gameObject);
    }

    #endregion

}
