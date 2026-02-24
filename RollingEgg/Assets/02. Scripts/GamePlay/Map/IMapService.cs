using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RollingEgg
{
    public interface IMapService
    {
        UniTask InitializeAsync();
        Map CreateMapInstance(GameObject mapPrefab);
        void DestroyMapInstance();
        Map CurrentMapInstance { get; }
    }
}
