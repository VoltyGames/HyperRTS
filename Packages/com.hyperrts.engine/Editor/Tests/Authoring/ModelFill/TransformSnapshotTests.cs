using HyperRTS.Editor.Authoring.ModelFill;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Authoring.ModelFill;

/// <summary>A model fill check puts back what a fill moved or hid under the component, such as a section model it scaled to 0.</summary>
public class TransformSnapshotTests
{
    [Test]
    public void Restore_PutsBackAHiddenChild()
    {
        var root = new GameObject("Building");
        try
        {
            var model = new GameObject("Section").transform;
            model.SetParent(root.transform);
            model.SetLocalPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            model.localScale = new Vector3(1f, 2f, 3f);
            var snapshot = new TransformSnapshot(root.transform);

            model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.zero;
            snapshot.Restore();

            Assert.AreEqual(new Vector3(1f, 2f, 3f), model.localPosition);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), model.localScale);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), model.localRotation), Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
