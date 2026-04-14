using LoopSortTest.Core.Interfaces;

namespace LoopSortTest.Core.Interfaces
{
    public interface IAlgorithmSwitcher
    {
        IPhysicsAlgorithm Current { get; }
        string CurrentName { get; }
        int CurrentIndex { get; }
        int AlgorithmCount { get; }
        string[] AlgorithmNames { get; }
        void Next();
        void SetByIndex(int index);
    }
}
