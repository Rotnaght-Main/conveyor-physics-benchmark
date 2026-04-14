using System.Collections.Generic;
using System.Linq;
using Zenject;
using LoopSortTest.Core.Interfaces;

namespace LoopSortTest.Core.Services
{
    public class AlgorithmSwitcher : IAlgorithmSwitcher, IInitializable
    {
        [Inject] private readonly List<IPhysicsAlgorithm> _algorithms;

        private int _currentIndex;

        public IPhysicsAlgorithm Current => _algorithms[_currentIndex];
        public string CurrentName => Current.AlgorithmName;
        public int CurrentIndex => _currentIndex;
        public int AlgorithmCount => _algorithms.Count;
        public string[] AlgorithmNames => _algorithms.Select(a => a.AlgorithmName).ToArray();

        public void Initialize()
        {
            _currentIndex = 0;
        }

        public void Next()
        {
            _algorithms[_currentIndex].Dispose();
            _currentIndex = (_currentIndex + 1) % _algorithms.Count;
        }

        public void SetByIndex(int index)
        {
            if (index < 0 || index >= _algorithms.Count) return;
            if (index == _currentIndex) return;

            _algorithms[_currentIndex].Dispose();
            _currentIndex = index;
        }
    }
}
