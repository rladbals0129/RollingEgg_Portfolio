using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RollingEgg
{
    public class MapService : MonoBehaviour, IMapService
    {
        private Map _currentMapInstance;

        public Map CurrentMapInstance => _currentMapInstance;

        public async UniTask InitializeAsync()
        {
            Debug.Log("[MapService] 초기화 시작...");
            await UniTask.Yield();
            Debug.Log("[MapService] 초기화 완료");
        }

        public Map CreateMapInstance(GameObject mapPrefab)
        {
            DestroyMapInstance();

            if (mapPrefab == null)
            {
                Debug.LogError("[MapService] 생성할 맵 프리팹이 비어 있습니다.");
                return null;
            }

            var spawned = Instantiate(mapPrefab);
            var mapInstance = spawned.GetComponent<Map>();
            if (mapInstance == null)
            {
                Debug.LogError("[MapService] 맵 프리팹에 Map 컴포넌트가 없습니다.");
                Destroy(spawned);
                return null;
            }

            _currentMapInstance = mapInstance;
            return mapInstance;
        }

        public void DestroyMapInstance()
        {
            if (_currentMapInstance == null)
                return;

            Destroy(_currentMapInstance.gameObject);
            _currentMapInstance = null;
        }
    }
}
