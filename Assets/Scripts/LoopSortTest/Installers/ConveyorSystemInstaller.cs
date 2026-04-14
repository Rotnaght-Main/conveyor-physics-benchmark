using UnityEngine;
using Zenject;
using LoopSortTest.Algorithms;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;
using LoopSortTest.Core.Services;

namespace LoopSortTest.Installers
{
    public class ConveyorSystemInstaller : MonoInstaller
    {
        [SerializeField] private ConveyorConfig _config;

        public override void InstallBindings()
        {
            // Config
            Container.BindInstance(_config).AsSingle();

            // Track
            Container.Bind<ConveyorTrack>()
                .FromMethod(_ => TrackFactory.CreateOval(_config))
                .AsSingle();

            // Algorithms
            Container.Bind<IPhysicsAlgorithm>().To<SpatialHashPhysics>().AsSingle();
            Container.Bind<IPhysicsAlgorithm>().To<AABBPhysics>().AsSingle();
            Container.Bind<IPhysicsAlgorithm>().To<SATPhysics>().AsSingle();
            Container.Bind<IPhysicsAlgorithm>().To<CircleApproxPhysics>().AsSingle();
            Container.Bind<IPhysicsAlgorithm>().To<VerletPhysics>().AsSingle();

            // Services
            Container.BindInterfacesAndSelfTo<AlgorithmSwitcher>().AsSingle();
            Container.BindInterfacesAndSelfTo<ConveyorRenderer>().AsSingle();

            // Main system
            Container.BindInterfacesAndSelfTo<ConveyorSystem>().AsSingle();
        }
    }
}
