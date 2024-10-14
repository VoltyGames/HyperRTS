using HyperRTS.Core.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ResourcesEditor : EditorWindow
{
    [SerializeField] 
    private VisualTreeAsset UXMLTree;
    
    [MenuItem("RTS Engine/Resource Manager")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<ResourcesEditor>();
        wnd.titleContent = new GUIContent("Resource Editor");
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Add(UXMLTree.Instantiate());
        
        // Get references to UI elements
        var resourceNameField = root.Q<TextField>("resourceNameField");
        var addResourceButton = root.Q<Button>("addResourceButton");
        var resourceList = root.Q<ScrollView>("resourceList");

        // Load existing resources into the ScrollView
        LoadResourceTypes(resourceList);

        // Button click handler for adding a new resource
        addResourceButton.clicked += () =>
        {
            var resourceName = resourceNameField.value;
            if (!string.IsNullOrEmpty(resourceName))
            {
                AddResource(resourceName, resourceList);
                resourceNameField.value = "";  // Clear the input field
            }
        };
    }
    
    private void AddResource(string resourceName, ScrollView resourceList)
    {
        // Register the new resource type
        var newResource = ResourceRegistry.RegisterResourceType(resourceName);

        // Add the new resource to the UI
        var resourceLabel = new Label($"- {newResource.Name} (ID: {newResource.Id})");
        resourceList.Add(resourceLabel);
    }

    // Load all registered resources into the ScrollView
    private void LoadResourceTypes(ScrollView resourceList)
    {
        resourceList.Clear();
        var resourceTypes = ResourceRegistry.GetResourceTypes();
        
        foreach (var resource in resourceTypes)
        {
            var resourceLabel = new Label($"- {resource.Name} (ID: {resource.Id})");
            resourceList.Add(resourceLabel);
        }
    }
}
