using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BoundsOctreeManager : MonoBehaviour
{
    private BoundsOctree<GameObject> boundsTree;
    private Dictionary<GameObject, Vector3> trackedObjects = new();

    public float worldSize = 0.5f;
    public float minNodeSize = 0.01f; 
    public float loosenessVal = 1.2f;

    public GameObject newObj;
    public GameObject deletedObj;

    void Start()
    {
        boundsTree = new BoundsOctree<GameObject>(worldSize, Vector3.zero, minNodeSize, loosenessVal);

        List<GameObject> objects = GameObject.FindGameObjectsWithTag("Selectable").ToList(); //Make it interactable tag for final user tests

        foreach (GameObject obj in objects)
        {
            AddObjectToBoundsTree(obj);
            //TrackObject(obj);
        }
    }

    public void AddObjectToBoundsTree(GameObject obj)
    {
        boundsTree.Add(obj, new Bounds(obj.transform.position, GetObjectBoundsSize(obj)));
    }

    public void RemoveObjectFromBoundsTree(GameObject obj)
    {
        boundsTree.Remove(obj);
        trackedObjects.Remove(obj);
    }

    public void TrackObject(GameObject obj)
    {
        if (!trackedObjects.ContainsKey(obj))
        {
            trackedObjects.Add(obj, obj.transform.position);
        }
    }

    public void MoveObject(GameObject obj) // For my use case: when user's task of moving object is finished, this is then called
    {
        if (boundsTree == null) return;

        if (boundsTree.Remove(obj))
        {
            boundsTree.Add(obj, new Bounds(obj.transform.position, GetObjectBoundsSize(obj)));
        }
        else
        {
            boundsTree.Add(obj, new Bounds(obj.transform.position, GetObjectBoundsSize(obj)));
        }

        trackedObjects[obj] = obj.transform.position;
    }

    void Update()
    {
      /*foreach (var kvp in trackedObjects)
        {
            GameObject obj = kvp.Key;
            Vector3 lastPos = kvp.Value;
            if (obj != null && obj.transform.position != lastPos)
            {
                MoveObject(obj);
            }
        } */

        if (Input.GetKeyDown(KeyCode.N)) AddObjectToBoundsTree(newObj);
        if (Input.GetKeyDown(KeyCode.M)) RemoveObjectFromBoundsTree(deletedObj);
    }

    private Vector3 GetObjectBoundsSize(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.size;
        }
        else
        {
            return Vector3.one;
        }
    }

    public void GetColliding(List<GameObject> collidingWith, Bounds checkBounds)
    {
        if (boundsTree == null)
        {
            Debug.LogError("BoundsOctree is not initialized!");
            return;
        }

        if (checkBounds.size == Vector3.zero)
        {
            Debug.LogWarning("Invalid bounds size.");
            return;
        }

        collidingWith.Clear();
        boundsTree.GetColliding(collidingWith, checkBounds);

    }


    void OnDrawGizmos()
    {
        if (boundsTree != null)
        {
            boundsTree.DrawAllBounds();
            boundsTree.DrawAllObjects();
        }
    }
}
