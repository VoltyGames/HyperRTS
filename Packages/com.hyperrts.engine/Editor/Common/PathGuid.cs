using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common;

/// <summary>
/// The GUID derived from an asset's project path, so build output kept out of version control gets the same GUID
/// in every clone and rebuild, and tracked assets can point at it.
/// </summary>
public readonly struct PathGuid
{
    private const string SceneImporter =
        "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

    private const string AssetImporter =
        "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

    public PathGuid(string path)
    {
        Path = path.Replace('\\', '/');
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Path));
        Value = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    public string Path { get; }

    /// <summary>The MD5 of the path as 32 lowercase hex digits.</summary>
    public string Value { get; }

    /// <summary>Saves the scene at the path: a new file takes the derived GUID, an existing one keeps its own.</summary>
    public void SaveScene(Scene scene)
    {
        if (!File.Exists(Path))
        {
            WriteMeta(SceneImporter);
        }

        EditorSceneManager.SaveScene(scene, Path);
    }

    /// <summary>Creates a <typeparamref name="T"/> at the free path under the derived GUID.</summary>
    public T Create<T>() where T : ScriptableObject
    {
        if (File.Exists(Path))
        {
            throw new InvalidOperationException($"HyperRTS: {Path} already exists.");
        }

        // AssetDatabase.CreateAsset ignores a .meta written before it, so the file is written and imported by hand.
        var asset = ScriptableObject.CreateInstance<T>();
        InternalEditorUtility.SaveToSerializedFileAndForget([asset], Path, true);
        Object.DestroyImmediate(asset);
        WriteMeta(AssetImporter);
        AssetDatabase.ImportAsset(Path);
        return AssetDatabase.LoadAssetAtPath<T>(Path);
    }

    private void WriteMeta(string importer) =>
        File.WriteAllText(Path + ".meta", $"fileFormatVersion: 2\nguid: {Value}\n{importer}");
}
