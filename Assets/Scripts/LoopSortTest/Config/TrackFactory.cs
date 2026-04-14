using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Config
{
    public static class TrackFactory
    {
        public static ConveyorTrack CreateOval(ConveyorConfig config)
        {
            var waypoints = new List<Vector3>();
            float halfW = config.OvalWidth * 0.5f;
            float halfH = config.OvalHeight * 0.5f;

            for (int i = 0; i < config.WaypointCount; i++)
            {
                float angle = (float)i / config.WaypointCount * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * halfW;
                float z = Mathf.Sin(angle) * halfH;
                waypoints.Add(new Vector3(x, 0f, z));
            }

            return new ConveyorTrack(waypoints, config.BeltWidth);
        }
    }
}
