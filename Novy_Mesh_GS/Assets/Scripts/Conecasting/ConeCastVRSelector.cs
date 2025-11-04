using UnityEngine;
using System.Collections.Generic;

public class ConeCastVRSelector : MonoBehaviour
{
    public float detectionDistance = 5f;
    public float coneAngle = 10f;
    public LayerMask detectionMask;

    private Collider[] lastDetectedObjects;
    private Collider mostPresentObject;
    private Collider previousMostPresentObject;

    public delegate void ObjectSelectedHandler(Collider selectedObject);
    public event ObjectSelectedHandler OnObjectSelected;

    void Start()
    {
        SelectionVisualization visualization = GetComponent<SelectionVisualization>();
        if (visualization != null)
        {
            OnObjectSelected += visualization.VisualizeSelectedObject;
        }
    }

    void Update()
    {
        InitiateConeCasting();
    }

    private void InitiateConeCasting()
    {
        lastDetectedObjects = ConeCastUtility.FindConeColliders(
            transform.position, transform.forward, coneAngle, detectionDistance, detectionMask
        );

        if (lastDetectedObjects.Length > 0)
        {
            FindObjectWithMostPresence();
            if (mostPresentObject != previousMostPresentObject)
            {
                OnObjectSelected?.Invoke(mostPresentObject);
                previousMostPresentObject = mostPresentObject;
            }
        }
        else
        {
            if (mostPresentObject != null)
            {
                mostPresentObject = null;
                //OnObjectSelected?.Invoke(null);
            }
        }
    }

    void FindObjectWithMostPresence()
    {
        float bestScore = 0f;
        Collider newMostPresentObject = lastDetectedObjects[0];

        foreach (var obj in lastDetectedObjects)
        {
            float score = ComputePresenceScore(obj);
            if (score > bestScore)
            {
                bestScore = score;
                newMostPresentObject = obj;
            }
        }

        mostPresentObject = newMostPresentObject;
    }

    float ComputePresenceScore(Collider obj)
    {
        Vector3 toObj = obj.bounds.center - transform.position;
        float distance = toObj.magnitude;
        if (distance > detectionDistance) return 0;

        float angle = Vector3.Angle(transform.forward, toObj.normalized);
        if (angle > coneAngle) return 0;

        float angleScore = 1f - (angle / coneAngle);
        float sizeScore = obj.bounds.extents.magnitude;
        float distanceScore = 1f / (1f + distance);

        return angleScore * sizeScore * distanceScore;
    }
}