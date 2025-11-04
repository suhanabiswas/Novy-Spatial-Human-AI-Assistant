using UnityEngine;

public class SelectionVisualization : MonoBehaviour
{
    public GameObject circlePrefab; // Reference to your circle prefab
    public GameObject circleInstance;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main; // Get the main camera

        if (circlePrefab != null)
        {
            circleInstance = Instantiate(circlePrefab);
            circleInstance.SetActive(false); // Initially inactive
        }
        else
        {
            Debug.LogError("Circle prefab is not assigned.");
        }
    }

    public void VisualizeSelectedObject(Collider selectedObject)
    {
        if (circleInstance == null || mainCamera == null) return;

        if (selectedObject != null)
        {
            Vector3 intersectionPoint = selectedObject.bounds.center;
            circleInstance.SetActive(true);
            circleInstance.transform.position = intersectionPoint;
            circleInstance.name = selectedObject.name;

            // Make the circle face the camera
            circleInstance.transform.LookAt(mainCamera.transform);
            circleInstance.transform.Rotate(0, 180, 0); // Adjust rotation to face the camera correctly
        }
        else
        {
            circleInstance.SetActive(false);
        }
    }

    public void HighlightObject(bool highlight)
    {
        var renderer = circleInstance.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = highlight ? Color.yellow : Color.white;
        }
    }

}