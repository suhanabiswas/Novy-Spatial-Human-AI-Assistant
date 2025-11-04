using UnityEngine;

public class ConeCastExample : MonoBehaviour
{
    public float detectionDistance = 5f;
    public float coneAngle = 30f;
    public LayerMask detectionMask;
    private Collider[] lastDetectedObjects;
    public GameObject detectableObjects;

    private Collider mostPresentObject;

    void Update()
    {
       
        if (Input.GetKeyDown(KeyCode.Space)) // Press Space to detect objects
        {
            lastDetectedObjects = ConeCastUtility.FindConeColliders(
                transform.position, transform.forward, coneAngle, detectionDistance, detectionMask
            );

            if( lastDetectedObjects.Length > 0)
            {
                FindObjectWithMostVolumeInsideCone(); // Find the object most present inside the cone
            }
            ApplyColors();
        }

        // Movement Controls for testing
        if (Input.GetKeyDown(KeyCode.W))
        {
            transform.position += new Vector3(0, 0.2f, 0);
            mostPresentObject = null;
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            transform.position += new Vector3(0, -0.2f, 0);
            mostPresentObject = null;
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            transform.position += new Vector3(0.2f, 0, 0);
            mostPresentObject = null;
        }
        
        if (Input.GetKeyDown(KeyCode.A))
        {
            transform.position += new Vector3(-0.2f, 0, 0);
            mostPresentObject = null;
        }
        
    }

    void FindObjectWithMostVolumeInsideCone()
    {
        float maxInsideVolumeRatio = 0f;
        mostPresentObject = lastDetectedObjects[0];

        foreach (var obj in lastDetectedObjects)
        {
            float insideVolume = CalculateVolumeInsideCone(obj);
            float totalVolume = 0f;

            // Calculate total volume based on collider type
            if (obj is BoxCollider boxCollider)
            {
                totalVolume = boxCollider.size.x * boxCollider.size.y * boxCollider.size.z;
            }
            else if (obj is SphereCollider sphereCollider)
            {
                totalVolume = (4f / 3f) * Mathf.PI * Mathf.Pow(sphereCollider.radius, 3);
            }
            else if (obj is CapsuleCollider capsuleCollider)
            {
                totalVolume = Mathf.PI * Mathf.Pow(capsuleCollider.radius, 2) * capsuleCollider.height;
            }

            float insideVolumeRatio = insideVolume / totalVolume;

            if (insideVolumeRatio > maxInsideVolumeRatio)
            {
                maxInsideVolumeRatio = insideVolumeRatio;
                mostPresentObject = obj;
            }
        }

        if (mostPresentObject != null)
        {
            Debug.Log("Object with most volume in the cone: " + mostPresentObject.name);
            ChangeObjectColor(mostPresentObject, Color.red);
        }
    }

    // Calculate how much of the object's volume is inside the cone using different collider types
    float CalculateVolumeInsideCone(Collider obj)
    {
        float objectVolumeInsideCone = 0f;

        if (obj is BoxCollider boxCollider)
        {
            objectVolumeInsideCone = CalculateBoxVolumeInsideCone(boxCollider);
        }
        else if (obj is SphereCollider sphereCollider)
        {
            objectVolumeInsideCone = CalculateSphereVolumeInsideCone(sphereCollider);
        }
        else if (obj is CapsuleCollider capsuleCollider)
        {
            objectVolumeInsideCone = CalculateCapsuleVolumeInsideCone(capsuleCollider);
        }

        return objectVolumeInsideCone;
    }

    // Handle BoxCollider
    float CalculateBoxVolumeInsideCone(BoxCollider boxCollider)
    {
        float objectVolumeInsideCone = 0f;
        Vector3 boxCenter = boxCollider.transform.position + boxCollider.center;
        Vector3 directionToBox = boxCenter - transform.position;
        float distanceToBox = directionToBox.magnitude;

        if (distanceToBox <= detectionDistance)
        {
            float angleToBox = Vector3.Angle(transform.forward, directionToBox.normalized);
            if (angleToBox <= coneAngle)
            {
                // Approximate by considering the full volume if inside cone
                objectVolumeInsideCone = boxCollider.size.x * boxCollider.size.y * boxCollider.size.z;
            }
        }

        return objectVolumeInsideCone;
    }

    // Handle SphereCollider
    float CalculateSphereVolumeInsideCone(SphereCollider sphereCollider)
    {
        float objectVolumeInsideCone = 0f;
        Vector3 sphereCenter = sphereCollider.transform.position + sphereCollider.center;
        Vector3 directionToSphere = sphereCenter - transform.position;
        float distanceToSphere = directionToSphere.magnitude;

        if (distanceToSphere <= detectionDistance)
        {
            float angleToSphere = Vector3.Angle(transform.forward, directionToSphere.normalized);
            if (angleToSphere <= coneAngle)
            {
                // Calculate the volume of the sphere (if fully inside cone)
                objectVolumeInsideCone = (4f / 3f) * Mathf.PI * Mathf.Pow(sphereCollider.radius, 3);
            }
        }

        return objectVolumeInsideCone;
    }

    // Handle CapsuleCollider
    float CalculateCapsuleVolumeInsideCone(CapsuleCollider capsuleCollider)
    {
        float objectVolumeInsideCone = 0f;
        Vector3 capsuleCenter = capsuleCollider.transform.position + capsuleCollider.center;
        Vector3 directionToCapsule = capsuleCenter - transform.position;
        float distanceToCapsule = directionToCapsule.magnitude;

        if (distanceToCapsule <= detectionDistance)
        {
            float angleToCapsule = Vector3.Angle(transform.forward, directionToCapsule.normalized);
            if (angleToCapsule <= coneAngle)
            {
                // Approximate the capsule volume as the full volume if it is inside the cone
                objectVolumeInsideCone = Mathf.PI * Mathf.Pow(capsuleCollider.radius, 2) * capsuleCollider.height;
            }
        }

        return objectVolumeInsideCone;
    }

    void ApplyColors()
    {
        Collider[] allColliders = detectableObjects.GetComponentsInChildren<Collider>();

        foreach (var obj in allColliders)
        {
            ChangeObjectColor(obj, Color.white); // Set default color to white
        }

        if (lastDetectedObjects != null)
        {
            foreach (var hit in lastDetectedObjects)
            {
                ChangeObjectColor(hit, Color.blue); // Detected objects turn blue
            }
        }

        if (mostPresentObject != null)
        {
            ChangeObjectColor(mostPresentObject, Color.red); // Highlight most present object in red
        }
    }

    void ChangeObjectColor(Collider obj, Color color)
    {
        Renderer objRenderer = obj.GetComponent<Renderer>();
        if (objRenderer != null)
        {
            objRenderer.material.color = color;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);

        Gizmos.color = Color.yellow;
        Vector3 leftLimit = Quaternion.Euler(0, -coneAngle, 0) * transform.forward * detectionDistance;
        Vector3 rightLimit = Quaternion.Euler(0, coneAngle, 0) * transform.forward * detectionDistance;
        Gizmos.DrawRay(transform.position, leftLimit);
        Gizmos.DrawRay(transform.position, rightLimit);
    }
}
