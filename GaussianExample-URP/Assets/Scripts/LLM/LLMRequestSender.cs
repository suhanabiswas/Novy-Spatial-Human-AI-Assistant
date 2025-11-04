using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

//not used at the momet, was for initial testing

public class LLMRequestSender : MonoBehaviour
{
    public string jsonFileName = "spatialData"; // Without .json extension
    //public TextAsset jsonFile;
    public string pointedTargetObject;

    void Start()
    {
        StartCoroutine(SendJsonFileToFlask());
    }

    IEnumerator SendJsonFileToFlask()
    {
        // Load JSON file from Resources
       TextAsset jsonFile = Resources.Load<TextAsset>(jsonFileName);
        if (jsonFile == null)
        {
            Debug.LogError("JSON file not found.");
            yield break;
        }

        // Prepare JSON payload to send
        string jsonPayload = $"{{\"user_query\": \"What can you infer from this spatial layout?\", \"spatial_data\": {jsonFile.text} }}";

        UnityWebRequest request = new UnityWebRequest("http://localhost:5000/query_spatial", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("Uploading JSON to Flask...");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Upload failed: " + request.error);
        }
        else
        {
            Debug.Log("LLM Response: " + request.downloadHandler.text);
        }
    }
}
