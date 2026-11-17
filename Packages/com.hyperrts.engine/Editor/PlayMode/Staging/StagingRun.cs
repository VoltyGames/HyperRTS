using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Input.Cameras;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Common.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>One staging in Play: waits for the match to be live, runs the steps at their times and writes the report.</summary>
    internal sealed class StagingRun
    {
        private const double LiveTimeoutSeconds = 120.0;
        private const double RunTimeoutSeconds = 900.0;

        private readonly StagingFile _file;
        private readonly Dictionary<string, StagingGroup> _groups = new();
        private readonly List<(StagingActivity activity, StagingReport.Step step)> _active = new();
        private readonly ConsoleIssues _console = new();
        private readonly double _began = EditorApplication.timeSinceStartup;
        private bool _live;
        private float _liveTime;
        private int _next;
        private StagingReport.Step _current;

        public StagingRun(StagingFile file)
        {
            _file = file;
            Report = new StagingReport { name = file.Name, scene = file.Scene };
        }

        public StagingReport Report { get; }

        public string Folder => _file.Folder;

        public Vector2Int Resolution => _file.Resolution;

        public Camera Camera { get; private set; }

        public CameraController Controller { get; private set; }

        /// <summary>The group the camera keeps centred each frame, or null.</summary>
        public string Follow { get; set; }

        /// <summary>Seconds of game time since the match went live.</summary>
        public float GameTime => Time.time - _liveTime;

        public EntityManager World =>
            PlayWorld.TryGetAuthoritative(out var entityManager) ? entityManager : throw new InvalidOperationException("no running match world");

        public string Progress =>
            _live ? $"step {_next} of {_file.Steps.Count} at {GameTime:0.0} s of '{_file.Name}'" : $"'{_file.Name}' waiting for the match to go live";

        public void AddGroup(StagingGroup group) => _groups[group.Name] = group;

        public StagingGroup FindGroup(string name) =>
            _groups.TryGetValue(name, out var group) ? group : throw new InvalidOperationException($"group '{name}' was never spawned");

        /// <summary>The group's live units, failing the step named by <paramref name="verb"/> when none are left.</summary>
        public List<Entity> LiveUnits(string name, string verb)
        {
            var units = FindGroup(name).Alive(World);
            return units.Count > 0 ? units : throw new InvalidOperationException($"group '{name}' has no live units to {verb}");
        }

        public float2 GroupCentre(string name) =>
            FindGroup(name).TryGetCentre(World, out var centre) ? centre : throw new InvalidOperationException($"group '{name}' has no live units");

        public float3 GroundPoint(float2 point)
        {
            PlayWorld.TryGetSingleton(World, out TerrainHeight terrain);
            return new float3(point.x, terrain.Height(point), point.y);
        }

        public Entity FindPlayer(byte owner)
        {
            var entityManager = World;
            foreach (var player in PlayWorld.Players(entityManager))
            {
                if (entityManager.GetComponentData<Player>(player).Owner == owner)
                {
                    return player;
                }
            }

            throw new InvalidOperationException($"no player owns number {owner}");
        }

        /// <summary>Hands a running step's work over frames; its outcome reads "running" until the work ends.</summary>
        public void Start(StagingActivity activity)
        {
            if (activity.Records && _active.Any(running => running.activity.Records))
            {
                throw new InvalidOperationException("another capture is still recording; space the steps apart");
            }

            _current.outcome = "running";
            _active.Add((activity, _current));
        }

        /// <summary>Advances a frame; true when every step has run and finished. Throws when the match never goes live.</summary>
        public bool Tick()
        {
            if (!_live && !TryGoLive())
            {
                return false;
            }

            RunDueSteps();
            StepActivities();
            Recentre();
            return _next == _file.Steps.Count && _active.Count == 0;
        }

        /// <summary>Stops what is running, writes the report and the contact sheet, and returns the report path.</summary>
        public string End(string failure)
        {
            _console.Dispose();
            foreach (var (activity, step) in _active)
            {
                activity.Stop();
                step.outcome = "error: the run ended first";
            }

            Report.ended = failure == null ? "Done" : $"Failed: {failure}";
            var worldKept = PlayWorld.TryGetAuthoritative(out var entityManager);
            foreach (var group in _groups.Values)
            {
                var alive = worldKept ? group.Alive(entityManager).Count : 0;
                Report.groups.Add(new StagingReport.Group { name = group.Name, alive = alive, total = group.Total });
            }

            Report.consoleErrors.AddRange(_console.Issues);
            WriteSheet();
            var path = $"{Folder}/report.json";
            Directory.CreateDirectory(Folder);
            File.WriteAllText(path, JsonUtility.ToJson(Report, true));
            return path;
        }

        private bool TryGoLive()
        {
            var missing = WaitingFor();
            if (missing == null)
            {
                _live = true;
                _liveTime = Time.time;

                // A clean folder, so the sheet and report hold only this run.
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, true);
                }

                Directory.CreateDirectory(Folder);
                return true;
            }

            if (EditorApplication.timeSinceStartup - _began > LiveTimeoutSeconds)
            {
                throw new TimeoutException($"the match was not live after {LiveTimeoutSeconds:0} s: waiting for {missing}");
            }

            return false;
        }

        // The first thing the match still lacks, or null once it is live.
        // Keeps the match camera once it is found.
        private string WaitingFor()
        {
            if (!PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return "the match world";
            }

            var streamed = PlayWorld.TryGetSingleton(entityManager, out PrefabRegistry registry) && registry.ByTypeId.IsCreated
                && PlayWorld.TryGetSingleton(entityManager, out TerrainHeight _);
            if (!streamed)
            {
                return "the SubScene entities to stream in";
            }

            using var local = entityManager.CreateEntityQuery(typeof(Player), typeof(LocalPlayer));
            if (local.IsEmptyIgnoreFilter)
            {
                return "a local player";
            }

            Camera = UnityEngine.Camera.main;
            Controller = Camera != null ? Camera.GetComponent<CameraController>() : null;
            return Controller != null ? null : "the match camera";
        }

        private void RunDueSteps()
        {
            while (_next < _file.Steps.Count && _file.Steps[_next].At <= GameTime)
            {
                var step = _file.Steps[_next++];
                _current = new StagingReport.Step { index = step.Index, kind = step.Kind, at = step.At, ran = GameTime, outcome = "ok" };
                Report.steps.Add(_current);
                try
                {
                    step.Run(this);
                }
                catch (Exception exception)
                {
                    _current.outcome = $"error: {exception.Message}";
                    if (step.EndsRunOnError)
                    {
                        throw new InvalidOperationException($"step {step.Index} ({step.Kind}): {exception.Message}");
                    }
                }
            }
        }

        private void StepActivities()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var (activity, step) = _active[i];
                try
                {
                    if (!activity.Step(this))
                    {
                        continue;
                    }

                    step.outcome = "ok";
                }
                catch (Exception exception)
                {
                    activity.Stop();
                    step.outcome = $"error: {exception.Message}";
                }

                _active.RemoveAt(i);
            }
        }

        private void Recentre()
        {
            if (Follow == null)
            {
                return;
            }

            if (FindGroup(Follow).TryGetCentre(World, out var centre))
            {
                Controller.FocusOn(new Vector3(centre.x, 0f, centre.y));
                return;
            }

            Follow = null;
        }

        // Every shot, then a spread of each burst's frames, in step order.
        private void WriteSheet()
        {
            var sheet = new ContactSheet();
            foreach (var shot in Report.shots)
            {
                sheet.Add(shot);
            }

            foreach (var folder in Report.frames)
            {
                sheet.AddSpread(Directory.GetFiles(folder, "*.png").Select(file => file.Replace('\\', '/')).OrderBy(file => file).ToList(), 6);
            }

            var path = $"{Folder}/sheet.png";
            if (sheet.Write(path))
            {
                Report.sheet = path;
                Report.sheetTiles.AddRange(sheet.Images);
            }
        }
    }
}
