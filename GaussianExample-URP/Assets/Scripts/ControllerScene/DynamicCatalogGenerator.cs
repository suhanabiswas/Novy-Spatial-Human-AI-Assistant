using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DynamicCatalogGenerator : MonoBehaviour
{
    [Tooltip("Parent UI Panel to hold buttons")]
    public Transform buttonsParent;

    [Tooltip("Prefab for the button - must have a Button component")]
    public GameObject buttonPrefab;

    public GameObject officeObjects;

    [Header("Category Dropdown")]
    public TMP_Dropdown categoryDropdown;

    private List<GameObject> generatedButtons = new List<GameObject>();

    private void OnEnable()
    {
        PopulateDropdown();
        categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        GenerateCatalogButtons();
    }

    private void OnDisable()
    {
        ClearButtons();
        categoryDropdown.onValueChanged.RemoveListener(OnCategoryChanged);
    }

    private void PopulateDropdown()
    {
        categoryDropdown.ClearOptions();
        var categoryNames = new List<string>(System.Enum.GetNames(typeof(ObjectCategory)));
        categoryDropdown.AddOptions(categoryNames);
    }

    private void OnCategoryChanged(int index)
    {
        GenerateCatalogButtons();
    }

    private void GenerateCatalogButtons()
    {
        if (buttonsParent == null || buttonPrefab == null || officeObjects == null || categoryDropdown == null)
        {
            Debug.LogError("One or more references not assigned.");
            return;
        }

        ClearButtons();

        ObjectCategory selectedCategory = (ObjectCategory)categoryDropdown.value;

        List<GameObject> spatialObjects = new List<GameObject>();
        FindSpatialObjectsRecursive(officeObjects.transform, spatialObjects);

        foreach (GameObject obj in spatialObjects)
        {
            var meta = obj.GetComponent<ObjectMetadata>();
            if (meta == null || meta.category != selectedCategory)
                continue;

            GameObject newButtonObj = Instantiate(buttonPrefab, buttonsParent);
            generatedButtons.Add(newButtonObj);

            var textComponent = newButtonObj.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = obj.name;

            var findBtn = newButtonObj.AddComponent<FindObjectButton>();
            findBtn.targetObject = obj;
        }
    }

    private void ClearButtons()
    {
        foreach (GameObject btn in generatedButtons)
        {
            Destroy(btn);
        }
        generatedButtons.Clear();
    }

    private void FindSpatialObjectsRecursive(Transform parent, List<GameObject> found)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.CompareTag("SpatialObject") && child.gameObject.activeSelf)
                found.Add(child.gameObject);

            FindSpatialObjectsRecursive(child, found);
        }
    }
}
