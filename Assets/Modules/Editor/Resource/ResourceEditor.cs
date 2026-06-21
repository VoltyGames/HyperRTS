using System.Collections.Generic;
using System.IO;
using HyperRTS.Simulation.Resources;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Resource
{
    /// <summary>Editor window to create, edit and delete resource definition assets.</summary>
    public class ResourcesEditor : EditorWindow
    {
        private const string ResourceAssetsPath = "Assets/ScriptableObjects/Resources";
        private const string UxmlPath = "Assets/Modules/Editor/Resource/ResourceEditor.uxml";

        private readonly List<ResourceData> resources = new();
        private ResourceData selected;

        private TextField idField;
        private TextField nameField;
        private ObjectField iconField;
        private ListView listView;
        private Button saveButton;
        private Button removeButton;

        [MenuItem("HyperRTS/Resource Editor")]
        public static void ShowWindow()
        {
            GetWindow<ResourcesEditor>().titleContent = new GUIContent("Resource Editor");
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label($"Resource Editor layout not found at '{UxmlPath}'."));
                return;
            }

            rootVisualElement.Add(tree.Instantiate());

            idField = rootVisualElement.Q<TextField>("resourceIdField");
            nameField = rootVisualElement.Q<TextField>("resourceNameField");
            iconField = rootVisualElement.Q<ObjectField>("resourceIconField");
            iconField.objectType = typeof(Sprite);

            saveButton = rootVisualElement.Q<Button>("saveResourceButton");
            removeButton = rootVisualElement.Q<Button>("removeResourceButton");
            rootVisualElement.Q<Button>("addResourceButton").clicked += AddResource;
            saveButton.clicked += SaveResource;
            removeButton.clicked += RemoveResource;

            listView = rootVisualElement.Q<ListView>("resourcesList");
            listView.itemsSource = resources;
            listView.selectionChanged += _ => Select(listView.selectedItem as ResourceData);

            Reload();
            Select(null);
        }

        private void AddResource()
        {
            var id = idField.value;
            if (string.IsNullOrEmpty(id))
            {
                EditorUtility.DisplayDialog("Error", "Resource ID cannot be empty.", "OK");
                return;
            }

            var path = $"{ResourceAssetsPath}/{id}.asset";
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", $"Resource with the ID '{id}' already exists.", "OK");
                return;
            }

            // An icon is optional, but missing it is usually a mistake - confirm before continuing.
            var icon = iconField.value as Sprite;
            if (icon == null &&
                !EditorUtility.DisplayDialog("Missing icon",
                    $"Resource '{id}' has no icon assigned. Add it anyway?", "Add", "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(ResourceAssetsPath);
            var data = ResourceData.Create(ResourceManager.CreateResource(id, nameField.value), icon);
            AssetDatabase.CreateAsset(data, path);
            Persist();
            Select(null);
        }

        private void SaveResource()
        {
            if (selected == null)
            {
                return;
            }

            selected.resourceId = idField.value;
            selected.resourceName = nameField.value;
            selected.resourceIcon = iconField.value as Sprite;
            ResourceManager.UpdateOrCreateResource(selected.resourceId, selected.resourceName);

            EditorUtility.SetDirty(selected);
            Persist();
        }

        private void RemoveResource()
        {
            if (selected == null)
            {
                return;
            }

            ResourceManager.RemoveResource(selected.resourceId);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selected));
            Persist();
            Select(null);
        }

        // Persist asset changes, then re-sync the list (no Refresh - the AssetDatabase calls update it in-process).
        private void Persist()
        {
            AssetDatabase.SaveAssets();
            Reload();
        }

        // Load resource assets from disk into the list, registering each with the runtime manager.
        private void Reload()
        {
            resources.Clear();

            if (Directory.Exists(ResourceAssetsPath))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:ResourceData", new[] { ResourceAssetsPath }))
                {
                    var data = AssetDatabase.LoadAssetAtPath<ResourceData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (data == null)
                    {
                        continue;
                    }

                    ResourceManager.CreateResource(data.resourceId, data.resourceName);
                    resources.Add(data);
                }
            }

            listView.RefreshItems();
        }

        // Bind a resource to the form; null clears it and hides the edit buttons.
        private void Select(ResourceData data)
        {
            selected = data;
            idField.value = data != null ? data.resourceId : string.Empty;
            nameField.value = data != null ? data.resourceName : string.Empty;
            iconField.value = data != null ? data.resourceIcon : null;
            saveButton.visible = removeButton.visible = data != null;
        }
    }
}
