using UnityEngine;
using System.Collections.Generic;


public class NearbyObjectSearch : MonoBehaviour //Using boundsoctree to search for objects nearby a given area or another object
{
    public BoundsOctreeManager boundsOctreeManager;  
    public GameObject obj; // The loc or object in the scene around which we search
    public float searchRadius = 0.5f; // The radius around the obj to search for cubes

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.U)) FindObjsNearby();
    }

    void FindObjsNearby()
    {
        // Get the position of the cylinder
        Vector3 objPosition = obj.transform.position;

        // Define a bounding box around the cylinder to search within
        Bounds searchArea = new Bounds(objPosition, new Vector3(searchRadius * 2, searchRadius * 2, searchRadius * 2));

        // List to store objects colliding with the search area
        List<GameObject> nearbyObjects = new List<GameObject>();

        // Get objects that intersect with the defined bounds
        boundsOctreeManager.GetColliding(nearbyObjects, searchArea);

        if (nearbyObjects.Count == 0) Debug.Log("Nothing found");

        foreach (var item in nearbyObjects)
        {
            Debug.Log(item.name);
        }

        // Loop through the nearby objects and check if any are cubes
        foreach (GameObject obj in nearbyObjects)
        {
            // For simplicity, assume we are looking for objects tagged as "Cube"
            if (obj.name == "Capsule (1)")
            {
                Debug.Log("Found a capsule near the given object: " + obj.name);
                // You can now interact with or manipulate the cube
            }
        }
    }
}
