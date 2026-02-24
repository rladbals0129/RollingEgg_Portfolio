using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using UnityEngine;

namespace RollingEgg
{
    public class PlayerService : MonoBehaviour, IPlayerService
    {
        private const string PlayerPrefabPath = "Assets/03. Prefabs/Player.prefab";

        private GameObject _playerPrefab;
        private PlayerController _currentPlayer;
        private IResourceService _resourceService;

        public PlayerController CurrentPlayer => _currentPlayer;
        public GameObject PlayerPrefab => _playerPrefab;

        public async UniTask InitializeAsync()
        {
            _resourceService = ServiceLocator.Get<IResourceService>();
            await LoadPlayerPrefab();
        }

        public PlayerController CreatePlayerInstance()
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerService] PlayerPrefab이 로드되지 않았습니다.");
                return null;
            }

            DestroyPlayerInstance();

            var spawned = Instantiate(_playerPrefab);
            var controller = spawned.GetComponent<PlayerController>();
            if (controller == null)
            {
                Debug.LogError("[PlayerService] PlayerPrefab에 PlayerController 컴포넌트가 없습니다.");
                Destroy(spawned);
                return null;
            }

            _currentPlayer = controller;
            return controller;
        }

        public void DestroyPlayerInstance()
        {
            if (_currentPlayer == null)
                return;

            Destroy(_currentPlayer.gameObject);
            _currentPlayer = null;
        }

        private async UniTask LoadPlayerPrefab()
        {
            if (_resourceService == null)
            {
                Debug.LogError("[PlayerService] ResourceService가 등록되지 않았습니다.");
                return;
            }

            _playerPrefab = await _resourceService.LoadAssetAsync<GameObject>(PlayerPrefabPath);
            if (_playerPrefab == null)
            {
                Debug.LogError($"[PlayerService] PlayerPrefab 로드 실패: {PlayerPrefabPath}");
            }
        }
    }
}





