using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RollingEgg.Core
{
    public interface IRunningService
    {
        UniTask InitializeAsync();
        UniTask OnStartRunning(int eggId, int stageNumber);
        UniTask OnReStartRunning();
        UniTask OnNextStage();

        UniTask<bool> PrepareRunningInstancesAsync(int eggId, int stageNumber);
        UniTask StartCountdownAsync();

        void PauseRunning();
        void ResumeRunning();

        void Dispose();
        bool IsMaxStage();
        void SetRunningEgg(int eggId, string eggType);

        ColorKeyMapping GetCurrentColorKeyMapping();

        int CurrentEggId { get; }
        string CurrentEggType { get; }
        int CurrentStageId { get; }
    }
}