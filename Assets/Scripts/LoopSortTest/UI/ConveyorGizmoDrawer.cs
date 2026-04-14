using UnityEngine;
using Zenject;
using LoopSortTest.Config;
using LoopSortTest.Core.Models;
using LoopSortTest.Core.Services;

namespace LoopSortTest.UI
{
    public class ConveyorGizmoDrawer : MonoBehaviour
    {
        [Inject] private ConveyorConfig _config;
        [Inject] private ConveyorTrack _track;
        [Inject] private ConveyorSystem _system;

        private void OnDrawGizmos()
        {
            if (_config == null || !_config.DrawGizmos) return;
            if (_track == null) return;

            // Draw track centerline
            Gizmos.color = Color.gray;
            var waypoints = _track.Waypoints;
            for (int i = 0; i < waypoints.Count; i++)
            {
                int next = (i + 1) % waypoints.Count;
                Gizmos.DrawLine(waypoints[i], waypoints[next]);
            }

            // Draw belt boundaries (inner + outer walls)
            _track.GetBoundaryPoints(out var inner, out var outer);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            for (int i = 0; i < inner.Count; i++)
            {
                int next = (i + 1) % inner.Count;
                Gizmos.DrawLine(inner[i], inner[next]);
                Gizmos.DrawLine(outer[i], outer[next]);
            }

            // Draw cube positions
            if (_system == null) return;
            foreach (var cube in _system.Cubes)
            {
                Gizmos.color = cube.Color;
                Gizmos.DrawWireCube(cube.Position, cube.Size);

                // Velocity indicator
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(cube.Position, cube.Velocity * 0.3f);
            }
        }
    }
}
