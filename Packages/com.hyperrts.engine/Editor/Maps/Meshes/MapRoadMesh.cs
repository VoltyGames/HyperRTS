using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes;

/// <summary>
/// One road class's ribbons over the terrain as one mesh. UV X runs across the road, UV Y is metres along it, and
/// vertex alpha fades the ends.
/// </summary>
public sealed class MapRoadMesh
{
    private const float Step = 2f;
    public const float Lift = 0.05f;
    private const float FadeLength = 4f;

    private const float AcrossStep = 1.5f;
    private const float SpillStep = 0.5f;
    private const float SpillIn = 3f;
    private const float SpillOut = 2f;

    private readonly MapHeights _heights;
    private readonly float _groundLevel;

    /// <summary><paramref name="groundLevel"/> is the terrain height of world y = 0.</summary>
    public MapRoadMesh(MapHeights heights, float groundLevel)
    {
        _heights = heights;
        _groundLevel = groundLevel;
    }

    public List<Vector3> Vertices { get; } = [];

    public List<Vector3> Normals { get; } = [];

    public List<Vector4> Tangents { get; } = [];

    public List<Vector2> Uvs { get; } = [];

    public List<Color32> Colors { get; } = [];

    public List<int> Triangles { get; } = [];

    public int Count => Vertices.Count;

    /// <summary>
    /// Adds a road's ribbon. Rows whose centre is <paramref name="hidden"/> (water, bridge) are left out and
    /// break the ribbon, fading at the break.
    /// </summary>
    public void AddRibbon(MapRoad road, bool fadeStart, bool fadeEnd, Func<Vector2, bool> hidden)
    {
        var points = MapCurves.Resample(road.Line.Points, Step);
        var along = Distances(points);
        var shown = points.ConvertAll(p => !hidden(p));
        var last = points.Count - 1;
        for (var first = 0; first <= last; first++)
        {
            if (!shown[first])
            {
                continue;
            }

            var end = first;
            while (end < last && shown[end + 1])
            {
                end++;
            }

            var run = (first, end, first > 0 || fadeStart, end < last || fadeEnd);
            AddRun(points, along, run, road.Width);
            first = end;
        }
    }

    /// <summary>
    /// Adds the stretch of a road's end that meets a higher-ranked road, drawn over that road: solid at its edge,
    /// fading out onto it and back out along the lower road, so the surface blends across the join.
    /// </summary>
    public void AddSpill(MapRoad road, MapRoad higher, bool atStart)
    {
        var points = MapCurves.Resample(road.Line.Points, SpillStep);
        var along = Distances(points);
        var half = higher.Width * 0.5f;
        var last = points.Count - 1;
        var reach = 0;
        while (reach < last && higher.Line.Distance(points[atStart ? reach : last - reach]) < half + SpillOut)
        {
            reach++;
        }

        var (first, end) = atStart ? (0, reach) : (last - reach, last);
        AddStrip(points, along, (first, end), road.Width, i =>
        {
            var distance = higher.Line.Distance(points[i]);
            var onto = Mathf.Clamp01((distance - (half - SpillIn)) / SpillIn);
            var alongRoad = Mathf.Clamp01((half + SpillOut - distance) / SpillOut);
            return Mathf.Min(onto, alongRoad);
        });
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.SetTangents(Tangents);
        mesh.SetUVs(0, Uvs);
        mesh.SetColors(Colors);
        mesh.SetTriangles(Triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>One unbroken stretch; <paramref name="along"/> is metres along the road per row.</summary>
    private void AddRun(List<Vector2> points, List<float> along, (int First, int Last, bool In, bool Out) run,
        float width)
    {
        AddStrip(points, along, (run.First, run.Last), width, i => Alpha(along, run, i));
    }

    /// <summary>Rows <paramref name="span"/> of the points as one strip of quads, each row's vertex alpha from <paramref name="alphaAt"/>.</summary>
    private void AddStrip(List<Vector2> points, List<float> along, (int First, int Last) span, float width,
        Func<int, float> alphaAt)
    {
        var columns = Mathf.Max(3, Mathf.CeilToInt(width / AcrossStep) + 1);
        var previous = -1;
        for (var i = span.First; i <= span.Last; i++)
        {
            var ahead = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
            var row = Count;
            AddRow(points[i], ahead.normalized, width, columns, along[i], alphaAt(i));
            if (previous >= 0)
            {
                AddQuads(previous, row, columns);
            }

            previous = row;
        }
    }

    /// <summary>Vertex alpha at a row: clear at a fading end, solid from <see cref="FadeLength"/> in.</summary>
    private static float Alpha(List<float> along, (int First, int Last, bool In, bool Out) run, int row)
    {
        var fromStart = run.In ? (along[row] - along[run.First]) / FadeLength : 1f;
        var fromEnd = run.Out ? (along[run.Last] - along[row]) / FadeLength : 1f;
        return Mathf.Clamp01(Mathf.Min(fromStart, fromEnd));
    }

    private static List<float> Distances(List<Vector2> points)
    {
        var along = new List<float>(points.Count) { 0f };
        for (var i = 1; i < points.Count; i++)
        {
            along.Add(along[i - 1] + (points[i] - points[i - 1]).magnitude);
        }

        return along;
    }

    private void AddRow(Vector2 center, Vector2 forward, float width, int columns, float along, float alpha)
    {
        var right = MapGeometry.Right(forward);
        var fade = (byte)Mathf.RoundToInt(alpha * 255f);
        for (var c = 0; c < columns; c++)
        {
            var across = (float)c / (columns - 1);
            var point = center + right * ((across - 0.5f) * width);
            var normal = _heights.Normal(point);
            var side = new Vector3(right.x, 0f, right.y);
            var tangent = (side - normal * Vector3.Dot(side, normal)).normalized;
            Vertices.Add(new Vector3(point.x, _heights.Height(point) - _groundLevel + Lift, point.y));
            Normals.Add(normal);
            Tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1f));
            Uvs.Add(new Vector2(across, along));
            Colors.Add(new Color32(255, 255, 255, fade));
        }
    }

    /// <summary>Two triangles per cell, clockwise seen from above.</summary>
    private void AddQuads(int previous, int row, int columns)
    {
        for (var c = 0; c + 1 < columns; c++)
        {
            var a = previous + c;
            var b = row + c;
            Triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
        }
    }
}
