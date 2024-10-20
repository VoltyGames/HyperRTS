using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private readonly List<ResourceData> resourcesDataList = new();
        private ResourceData selectedResourceData;

        private TextField resourceIdField;
        private TextField resourceNameField;
        private ObjectField resourceIconField;
        private Button addResourceButton;
        private Button removeResourceButton;
        private Button saveResourceButton;

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
            // Load the UXML file
            rootVisualElement.Add(uxmlTree.Instantiate());

            // Get references to UI elements
            resourceIdField = rootVisualElement.Q<TextField>("resourceIdField");
            resourceNameField = rootVisualElement.Q<TextField>("resourceNameField");
            resourceIconField = rootVisualElement.Q<ObjectField>("resourceIconField");
            addResourceButton = rootVisualElement.Q<Button>("addResourceButton");
            removeResourceButton = rootVisualElement.Q<Button>("removeResourceButton");
            saveResourceButton = rootVisualElement.Q<Button>("saveResourceButton");
            var resourcesListView = rootVisualElement.Q<ListView>("resourcesList");

            resourceIconField.objectType = typeof(Sprite);
            resourcesListView.itemsSource = resourcesDataList;
            resourcesListView.selectionChanged += OnResourceSelected;
            addResourceButton.clicked += AddResource;
            removeResourceButton.clicked += RemoveResource;
            saveResourceButton.clicked += SaveResource;

            // Load existing resources into the ScrollView
            LoadRegisteredResources();
        }

        /// <summary>
        /// Add a new resource to the registry and UI.
        /// </summary>
        private void AddResource()
        {
            EnsureResourceDirectoryExists();
            
            var resourceId = resourceIdField.value;
            var resourceName = resourceNameField.value;
            var resourceIcon = resourceIconField.value as Sprite;
            var path = $"{ResourceAssetsPath}/{resourceId}.asset";

            if (string.IsNullOrEmpty(resourceId))
            {
                EditorUtility.DisplayDialog("Error", "Resource ID cannot be empty.", "OK");
                return;
            }

            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", $"Resource with the ID '{resourceId}' already exists.", "OK");
                return;
            }
            
            // Register the new resource type
            var newResource = ResourceManager.CreateResource(resourceId, resourceName);
            var resourceData = ResourceData.Create(newResource, resourceIcon);
            resourcesDataList.Add(resourceData);
            
            // Save the asset file
            AssetDatabase.CreateAsset(resourceData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ResetForm();
        }

        private void SaveResource()
        {
            if (selectedResourceData is null)
            {
                return;
            }

            selectedResourceData.resourceId = resourceIdField.value;
            selectedResourceData.resourceName = resourceNameField.value;
            selectedResourceData.resourceIcon = resourceIconField.value as Sprite;

            ResourceManager.UpdateOrCreateResource(selectedResourceData.resourceId, selectedResourceData.resourceName);

            // Save the changes back to the asset file
            EditorUtility.SetDirty(selectedResourceData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Method to remove the selected resource from the list and delete the asset file
        private void RemoveResource()
        {
            if (selectedResourceData is null)
            {
                return;
            }

            // Remove from the list
            resourcesDataList.Remove(selectedResourceData);
            ResourceManager.RemoveResource(selectedResourceData.resourceId);

            // Delete the asset file
            var path = AssetDatabase.GetAssetPath(selectedResourceData);
            AssetDatabase.DeleteAsset(path);
            ResetForm();
        }

        /// <summary>
        /// Load all registered resources from the Resources folder and add them to the UI.
        /// </summary>
        private void LoadRegisteredResources()
        {
            if (!Directory.Exists(ResourceAssetsPath))
            {
                return;
            }
            
            resourcesDataList.Clear();
            var resourcePaths = Directory.GetFiles(ResourceAssetsPath, "*.asset");

            foreach (var path in resourcePaths)
            {
                var resourceData = AssetDatabase.LoadAssetAtPath<ResourceData>(path);

                if (resourceData is not null)
                {
                    Debug.Log($"Loaded resource: {resourceData.resourceId} - {resourceData.resourceName}");
                    ResourceManager.CreateResource(resourceData.resourceId, resourceData.resourceName);
                    resourcesDataList.Add(resourceData);
                }
            }
        }

        private void OnResourceSelected(IEnumerable<object> selectedItems)
        {
            // Get the selected resource data
            selectedResourceData = selectedItems.FirstOrDefault() as ResourceData;

            if (selectedResourceData is null)
            {
                return;
            }

            // Bind the selected resource data to the UI fields
            resourceIdField.value = selectedResourceData.resourceId;
            resourceNameField.value = selectedResourceData.resourceName;
            resourceIconField.value = selectedResourceData.resourceIcon;
            
            // Update the UI buttons
            saveResourceButton.visible = true;
            removeResourceButton.visible = true;
        }

        private void ResetForm()
        {
            resourceIconField.value = null;
            selectedResourceData = null;
            resourceNameField.value = "";
            resourceIdField.value = "";
            saveResourceButton.visible = false;
            removeResourceButton.visible = false;
        }

        private static void EnsureResourceDirectoryExists()
        {
            if (!Directory.Exists(ResourceAssetsPath))
            {
                Directory.CreateDirectory(ResourceAssetsPath);
            }
        }
    }
}
