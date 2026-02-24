using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RollingEgg
{
    public interface IPlayerService
    {
        UniTask InitializeAsync();
        PlayerController CreatePlayerInstance();
        void DestroyPlayerInstance();
        PlayerController CurrentPlayer { get; }
        GameObject PlayerPrefab { get; }
    }
}





