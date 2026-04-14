using System.Collections.Generic;
using UnityEngine;
using Zenject;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;
using LoopSortTest.Config;

namespace LoopSortTest.Core.Services
{
    public class ConveyorSystem : ITickable, IInitializable
    {
        [Inject] private readonly ConveyorConfig _config;
        [Inject] private readonly ConveyorTrack _track;
        [Inject] private readonly IAlgorithmSwitcher _switcher;
        [Inject] private readonly ConveyorRenderer _renderer;

        private readonly List<ConveyorCube> _cubes = new();

        public List<ConveyorCube> Cubes => _cubes;

        public void Initialize()
        {
            SpawnCubes();
        }

        private void SpawnCubes()
        {
            _cubes.Clear();

            for (int i = 0; i < _config.CubeCount; i++)
            {
                float t = (float)i / Mathf.Max(_config.CubeCount, 1);
                Vector3 pos = _track.GetPositionAtT(t);
                pos.y = _config.CubeSize.y * 0.5f;

                var cube = new ConveyorCube
                {
                    Id = i,
                    Color = Color.HSVToRGB((float)i / Mathf.Max(_config.CubeCount, 1), 0.8f, 0.9f),
                    Position = pos,
                    PrevPosition = pos,
                    Velocity = Vector3.zero,
                    Rotation = Quaternion.identity,
                    Size = _config.CubeSize
                };
                _cubes.Add(cube);
            }
        }

        public void Tick()
        {
            _switcher.Current.Tick(_cubes, _track, _config, Time.deltaTime);
        }
    }
}
