using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;  // Add this namespace for ToList()

public class PointsOctreeManager : MonoBehaviour
{
    private PointOctree<GameObject> octree;
    public float worldSize = 50f; // Adjust based on your environment

    void Start()
    {
        octree = new PointOctree<GameObject>(worldSize, new Vector3(0, 3.7f, -10), 1f);

        // Convert the array returned by FindGameObjectsWithTag to a List
        List<GameObject> objects = GameObject.FindGameObjectsWithTag("Selectable").ToList();

        // Add each object to the octree
        foreach (GameObject obj in objects)
        {
            AddObjectToOctree(obj);
        }
    }

    public void AddObjectToOctree(GameObject obj)
    {
        octree.Add(obj, obj.transform.position);
    }

    public List<GameObject> GetNearbyObjects(Vector3 point, float radius)
    {
        return octree.GetNearby(point, radius).ToList();
    }

    void OnDrawGizmos()
    {
        // boundsTree.DrawAllBounds(); // Draw node boundaries
        // boundsTree.DrawAllObjects(); // Draw object boundaries
        // boundsTree.DrawCollisionChecks(); // Draw the last *numCollisionsToSave* collision check boundaries

        if (octree != null)
        {
            octree.DrawAllBounds(); // Draw node boundaries
            octree.DrawAllObjects(); // Mark object positions
        }
    }
}
