using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using TMPro;
using Newtonsoft.Json;
using System;

public class LLMResponseHandler : MonoBehaviour
{
    public SpatialExporter exporter;
    public TMP_Text feedbackText;
    public GameObject userTransform;

    //references to new object prefabs
    public GameObject vasePrefab;
    public GameObject framePrefab;
    public GameObject trashCanPrefab;
    public GameObject globePrefab;
    public GameObject wallClockPrefab;
    public GameObject smallSofaPrefab;
    public GameObject tablePrefab;

    public Button confirmButton;
    //public Button retryButton;
    //public Button submitCommandButton;

    private List<GameObject> previousTargets = new List<GameObject>();
    private GameObject lastHighlighted;
    private Color? lastOriginalColor;

    public GameObject previousTargetObject;

    public Whisper.Samples.STTManager sTTManager;
    public QueryInputHandler queryInputHandler;

    public ObjectPointer objectPointerScript;       // Script that follows a target
    public PointerArrow pointerArrowObject;          // Arrow that points at the target
    public CurveGenerator curveGenerator;           // Custom script to draw curves
    public string pointedTargetObject;
    public string pointedSurfaceObject;
    public float[] pointedTargetPos;

    private Vector3 userForwardVec;
    private Vector3 userRightVec;
    private Vector3 userPosVec;

    private LLMResponse latestResponse;

    [System.Serializable]
    public class LLMResponse
    {
        public string action;
        public string target_object;
        public float[] target_position;      
        public string reference_objects;
        public float[] reference_position;
        public string rotationAxis;      // "x", "y", or "z"
        public string direction;
        public float? distance;               // Nullable float
        public float? rotation;               // Nullable float
        public float? scale;                  // Nullable float
        public string color;
        public bool valid;
        public string reason;

        // New boolean flags for Manipulate action
        public bool isMove;
        public bool isRotate;
        public bool isScale;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Action: {action}");
            sb.AppendLine($"Target Object: {target_object}");
            sb.AppendLine($"Valid: {valid}");

            if (action == "manipulate")
            {
                sb.AppendLine($" - isMove: {isMove}");
                sb.AppendLine($" - isRotate: {isRotate}");
                sb.AppendLine($" - isScale: {isScale}");
            }

            if (!valid)
            {
                sb.AppendLine($"Reason: {reason}");
            }

            return sb.ToString();
        }
    }

    //using stack to store all versions of a given object, so can have multiple undos on it. Topmost state will be latest 
    private Dictionary<string, Stack<TransformState>> objectCache = new Dictionary<string, Stack<TransformState>>();

    [System.Serializable]
    public class TransformState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool isActive;
        public Color? color;
    }

    public void Start()
    {
        previousTargetObject = null;
    }

    public void HandleLLMResponse(string json)
    {
        string validatedJson = JsonValidator(json);

        if (validatedJson != null)
        {
            try
            {
                // Deserialize the inner JSON string into the LLMResponse object
                latestResponse = JsonConvert.DeserializeObject<LLMResponse>(validatedJson);
                Debug.Log($"Suggested action: \n{latestResponse}");

                if (latestResponse.valid)
                {
                    //Only clear previous targets if action is not undo
                    if (latestResponse.action.ToLower() != "undo")
                    {
                        previousTargets.Clear();
                    } 

                    OnUserConfirms();

                }
                else
                {
                    sTTManager.GiveFeedback($"Unfortunately, I could not execute your command: {latestResponse.reason}. Please start your next command with \"Hey Novy...\"", sTTManager.HexToColor("#633335"));
                }
            }
            catch (JsonException ex)
            {
                Debug.LogError($"Failed to parse JSON: {ex.Message}");
                sTTManager.GiveFeedback("Error encountered, processing command again, please wait.", sTTManager.HexToColor("#633335"));
                //feedbackText.text = "Error encountered, processing command again, please wait.";
                exporter.ResendLastQuery();
            }
        }

    }

    private string JsonValidator(string json)
    {
        try
        {
            // Try to extract the "response" field
            var outerResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (!outerResponse.TryGetValue("response", out string innerJson))
            {
                Debug.LogError("Missing 'response' field in LLM reply.");
                return null;
            }

            innerJson = innerJson.Trim();

            // Strip markdown blocks (e.g., ```json ... ```)
            if (innerJson.StartsWith("```json"))
                innerJson = innerJson.Substring(7).TrimStart(); // remove "```json\n"
            else if (innerJson.StartsWith("```"))
                innerJson = innerJson.Substring(3).TrimStart(); // remove generic "```"

            if (innerJson.EndsWith("```"))
                innerJson = innerJson.Substring(0, innerJson.Length - 3).TrimEnd();

            // Remove any text before the actual JSON starts
            int firstJsonChar = innerJson.IndexOfAny(new[] { '{', '[' });
            if (firstJsonChar > 0)
            {
                innerJson = innerJson.Substring(firstJsonChar);
            }

            return innerJson;
        }
        catch (JsonException ex)
        {
            Debug.LogError($"JsonValidator failed: {ex.Message}");
            OnUserRetries();
            return null;
        }
    }

    public void OnUserConfirms()
    {
        ApplyAction(latestResponse);
        exporter.SaveRoomLayout(); // Save & re-upload updated layout
        if(latestResponse.action != "undo")
            sTTManager.GiveFeedback("Action applied and layout updated. Please start your next command with \"Hey Novy...\"...", sTTManager.HexToColor("#485E30"));
    }

    public void OnUserRetries()
    {
       // queryInputHandler.OnSubmit();
        feedbackText.text = "Sending clarified command...";
        queryInputHandler.OnSubmit(sTTManager.pendingCommand.command);
        Debug.Log("Button resend command pressed!");
        //retryButton.gameObject.SetActive(false);
        //submitCommandButton.gameObject.SetActive(true);
    }

    public void SetUserContext(float[] userPos, float[] userForward, float[] userRight)
    {
        userPosVec = new Vector3(userPos[0], userPos[1], userPos[2]);
        userForwardVec = new Vector3(userForward[0], userForward[1], userForward[2]);
        userRightVec = new Vector3(userRight[0], userRight[1], userRight[2]);
    }

    private void PlaceAndRotateAgainstSurface(GameObject obj, string surfaceName)
    {
        if (obj == null || string.IsNullOrEmpty(surfaceName)) return;

        surfaceName = surfaceName.ToLowerInvariant();
        Quaternion rotation = Quaternion.identity;

        switch (surfaceName)
        {
            case "wall1":
                rotation = Quaternion.Euler(0f, 180f, 0f); // Facing into room from wall1
                break;
            case "wall2":
                rotation = Quaternion.Euler(0f, 0f, 0f); // No rotation
                break;
            case "wall3":
                rotation = Quaternion.Euler(0f, -90f, 0f); // Facing into room from wall3
                break;
            case "wall4":
                rotation = Quaternion.Euler(0f, 90f, 0f); // Facing into room from wall4
                break;
            default:
                return;
        }

        obj.transform.rotation = rotation;
    }

    private void FaceObjectTowardUser(GameObject obj)
    {
        if (obj == null || userTransform == null) return;

        Vector3 direction = userTransform.transform.position - obj.transform.position;
        direction.y = 0f; // Yaw only

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            obj.transform.rotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f);
        }
    }

    private void ApplyAction(LLMResponse response)
    {
        string[] targetNames = response.target_object.Split(',');
        List<GameObject> targets = new List<GameObject>();

        if(response.action.ToLower() != "add" || response.action.ToLower() != "undo")
        {
            foreach (var name in targetNames)
            {
                string trimmedName = name.Trim();
                if (!string.IsNullOrEmpty(trimmedName))
                {
                    GameObject found = FindAndCacheTarget(trimmedName);
                    if (found != null)
                    {
                        targets.Add(found);
                        previousTargetObject = found;
                    }
                }
            }
        }

        string action = response.action.ToLower();

        switch (action)
        {
            case "manipulate":
                // Move
                if (response.isMove)
                {
                    string dir = response.direction?.ToLowerInvariant();
                    HashSet<string> relativeDirections = new HashSet<string> { "left", "right", "forward", "backward", "towards", "away", "towards me", "away from me" };
                    if (!string.IsNullOrEmpty(dir) && relativeDirections.Contains(dir) && response.reference_objects == null)
                    {
                        float distance = response.distance ?? 0.5f;
                        Vector3 offset;

                        switch (dir)
                        {
                            case "left":
                                offset = -userRightVec * distance;
                                break;
                            case "right":
                                offset = userRightVec * distance;
                                break;
                            case "forward":
                            case "towards":
                            case "towards me":
                                offset = -userForwardVec * distance;
                                break;
                            case "backward":
                            case "away":
                            case "away from me":
                                offset = userForwardVec * distance;
                                break;
                            default:
                                offset = Vector3.zero;
                                break;
                        }

                        foreach (var target in targets)
                        {
                            target.transform.position += offset;
                        }
                    }
                    else if (response.target_position != null && response.target_position.Length == 3)
                    {
                        Vector3 targetPos = new Vector3(response.target_position[0],response.target_position[1],response.target_position[2]);

                        foreach (var target in targets)
                        {
                            target.transform.position = targetPos;

                            if (!string.IsNullOrEmpty(response.reference_objects))
                            {
                                string refObj = response.reference_objects.ToLowerInvariant();
                                if (refObj.Contains("wall") || refObj == "ceiling" || refObj == "floor")
                                {
                                    PlaceAndRotateAgainstSurface(target, refObj);
                                }
                            }
                        }

                    }
                }

                if (response.isRotate)
                {
                    string rotDir = response.direction?.ToLowerInvariant();
                    HashSet<string> relativeRotations = new HashSet<string>
    {
        "left", "right", "rotate left", "rotate right",
        "clockwise", "anticlockwise", "counter-clockwise", "counterclockwise", "counter clockwise"
    };

                    if (!string.IsNullOrEmpty(rotDir) && relativeRotations.Contains(rotDir))
                    {
                        float rotationAmount = response.rotation ?? 45f;
                        Vector3 rotationEuler = Vector3.zero;

                        switch (rotDir)
                        {
                            case "left":
                            case "clockwise":
                            case "rotate left":
                                rotationEuler.y = rotationAmount; // Rotate left (clockwise around Y)
                                break;
                            case "right":
                            case "anticlockwise":
                            case "counterclockwise":
                            case "counter clockwise":
                            case "counter-clockwise":
                            case "rotate right":
                                rotationEuler.y = (rotationAmount > 0) ? -rotationAmount : rotationAmount; // Rotate right (counter-clockwise around Y)
                                break;
                            default:
                                break;
                        }

                        foreach (var target in targets)
                        {
                            target.transform.RotateAround(target.transform.position, Vector3.up, rotationEuler.y);
                        }
                    }
                    else if (rotDir == "towards me" || rotDir == "towards user")
                    {
                        foreach (var target in targets)
                        {
                            Vector3 lookDirection = userTransform.transform.position - target.transform.position;
                            lookDirection.y = 0f; // Yaw-only: remove vertical tilt
                            if (lookDirection != Vector3.zero)
                            {
                                Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
                                target.transform.rotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f);
                            }
                        }
                    }
                    else if (rotDir == "towards" && !string.IsNullOrEmpty(response.reference_objects))
                    {
                        Transform reference = FindReferenceObjectByName(response.reference_objects); // Implement this method

                        if (reference != null)
                        {
                            foreach (var target in targets)
                            {
                                Vector3 lookDirection = reference.position - target.transform.position;
                                lookDirection.y = 0f; // Yaw-only
                                if (lookDirection != Vector3.zero)
                                {
                                    Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
                                    target.transform.rotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f);
                                }
                            }
                        }
                    }
                    else if (response.rotation.HasValue)
                    {
                        foreach (var target in targets)
                        {
                            ApplyRotation(target, response.rotation, response.rotationAxis);
                        }
                    }
                }


                // Scale
                if (response.isScale)
                {
                    if (response.scale.HasValue)
                    {
                        foreach (var target in targets)
                        {
                            target.transform.localScale *= response.scale.Value;
                        }
                    }
                }
                break;

            case "find":
                foreach (var target in targets)
                {
                    HighlightObject(target);
                }
                break;

            case "recolor":
                if (!string.IsNullOrEmpty(response.color))
                {
                    Color newColor;
                    if (TryParseNamedColor(response.color, out newColor) ||
                        ColorUtility.TryParseHtmlString(response.color, out newColor))
                    {
                        foreach (GameObject obj in targets)
                        {
                            Renderer r = obj.GetComponent<Renderer>();

                            // If no renderer on the object, find the first child with a renderer
                            if (r == null)
                            {
                                foreach (Transform child in obj.transform)
                                {
                                    Renderer childRenderer = child.GetComponent<Renderer>();
                                    if (childRenderer != null)
                                    {
                                        r = childRenderer;
                                        break; // Use only the first found child renderer
                                    }
                                }
                            }

                            if (r != null)
                            {
                                Material[] materials = r.materials;

                                // Check if this is an OfficeFolder renderer
                                bool isOfficeFolder = r.gameObject.name.ToLower().Contains("office folder");

                                // Choose which material to update
                                int materialIndex = (isOfficeFolder && materials.Length > 1) ? 1 : 0;

                                if (materials.Length > materialIndex)
                                {
                                    Material mat = materials[materialIndex];
                                    mat.color = newColor;

                                    if (mat.HasProperty("_BaseColor"))
                                        mat.SetColor("_BaseColor", newColor);
                                    else if (mat.HasProperty("_Color"))
                                        mat.SetColor("_Color", newColor);
                                }
                            }
                        }

                    }
                    else
                    {
                        Debug.LogWarning($"Could not parse color: {response.color}");
                    }
                }
                break;

            case "delete":
                foreach (var target in targets)
                {
                    //Destroy(target); // Optionally use this instead
                    target.SetActive(false);
                }
                break;

            case "undo":
                UndoLastAction();
                break;

            case "add":
                if (!string.IsNullOrEmpty(response.target_object) &&
                    response.target_position != null && response.target_position.Length == 3)
                {
                    Vector3 position = new Vector3(
                        response.target_position[0],
                        response.target_position[1],
                        response.target_position[2]
                    );

                    GameObject prefabToInstantiate = null;
                    string normalizedName = NormalizeObjectName(response.target_object);

                    if (normalizedName.Contains("vase"))
                    {
                        prefabToInstantiate = vasePrefab;
                    }
                    else if (normalizedName.Contains("frame") || normalizedName.Contains("picture"))
                    {
                        prefabToInstantiate = framePrefab;
                    }
                    else if (normalizedName.Contains("trash") || normalizedName.Contains("bin") || normalizedName.Contains("waste"))
                    {
                        prefabToInstantiate = trashCanPrefab;
                    }
                    else if (normalizedName.Contains("sofa") || normalizedName.Contains("couch") || normalizedName.Contains("loveseat"))
                    {
                        prefabToInstantiate = smallSofaPrefab;
                    }
                    else if (normalizedName.Contains("globe") || normalizedName.Contains("world") || normalizedName.Contains("map"))
                    {
                        prefabToInstantiate = globePrefab;
                    }
                    else if (normalizedName.Contains("clock") || normalizedName.Contains("time") || normalizedName.Contains("wallclock"))
                    {
                        prefabToInstantiate = wallClockPrefab;
                    }
                    else if (normalizedName.Contains("table") || normalizedName.Contains("desk") || normalizedName.Contains("stand"))
                    {
                        prefabToInstantiate = tablePrefab;
                    }
                    else
                    {
                        Debug.LogWarning($"No prefab defined for object type: {response.target_object}");
                    }

                    if (prefabToInstantiate != null)
                    {
                        GameObject newObject = Instantiate(prefabToInstantiate, position, Quaternion.identity);
                        if (!string.IsNullOrEmpty(response.reference_objects))
                        {
                            string refObj = response.reference_objects.ToLowerInvariant();
                            if (refObj.Contains("wall"))
                            {
                                PlaceAndRotateAgainstSurface(newObject, refObj);
                            }
                            else
                            {
                                FaceObjectTowardUser(newObject);
                            }
                        }
                        else
                        {
                            // No reference object — treat it as a normal in-room placement and rotate to face user
                            FaceObjectTowardUser(newObject);
                        }
                        HighlightObject(newObject);
                        newObject.name = response.target_object;

                        GameObject parentObject = GameObject.Find("Office2");
                        if (parentObject != null)
                        {
                            newObject.transform.SetParent(parentObject.transform);
                        }
                        else
                        {
                            Debug.LogWarning("Parent object 'Office2' not found in scene.");
                        }

                        CacheTransform(response.target_object, newObject);
                        previousTargets.Add(newObject);
                        previousTargetObject = newObject;
                    }
                }
                break;
        }

        if (targets.Count > 0)
        {
            previousTargetObject = targets[0]; // Or you could store the entire list if needed
        }

        if (response.action.ToLower() != "delete" || response.action.ToLower() != "add")
        {
            GameObject target = GameObject.Find(response.target_object);
            if (target)
            {
                HighlightObject(target);
            }
        }
    }

    private Transform FindReferenceObjectByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Try exact match first
        GameObject exactMatch = GameObject.Find(name);
        if (exactMatch != null)
            return exactMatch.transform;

        // Optional: try case-insensitive and partial matches
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                obj.name.ToLowerInvariant().Contains(name.ToLowerInvariant()))
            {
                return obj.transform;
            }
        }

        // If no match found
        Debug.LogWarning($"Reference object '{name}' not found in the scene.");
        return null;
    }


    private GameObject FindAndCacheTarget(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            CacheTransform(objectName, target);
            previousTargets.Add(target);
        }
        else
        {
            Debug.LogWarning($"Object '{objectName}' not found in scene.");
        }
        return target;
    }


    private string NormalizeObjectName(string name)
    {
        return name.ToLowerInvariant().Replace(" ", "").Replace("-", "");
    }

    void ApplyRotation(GameObject obj, float? rotationValue, string axis)
    {
        if (obj == null || rotationValue == null)
        {
            Debug.LogWarning("ApplyRotation: Invalid object or rotation value.");
            return;
        }

        Vector3 rotationEuler = Vector3.zero;

        // Normalize and default to "y" if axis is null, empty, or invalid
        axis = (axis ?? "y").ToLower();
        if (axis != "x" && axis != "y" && axis != "z")
        {
            axis = "y";
        }

        // Apply rotation to the correct axis
        switch (axis)
        {
            case "x":
                rotationEuler.x = rotationValue.Value;
                break;
            case "y":
                rotationEuler.y = rotationValue.Value;
                break;
            case "z":
                rotationEuler.z = rotationValue.Value;
                break;
        }

        obj.transform.Rotate(rotationEuler, Space.Self);
    }

    private bool TryParseNamedColor(string colorName, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(colorName))
            return false;

        var formatted = colorName.Replace(" ", "").Replace("-", "").ToLowerInvariant();

        try
        {
            var myColorType = typeof(MyColor);
            var prop = myColorType.GetProperty(formatted, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType == typeof(Color))
            {
                color = (Color)prop.GetValue(null, null);
                return true;
            }
        }
        catch
        {
            // Ignore reflection errors
        }

        return false;
    }


    private void CacheTransform(string objectName, GameObject obj)
    {
        if (!objectCache.ContainsKey(objectName))
        {
            objectCache[objectName] = new Stack<TransformState>();
        }

        Renderer r = obj.GetComponent<Renderer>();
        Color? prevColor = null;

        if (r == null)
        {
            // Check only the first child with a Renderer
            foreach (Transform child in obj.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    r = childRenderer;
                    Material[] materials = r.materials;

                    bool isOfficeFolder = r.gameObject.name.ToLower().Contains("office folder");

                    if (isOfficeFolder && materials.Length > 1)
                    {
                        if (materials[1].HasProperty("_BaseColor"))
                            prevColor = materials[1].GetColor("_BaseColor");
                        else if (materials[1].HasProperty("_Color"))
                            prevColor = materials[1].GetColor("_Color");
                    }
                    else if (materials.Length > 0)
                    {
                        if (materials[0].HasProperty("_BaseColor"))
                            prevColor = materials[0].GetColor("_BaseColor");
                        else if (materials[0].HasProperty("_Color"))
                            prevColor = materials[0].GetColor("_Color");
                    }

                    break; // Only use the first child with a renderer
                }
            }
        }
        else
        {
            Material mat = r.material;
            if (mat.HasProperty("_BaseColor"))
                prevColor = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color"))
                prevColor = mat.GetColor("_Color");
        }

        // Push the current state to the stack
        objectCache[objectName].Push(new TransformState()
        {
            position = obj.transform.position,
            rotation = obj.transform.rotation,
            scale = obj.transform.localScale,
            isActive = obj.activeSelf,
            color = prevColor
        });
    }

    public void UndoLastAction()
    {
        if (previousTargets == null || previousTargets.Count == 0)
        {
            sTTManager.GiveFeedback("No previous targets to undo. Ready for your command! Please start your command with \"Hey Novy...\"", sTTManager.HexToColor("#633335"));
            return;
        }

        List<string> undoneObjectNames = new List<string>();

        foreach (GameObject target in previousTargets)
        {
            previousTargetObject = target;
            if (target == null) continue;

            string objectName = target.name;
            if (objectCache.ContainsKey(objectName) && objectCache[objectName].Count > 0)
            {
                var state = objectCache[objectName].Pop();

                target.transform.position = state.position;
                target.transform.rotation = state.rotation;
                target.transform.localScale = state.scale;
                target.SetActive(state.isActive);

                Renderer rend = target.GetComponent<Renderer>();
                if (rend == null)
                {
                    foreach (Transform child in target.transform)
                    {
                        Renderer childRenderer = child.GetComponent<Renderer>();
                        if (childRenderer != null)
                        {
                            rend = childRenderer;
                            break;
                        }
                    }
                }

                if (rend != null && state.color.HasValue)
                {
                    Color c = state.color.Value;

                    Material[] materials = rend.materials;
                    bool isOfficeFolder = rend.gameObject.name.ToLower().Contains("office folder");

                    if (isOfficeFolder && materials.Length > 1)
                    {
                        if (materials[1].HasProperty("_BaseColor"))
                            materials[1].SetColor("_BaseColor", c);
                        else if (materials[1].HasProperty("_Color"))
                            materials[1].SetColor("_Color", c);
                    }
                    else if (materials.Length > 0)
                    {
                        if (materials[0].HasProperty("_BaseColor"))
                            materials[0].SetColor("_BaseColor", c);
                        else if (materials[0].HasProperty("_Color"))
                            materials[0].SetColor("_Color", c);
                    }
                }

                undoneObjectNames.Add(objectName);
            }
        }

        previousTargets.Clear();

        if (undoneObjectNames.Count > 0)
        {
            string names = string.Join(", ", undoneObjectNames);
            string message = $"Undid changes to: {names}. Please start your next command with \"Hey Novy...\".";
            sTTManager.GiveFeedback(message, sTTManager.HexToColor("#485E30"));
            exporter.SaveRoomLayout();
        }
        else
        {
            sTTManager.GiveFeedback("No cached state found for previous targets.Please start your next command with \"Hey Novy...\"", sTTManager.HexToColor("#633335"));
        }
    }

    private void HighlightObject(GameObject obj)
    {
        //UnhighlightLatestObject();
        /*Renderer rend = obj.GetComponent<Renderer>();
        if (rend)
        {
            lastOriginalColor = rend.material.color;
            Debug.Log(lastOriginalColor.Value);
            rend.material.color = Color.yellow;
        }*/
        //lastHighlighted = obj;

        // Set pointers and curve targets
        if (objectPointerScript != null)
            objectPointerScript.SetTarget(obj);

        if (pointerArrowObject != null)
            pointerArrowObject.SetObjectToPoint(obj); 

        if (curveGenerator != null)
            curveGenerator.SetSingleTarget(obj.transform, null);
    }

    /*private void UnhighlightLatestObject()
    {
        if (lastHighlighted && lastOriginalColor.HasValue)
        {
            Renderer r = lastHighlighted.GetComponent<Renderer>();
            if (r) r.material.color = lastOriginalColor.Value;
        }

        
        // Unset references
        if (objectPointerScript != null)
            objectPointerScript.ClearTarget();

        if (pointerArrowObject != null)
            pointerArrowObject.ClearTarget(); 

        if (curveGenerator != null)
            curveGenerator.SetSingleTarget(null, null);
        
        lastHighlighted = null;
        lastOriginalColor = null;
        
    }*/

}
