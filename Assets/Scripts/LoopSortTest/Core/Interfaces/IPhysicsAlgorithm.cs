using System.Collections.Generic;
using LoopSortTest.Core.Models;
using LoopSortTest.Config;

namespace LoopSortTest.Core.Interfaces
{
    public interface IPhysicsAlgorithm
    {
        string AlgorithmName { get; }

        /// <summary>
        /// Tek bir fizik tick'i.
        /// Belt sürtünmesi, küp-küp çarpışması ve sınır kısıtlarını uygular.
        /// </summary>
        void Tick(List<ConveyorCube> cubes, ConveyorTrack track, ConveyorConfig config, float dt);

        void Dispose();
    }
}
