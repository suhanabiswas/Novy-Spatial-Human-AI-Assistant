using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SpatialExporter : MonoBehaviour
{
    public Vector3 roomDimensions = new Vector3(4f, 2.5f, 5f);
    public string exportFileName = "spatial_layout.json";
    public string apiUrl = "http://localhost:5000";
    public LLMResponseHandler llmResponseHandler;
    private QueryData? lastQuery;

    void Start()
    {
        SaveRoomLayout();
    }

    public void SaveRoomLayout()
    {
        RoomLayout layout = new RoomLayout();
        layout.roomDimensions = roomDimensions;

        GameObject root = GameObject.Find("Office2");
        if (root == null)
        {
            Debug.LogError("No GameObject named 'Office2' found!");
            return;
        }

        List<GameObject> spatialObjects = new List<GameObject>();
        FindSpatialObjectsRecursive(root.transform, spatialObjects);

        foreach (GameObject obj in spatialObjects)
        {
            Vector3 size = Vector3.one;
            string color = null;

            Renderer r = obj.GetComponent<Renderer>();

            if (r == null)
            {
                // Check only the first child with a Renderer
                foreach (Transform child in obj.transform)
                {
                    Renderer childRenderer = child.GetComponent<Renderer>();
                    if (childRenderer != null)
                    {
                        r = childRenderer;

                        bool isOfficeFolder = childRenderer.gameObject.name.ToLower().Contains("office folder");
                        Material[] materials = childRenderer.materials;

                        if (isOfficeFolder && materials.Length > 1)
                        {
                            color = ColorToName(materials[1].color);
                        }
                        else if (materials.Length > 0)
                        {
                            color = ColorToName(materials[0].color);
                        }

                        break; // Only use the first child with a renderer
                    }
                }
            }
            else
            {
                // Renderer is on the object itself
                if (r.material != null)
                    color = ColorToName(r.material.color);
            }


            // Try collider first
            Collider c = obj.GetComponent<Collider>();
            if (c == null)
                c = obj.GetComponentInChildren<Collider>();

            if (c != null)
            {
                size = c.bounds.size;
            }
            else
            {
                // Fallback to renderer
                if (r != null)
                {
                    size = r.bounds.size;

                }
            }

            SpatialObject spatialObj = new SpatialObject
            {
                name = obj.name,
                position = obj.transform.position,
                rotation = obj.transform.eulerAngles,
                dimensions = size,
                color = color
            };

            layout.objects.Add(spatialObj);
        }


        string json = JsonUtility.ToJson(layout, true);
        string path = Path.Combine(Application.persistentDataPath, exportFileName);
        File.WriteAllText(path, json);
        Debug.Log("Saved to: " + path);
        StartCoroutine(UploadJson(path));
    }

    public static string ColorToName(Color color)
    {
        if (color == Color.red) return "red";
        if (color == Color.green) return "green";
        if (color == Color.blue) return "blue";
        if (color == Color.yellow) return "yellow";
        if (color == Color.black) return "black";
        if (color == Color.white) return "white";
        if (color == Color.gray) return "gray";
        if (color == Color.cyan) return "cyan";
        if (color == Color.magenta) return "magenta";

        return $"#{ColorUtility.ToHtmlStringRGB(color)}"; // fallback to hex
    }


    void FindSpatialObjectsRecursive(Transform parent, List<GameObject> found)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.CompareTag("SpatialObject"))
                found.Add(child.gameObject);

            FindSpatialObjectsRecursive(child, found);
        }
    }

    IEnumerator UploadJson(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, Path.GetFileName(filePath), "application/json");

        UnityWebRequest request = UnityWebRequest.Post($"{apiUrl}/upload_layout", form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Upload failed: {request.error}");
        }
        else
        {
            Debug.Log($"Layout uploaded successfully: {request.downloadHandler.text}");
        }
    }

    public void ResendLastQuery()
    {
        if (lastQuery != null)
        {
            Debug.Log("Resending last query...");
            StartCoroutine(SendQueryToLLM(lastQuery));
        }
        else
        {
            Debug.LogWarning("No previous query to resend.");
        }
    }

    public void SendQuery(string query, float[] userPos, float[] userForward, float[] userRight, string targetObj, float[] targetPos, string referenceObj, string prevTargetObj)
    {
        string currentInteractionMode = LLMInteractionModeManager.Instance.GetCurrentModeAsString();

        lastQuery = new QueryData(query, userPos, userForward, userRight, targetObj, targetPos, referenceObj, prevTargetObj, currentInteractionMode);
        StartCoroutine(SendQueryToLLM(lastQuery));
    }

    // updated version that takes a QueryData directly
    IEnumerator SendQueryToLLM(QueryData qd)
    {
        string jsonQuery = JsonUtility.ToJson(qd);
        Debug.Log($"Sending JSON to LLM: {jsonQuery}");

        using (UnityWebRequest request = new UnityWebRequest($"{apiUrl}/runtime_query", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonQuery);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Query failed: {request.error}");
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"LLM response: {jsonResponse}");

                if (llmResponseHandler != null)
                {
                    llmResponseHandler.HandleLLMResponse(jsonResponse);
                }
            }
        }
    }

    [System.Serializable]
    public class QueryData
    {
        public string query;
        public float[] user_forward;
        public float[] user_right;
        public float[] user_position;
        public string? target_object;
        public float[]? target_position;
        public string? reference_object;
        public string? prev_target_object;
        public string interaction_mode;

        public QueryData(string query, float[] userPos, float[] userForward, float[] userRight, string? target_object, float[]? targetPos, string? reference_object, string? prev_target_object, string interactionMode)
        {
            this.query = query;
            this.user_position = userPos;
            this.user_forward = userForward;
            this.user_right = userRight;
            this.target_object = target_object;
            this.target_position = targetPos;
            this.reference_object = reference_object;
            this.prev_target_object = prev_target_object;
            this.interaction_mode = interactionMode;
        }
    }
}

#nullable enable

[System.Serializable]
public class SpatialObject
{
    public string name;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 dimensions;
    public string color;
}

[System.Serializable]
public class RoomLayout
{
    public Vector3 roomDimensions;
    public List<SpatialObject> objects = new List<SpatialObject>();
}
