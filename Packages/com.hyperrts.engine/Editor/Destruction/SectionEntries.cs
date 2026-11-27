using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Destruction;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Destruction;

/// <summary>
/// Turns a model's imported <see cref="ModelSections"/> into the authoring entries of the <see cref="SectionsAuthoring"/>
/// that holds the model.
/// </summary>
internal sealed class SectionEntries
{
    private readonly ModelSections _data;
    private readonly SectionsAuthoring _owner;

    public SectionEntries(ModelSections data, SectionsAuthoring owner)
    {
        _data = data;
        _owner = owner;
    }

    /// <summary>
    /// The sections as authoring entries, each box measured by <paramref name="measure"/>. Share, deck and
    /// needs-all-supports stay from the owner's entry for the same model. Holed, pieces, stub and heap models are
    /// set to scale 0. A section's parts are the model's moving parts on it and the team markers fixed to it.
    /// </summary>
    public List<SectionEntry> Build(Func<Transform, Bounds> measure)
    {
        var sections = _data.sections;
        var intacts = sections.ConvertAll(part => FindModel(part.intact));
        var boxes = intacts.ConvertAll(intact => measure(intact.transform));
        var all = boxes[0];
        foreach (var box in boxes)
        {
            all.Encapsulate(box);
        }

        var markers = new SectionMarkers(_owner, _data).Find(boxes, measure);
        var found = new List<SectionEntry>(sections.Count);
        for (var i = 0; i < sections.Count; i++)
        {
            var entry = Entry(sections[i], intacts[i], boxes[i], all.center);
            var index = i;
            entry.parts = entry.parts.Concat(markers.Where(marker => marker.Section == index).Select(marker => marker.Marker)).ToArray();
            found.Add(entry);
        }

        return found;
    }

    private SectionEntry Entry(ModelSections.Section part, GameObject intact, Bounds box, Vector3 middle)
    {
        var keptAt = Array.FindIndex(_owner.sections, item => item.intact == intact);
        var outward = part.exterior
            ? _owner.transform.InverseTransformDirection(intact.transform.TransformDirection(part.outward)).normalized
            : (Vector3)SectionsFill.Outward(box.center, middle);
        var entry = new SectionEntry
        {
            intact = intact,
            center = box.center,
            halfSize = box.extents,
            outward = outward,
            material = FindMaterial(part.material),
            floor = part.floor,
            anchored = part.anchored,
            exterior = part.exterior,
            parts = _data.parts.Where(moving => moving.section == part.name).Select(moving => FindModel(moving.node)).ToArray(),
            weapons = Weapons(part.name),
            supports = SectionModels(part.supports),
            rests = SectionModels(part.rests),
            pieces = Hidden(part.pieces),
            stub = Hidden(part.stub),
            heap = Hidden(part.heap),
            holes = Holes(part, intact),
        };
        if (keptAt >= 0)
        {
            var kept = _owner.sections[keptAt];
            entry.share = kept.share;
            entry.deck = kept.deck;
            entry.needsAllSupports = kept.needsAllSupports;
        }
        else
        {
            entry.share = SectionsFill.Share(box.size);
        }

        return entry;
    }

    private GameObject[] SectionModels(string[] names) =>
        names.Select(name => FindModel(_data.sections.Find(other => other.name == name).intact)).ToArray();

    private SectionsAuthoring.Hole[] Holes(ModelSections.Section part, GameObject intact) =>
        part.holed.Select(name => new SectionsAuthoring.Hole
        {
            model = Hidden(name),
            center = _owner.transform.InverseTransformPoint(intact.transform.TransformPoint(_data.holes.Find(hole => hole.model == name).center)),
        }).ToArray();

    // The mounts whose muzzle, turret or barrel sit under one of the section's moving parts.
    private int[] Weapons(string sectionName)
    {
        var carriers = _data.parts.Where(moving => moving.section == sectionName).Select(moving => FindModel(moving.node).transform).ToList();
        if (!_owner.TryGetComponent<WeaponAuthoring>(out var weapons))
        {
            return Array.Empty<int>();
        }

        var mounts = new List<int>();
        for (var m = 0; m < weapons.WeaponCount; m++)
        {
            if (weapons.MountStats(m).AimPoints().Any(point => carriers.Any(point.IsChildOf)))
            {
                mounts.Add(m);
            }
        }

        return mounts.ToArray();
    }

    private GameObject Hidden(string modelName)
    {
        var model = FindModel(modelName);
        model.transform.localScale = Vector3.zero;
        return model;
    }

    // Only the nodes of this model count, so dressing or a base prefab's child with the same name can't be taken.
    private GameObject FindModel(string modelName)
    {
        var modelPath = AssetDatabase.GetAssetPath(_data);
        GameObject found = null;
        foreach (var child in _owner.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != modelName)
            {
                continue;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
            if (source == null || AssetDatabase.GetAssetPath(source) != modelPath)
            {
                continue;
            }

            if (found != null)
            {
                throw new InvalidOperationException($"HyperRTS: '{_owner.name}' has two objects named '{modelName}'.");
            }

            found = child.gameObject;
        }

        return found.OrThrow($"HyperRTS: '{_owner.name}' has no object named '{modelName}' from its model's sections.");
    }

    private SectionMaterial FindMaterial(string materialName)
    {
        foreach (var item in _owner.materialNames)
        {
            if (item.name == materialName && item.material != null)
            {
                return item.material;
            }
        }

        throw new InvalidOperationException($"HyperRTS: sections on '{_owner.name}' have no material for the model's '{materialName}'.");
    }
}
