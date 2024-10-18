using System.IO;
using HyperRTS.Core.Resources;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Resource
{
    public class ResourcesEditor : EditorWindow
    {
        private const string ResourcePrefabPath = "Assets/Prefabs/Resources";
        
        [SerializeField]
        private VisualTreeAsset uxmlTree;

        [MenuItem("RTS Engine/Resource Manager")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ResourcesEditor>();
            wnd.titleContent = new GUIContent("Resource Editor");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Add(uxmlTree.Instantiate());

            // Get references to UI elements
            var resourceNameField = root.Q<TextField>("resourceNameField");
            var addResourceButton = root.Q<Button>("addResourceButton");
            var resourceIconField = root.Q<ObjectField>("resourceIconField");
            var resourcesList = root.Q<ScrollView>("resourcesList");

            // Load existing resources into the ScrollView
            LoadRegisteredResources(resourcesList);

            // Button click handler for adding a new resource
            addResourceButton.clicked += () =>
            {
                var resourceName = resourceNameField.value;
                var resourceIcon = resourceIconField.value as Sprite;

                if (!string.IsNullOrEmpty(resourceName))
                {
                    AddResource(resourceName, resourceIcon, resourcesList);
                    resourceNameField.value = ""; // Clear the input field
                    resourceIconField.value = null; // Clear the icon field
                }
            };
        }

        private void AddResource(string resourceName, Sprite resourceIcon, ScrollView resourceList)
        {
            // Register the new resource type
            var newResource = ResourceRegistry.RegisterResourceType(resourceName);

            // Create a ResourceData ScriptableObject to hold data
            var resourceData = CreateInstance<ResourceData>();
            resourceData.name = resourceName;
            resourceData.resourceType = newResource;
            resourceData.resourceIcon = resourceIcon;

            // Add the new resource to the UI
            var resourceLabel = new Label($"- {newResource.Name} (ID: {newResource.Id})");
            resourceList.Add(resourceLabel);
        }
        
        // Save ResourceData as a prefab in the Assets/Resources/Prefabs folder
        private void SaveResourceAsPrefab(ResourceData resourceData, string path)
        {
            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Create an empty GameObject to hold the ResourceData
            var resourceObject = new GameObject(resourceData.name);
            resourceObject.AddComponent<ResourceData>().CopyFrom(resourceData); // Copy data into the new GameObject

            // Save the GameObject as a prefab
            PrefabUtility.SaveAsPrefabAsset(resourceObject, path);
            DestroyImmediate(resourceObject); // Clean up
        }

        // Load registered resources and populate the UI
        private void LoadRegisteredResources(ScrollView resourceList)
        {
            resourceList.Clear();
            var resourcePaths = Directory.GetFiles(ResourcePrefabPath, "*.prefab");

            foreach (var path in resourcePaths)
            {
                var resourceData = AssetDatabase.LoadAssetAtPath<ResourceData>(path);
                
                if (resourceData is not null)
                {
                    AddResourceToUI(resourceData, resourceList);
                }
            }
        }

        // Add a resource entry to the UI list
        private void AddResourceToUI(ResourceData resourceData, ScrollView resourceList)
        {
            // Create a label for the resource with its name and icon
            var resourceEntry = new VisualElement();
            var resourceLabel = new Label($"- {resourceData.name} (ID: {resourceData.resourceType.Id})");
            resourceEntry.Add(resourceLabel);

            if (resourceData.resourceIcon != null)
            {
                var resourceIcon = new Image
                {
                    image = resourceData.resourceIcon.texture,
                    style =
                    {
                        width = 20,
                        height = 20
                    }
                };
                resourceEntry.Add(resourceIcon);
            }

            resourceList.Add(resourceEntry);
        }
    }
}
