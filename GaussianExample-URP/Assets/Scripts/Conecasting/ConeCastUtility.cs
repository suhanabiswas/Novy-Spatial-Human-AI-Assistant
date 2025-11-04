using System.Collections.Generic;
using UnityEngine;

public static class ConeCastUtility
{
    public static Collider[] FindConeColliders(Vector3 pos, Vector3 dir, float angle, float distance, LayerMask mask)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, distance, mask);
        if (colliders.Length == 0) return colliders;

        List<Collider> hits = new List<Collider>();
        float radAngle = angle * Mathf.Deg2Rad; // Convert degrees to radians

        foreach (Collider coll in colliders)
        {
            Vector3 closestPoint = coll.ClosestPoint(pos); // Get closest point on the collider
            Vector3 directionToCollider = (closestPoint - pos).normalized; // Get direction to collider

            float dotProduct = Vector3.Dot(dir.normalized, directionToCollider); // Calculate angle similarity
            float angleToCollider = Mathf.Acos(dotProduct); // Convert to angle in radians

            if (angleToCollider <= radAngle) // Check if inside the cone
            {
                hits.Add(coll);
            }
        }

        return hits.ToArray();
    }
}
