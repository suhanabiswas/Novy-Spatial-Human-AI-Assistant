using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class RaycastSelector : MonoBehaviour
{
    public PointsOctreeManager octreeManager;
    public float maxDistance = 15f;
    public float searchRadius = 5f;
    public float minDotThreshold = 0.8f;

    private GameObject lastSelected;
    private bool wasAPressedLastFrame = false;

    void Update()
    {
        /*InputDevice rightHandDevice = GetRightHandDevice();
        if (rightHandDevice.isValid && rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool isPressed))
        {
            // Trigger when button was pressed before, and now it's released
            if (wasAPressedLastFrame && !isPressed)
            {
                SelectObjectNearRay(); // Button was just released
            }

            // Store current state for next frame comparison
            wasAPressedLastFrame = isPressed;
        }*/

        SelectObjectNearRay();
    }

        InputDevice GetRightHandDevice()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        return devices.Count > 0 ? devices[0] : default;
    }

    void SelectObjectNearRay()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        // Draw the ray in the Scene view
        Debug.DrawRay(origin, direction * maxDistance, Color.green, 0.1f);

        // Get nearby objects using the octree
        List<GameObject> nearbyObjects = octreeManager.GetNearbyObjects(origin, searchRadius);

        if (nearbyObjects.Count > 0)
        {
            foreach (GameObject obj in nearbyObjects)
            {
                Debug.Log("Nearby object: " + obj.name);
            }
        }
        else
        {
            Debug.Log("No nearby objects found.");
        }

        // Find the best-matching object based on direction and distance
        GameObject bestMatch = null;
        float bestDot = -1f;

        foreach (GameObject obj in nearbyObjects)
        {
            Vector3 toObject = (obj.transform.position - origin).normalized;
            float dot = Vector3.Dot(direction, toObject);
            float distance = Vector3.Distance(origin, obj.transform.position);

            if (dot > bestDot && dot >= minDotThreshold && distance <= maxDistance)
            {
                bestDot = dot;
                bestMatch = obj;
            }
        }

        if (bestMatch != null)
        {
            Debug.Log("Selected: " + bestMatch.name);
            HighlightObject(bestMatch);
        }
        else
        {
            Debug.Log("No object selected.");
        }
    }

    void HighlightObject(GameObject obj)
    {
        // Unhighlight previous selection
        if (lastSelected != null && lastSelected != obj)
        {
            Renderer prevRenderer = lastSelected.GetComponent<Renderer>();
            if (prevRenderer != null)
            {
                prevRenderer.material.color = Color.white; // Reset color
            }
        }

        // Highlight the new selection
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.red;
        }

        lastSelected = obj;
    }
}
