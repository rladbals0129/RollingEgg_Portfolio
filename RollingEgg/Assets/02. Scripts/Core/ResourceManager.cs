using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RollingEgg.Core
{
    public interface IResourceService
    {
        void Initialize();
        UniTask<T> LoadAssetAsync<T>(string key = default) where T : Object;
        UniTask<List<T>> LoadAssetsByLabelAsync<T>(string label) where T : Object;
        UniTask<GameObject> InstantiateAsync(string key);
        void DestroyInstance(GameObject instance);
        void UnloadAsset(string key);
    }

    public class ResourceManager : IResourceService
    {
        private Dictionary<string, Object> _loadedAssets = new Dictionary<string, Object>();

        public void Initialize()
        {
            _loadedAssets.Clear();
            Debug.Log("ResourceManager Initialized.");
        }

        /// <summary>
        /// 특정 키(주소)를 사용하여 Addressable 에셋 하나를 비동기적으로 로드
        /// </summary>
        public async UniTask<T> LoadAssetAsync<T>(string key = default) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                key = typeof(T).Name;
            }

            if (_loadedAssets.TryGetValue(key, out var chaced) && chaced is T typed)
            {
                Debug.Log($"이미 로드된 리소스: {key}");
                return typed;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _loadedAssets[key] = handle.Result;
                Debug.Log($"로드 성공: {key}");
                return handle.Result;
            }
            else
            {
                Debug.LogError($"로드 실패: {key}");
            }

            return null;
        }

        /// <summary>
        /// 특정 라벨을 가진 모든 Addressable 리소스를 로드
        /// </summary>
        public async UniTask<List<T>> LoadAssetsByLabelAsync<T>(string label) where T : Object
        {
            List<T> loadedList = new List<T>();

            var handle = Addressables.LoadAssetsAsync<T>(label, null);  // 콜백 없이 로드
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var asset in handle.Result)
                {
                    string key = asset.name;
                    if (!_loadedAssets.ContainsKey(key))
                    {
                        _loadedAssets[key] = asset;
                    }

                    loadedList.Add(asset);
                }

                Debug.Log($"label : {label} 리소스 {loadedList.Count}개 로드 성공");
            }
            else
            {
                Debug.LogError($"{label} 라벨 리소스 로드 실패");
            }

            return loadedList;
        }

        /// <summary>
        /// 캐싱된 특정 에셋의 참조를 해제하고 캐시에서 제거
        /// </summary>
        public void UnloadAsset(string key)
        {
            if (_loadedAssets.ContainsKey(key))
            {
                Addressables.Release(_loadedAssets[key]);
                _loadedAssets.Remove(key);
                Debug.Log($"언로드 완료: {key}");
            }
        }

        /// <summary>
        /// Addressable 프리팹을 인스턴스화(Instantiate)
        /// </summary>
        public async UniTask<GameObject> InstantiateAsync(string key)
        {
            var handle = Addressables.InstantiateAsync(key);
            return await handle.ToUniTask();
        }

        /// <summary>
        /// InstantiateAsync로 생성된 게임 오브젝트를 안전하게 파괴하고 리소스를 해제
        /// </summary>
        /// <param name="instance">파괴할 게임 오브젝트</param>
        public void DestroyInstance(GameObject instance)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
                Debug.Log($"{instance.name} 인스턴스 해제 완료");
            }
        }
    }
}
