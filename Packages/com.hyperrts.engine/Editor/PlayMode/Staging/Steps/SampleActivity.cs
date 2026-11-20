using System.Globalization;
using System.IO;
using HyperRTS.Simulation.Common.Combat;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Writes a TSV row per rendered frame for the camera or each unit of a group.</summary>
    internal sealed class SampleActivity : StagingActivity
    {
        private readonly bool _camera;
        private readonly StagingGroup _group;
        private readonly float _until;
        private readonly StreamWriter _writer;
        private readonly StagingReport.Sample _result;
        private Pose? _first;
        private int _lastFrame = -1;

        public SampleActivity(StagingRun run, string what, float seconds)
        {
            _camera = what == SampleStep.CameraKey;
            _group = _camera ? null : run.FindGroup(what);
            _until = run.GameTime + seconds;
            var path = $"{run.Folder}/{what}.tsv";
            _writer = new StreamWriter(path);
            _writer.WriteLine(_camera ? "frame\ttime\tx\ty\tz\tpitch\tyaw\troll" : "frame\ttime\tunit\tx\ty\tz\thealth");
            _result = new StagingReport.Sample { what = what, path = path };
            run.Report.samples.Add(_result);
        }

        public override bool Advance(StagingRun run)
        {
            if (Time.frameCount != _lastFrame)
            {
                _lastFrame = Time.frameCount;
                Row(run);
            }

            if (run.GameTime < _until)
            {
                return false;
            }

            Stop();
            return true;
        }

        public override void Stop() => _writer.Dispose();

        private void Row(StagingRun run)
        {
            var head = $"{Time.frameCount}\t{Format(run.GameTime)}";
            if (_camera)
            {
                CameraRow(run.Camera.transform, head);
                return;
            }

            var entityManager = run.World;
            for (var i = 0; i < _group.Members.Count; i++)
            {
                var unit = _group.Members[i];
                if (!entityManager.Exists(unit) || !entityManager.HasComponent<LocalTransform>(unit))
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                var health = entityManager.HasComponent<Health>(unit) ? entityManager.GetComponentData<Health>(unit).Current : 0f;
                _writer.WriteLine($"{head}\t{i}\t{Format(position.x)}\t{Format(position.y)}\t{Format(position.z)}\t{Format(health)}");
            }
        }

        private void CameraRow(Transform camera, string head)
        {
            var pose = new Pose(camera.position, camera.rotation);
            _first ??= pose;
            _result.maxMove = Mathf.Max(_result.maxMove, Vector3.Distance(pose.position, _first.Value.position));
            _result.maxTurn = Mathf.Max(_result.maxTurn, Quaternion.Angle(pose.rotation, _first.Value.rotation));
            var angles = pose.rotation.eulerAngles;
            _writer.WriteLine($"{head}\t{Format(pose.position.x)}\t{Format(pose.position.y)}\t{Format(pose.position.z)}\t{Format(angles.x)}\t{Format(angles.y)}\t{Format(angles.z)}");
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
