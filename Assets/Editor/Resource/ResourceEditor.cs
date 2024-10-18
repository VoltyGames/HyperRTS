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
        private const string ResourceAssetsPath = "Assets/ScritableObjects/Resources";
        private ScrollView resourcesList;
        
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
            resourcesList = root.Q<ScrollView>("resourcesList");

            if (resourcesList is null)
            {
                Debug.LogError("Failed to find resourcesList ScrollView in UXML.");
                return;
            }

            // Load existing resources into the ScrollView
            EnsureResourceDirectoryExists();
            LoadRegisteredResources();

            // Button click handler for adding a new resource
            addResourceButton.clicked += () =>
            {
                var resourceName = resourceNameField.value;
                var resourceIcon = resourceIconField.value as Sprite;

                if (!string.IsNullOrEmpty(resourceName))
                {
                    AddResource(resourceName, resourceIcon);
                    resourceNameField.value = ""; // Clear the input field
                    resourceIconField.value = null; // Clear the icon field
                }
            };
        }

        /// <summary>
        /// Add a new resource to the registry and UI.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="resourceIcon"></param>
        private void AddResource(string resourceName, Sprite resourceIcon)
        {
            // Register the new resource type
            var newResource = ResourceRegistry.RegisterResourceType(resourceName);

            // Create a ResourceData ScriptableObject to hold data
            var resourceData = CreateInstance<ResourceData>();
            resourceData.name = resourceName;
            resourceData.resourceType = newResource;
            resourceData.resourceIcon = resourceIcon;
            
            var path = $"{ResourceAssetsPath}/{resourceName}.asset";
            SaveResourceAsAsset(resourceData, path);

            // Add the new resource to the UI
            var resourceLabel = new Label($"- {newResource.Name} (ID: {newResource.Id})");
            resourcesList.Add(resourceLabel);
        }
        
        /// <summary>
        /// Save a ResourceData instance as an asset at the specified path.
        /// </summary>
        /// <param name="resourceData"></param>
        /// <param name="path"></param>
        private void SaveResourceAsAsset(ResourceData resourceData, string path)
        {
            var directoryPath = Path.GetDirectoryName(path);

            if (directoryPath is not null)
            {
                // Ensure the directory exists
                Directory.CreateDirectory(directoryPath);
            }

            // Save the ScriptableObject as an asset
            AssetDatabase.CreateAsset(resourceData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Load all registered resources from the Resources folder and add them to the UI.
        /// </summary>
        private void LoadRegisteredResources()
        {
            resourcesList.Clear();
            var resourcePaths = Directory.GetFiles(ResourceAssetsPath, "*.asset");

            foreach (var path in resourcePaths)
            {
                var resourceData = AssetDatabase.LoadAssetAtPath<ResourceData>(path);
                
                if (resourceData is not null)
                {
                    AddResourceToUI(resourceData);
                }
            }
        }

        /// <summary>
        /// Add a ResourceData instance to the UI.
        /// </summary>
        /// <param name="resourceData">
        /// The ResourceData instance to add to the UI.
        /// </param>
        private void AddResourceToUI(ResourceData resourceData)
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

            resourcesList.Add(resourceEntry);
        }

        private void EnsureResourceDirectoryExists()
        {
            if (!Directory.Exists(ResourceAssetsPath))
            {
                Directory.CreateDirectory(ResourceAssetsPath);
            }
        }
    }
}
