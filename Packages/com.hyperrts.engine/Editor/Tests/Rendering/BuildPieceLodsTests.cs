using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Tests.Rendering;

/// <summary>A coarser level's triangle that spans two construction pieces waits whole for the later one; the finest level stays as it was.</summary>
public class BuildPieceLodsTests
{
    private static readonly Vector2 Early = new(0.2f, 1f);
    private static readonly Vector2 Late = new(0.6f, 1f);

    [Test]
    public void KeepPiecesWhole_GivesASpanningTriangleTheLaterPiecesOrder()
    {
        var mesh = TwoPieces();
        BuildPieceLods.KeepPiecesWhole(mesh);

        var orders = new List<Vector2>();
        mesh.GetUVs(3, orders);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, mesh.GetIndices(0, 0, true));
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, mesh.GetIndices(1, 0, true));
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, mesh.GetIndices(1, 1, true));
        var coarse = mesh.GetIndices(0, 1, true);
        Assert.IsTrue(coarse.All(corner => orders[corner] == Late));
        Assert.AreEqual(9, mesh.vertexCount);
        Object.DestroyImmediate(mesh);
    }

    // Piece one is vertices 0 to 2, piece two 3 to 5; the first sub-mesh's coarse level welds them into one triangle.
    private static Mesh TwoPieces()
    {
        var mesh = new Mesh();
        mesh.SetVertices(new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
            new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(1f, 1f, 0f),
        });
        mesh.SetUVs(3, new List<Vector2> { Early, Early, Early, Late, Late, Late });
        mesh.subMeshCount = 2;
        mesh.SetIndices(new[] { 0, 1, 2, 3, 4, 5, 0, 4, 2 }, MeshTopology.Triangles, 0);
        mesh.SetIndices(new[] { 3, 4, 5, 3, 4, 5 }, MeshTopology.Triangles, 1);
        mesh.lodCount = 2;
        mesh.SetLods(new[]
        {
            new MeshLodRange { indexStart = 0, indexCount = 6 },
            new MeshLodRange { indexStart = 6, indexCount = 3 },
        }, 0);
        mesh.SetLods(new[]
        {
            new MeshLodRange { indexStart = 0, indexCount = 3 },
            new MeshLodRange { indexStart = 3, indexCount = 3 },
        }, 1);
        return mesh;
    }
}
