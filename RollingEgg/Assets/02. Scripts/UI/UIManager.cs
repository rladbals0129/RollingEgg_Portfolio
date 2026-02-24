using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.UI
{
    [DefaultExecutionOrder(-1000)]
    public class UIManager : MonoSingleton<UIManager>
    {
        [Header("## Registry")]
        [SerializeField] private UIRegistry _uiRegistry;

        [Header("## Scene")]
        [SerializeField] private Transform _sceneLayer;

        [Header("## Popup")]
        [SerializeField] private Transform _popupLayer;

        [Header("## Pool")]
        [SerializeField] private Transform _poolLayer;

        [Header("## Transition")]
        [SerializeField] private UIFadeController _fadeController;

        private UI_Scene _currentScene;
        private UI_Popup _currentPopup;

        private readonly Dictionary<ESceneUIType, UI_Scene> _sceneCache = new Dictionary<ESceneUIType, UI_Scene>();
        private readonly Dictionary<EPopupUIType, UI_Popup> _popupCache = new Dictionary<EPopupUIType, UI_Popup>();
        private readonly Dictionary<Type, Stack<Component>> _uiPool = new Dictionary<Type, Stack<Component>>();

        private readonly Stack<UI_Popup> _popupHistory = new Stack<UI_Popup>();

        private bool _isInitialized = false;
        private bool _isInitializing = false;
        private float _initializationProgress = 0f;
        private bool _isSceneTransitioning = false;

        private int _processedItems;
        private int _totalItems;

        public bool IsInitialized => _isInitialized;
        public float InitializationProgress => _initializationProgress;
        public int PopupHistoryCount => _popupHistory.Count;
        public bool IsSceneTransitioning => _isSceneTransitioning;

        protected override void Awake()
        {
            base.Awake();
            EnsurePoolLayer();
        }

        public async UniTask InitializeAsync(IProgress<float> progress = null)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("UIManager is already initialized.");
                progress?.Report(1f);
                return;
            }

            // 동시 호출 가드: 이미 초기화 중이면 완료까지 대기
            if (_isInitializing)
            {
                progress?.Report(_initializationProgress);
                await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => _isInitialized == true || _isInitializing == false);
                return;
            }

            // 필수 참조 가드: 씬에 배치된 UIManager에 레퍼런스를 설정하지 않으면 초기화 불가
            if (_uiRegistry == null)
            {
                Debug.LogError("[UIManager] UIRegistry가 할당되지 않았습니다. 씬에 배치된 UIManager에 레퍼런스를 설정하세요.");
                return;
            }

            Debug.Log("Starting UI initialization with UniTask...");
            _initializationProgress = 0f;
            _isInitializing = true;

            try
            {
                _totalItems = (_uiRegistry.Scenes?.Count ?? 0) + (_uiRegistry.Popups?.Count ?? 0);
                _processedItems = 0;

                if (_uiRegistry.Scenes != null)
                    await InitializeUIElements(_uiRegistry.Scenes, _sceneLayer, _sceneCache, progress);

                if (_uiRegistry.Popups != null)
                    await InitializeUIElements(_uiRegistry.Popups, _popupLayer, _popupCache, progress);

                _isInitialized = true;
                _initializationProgress = 1f;
                progress?.Report(1f);

                Debug.Log("UI initialization completed successfully.");
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                Debug.LogError($"[UIManager] UI 초기화 중 오류: {ex.Message}");
            }
            finally
            {
                _isInitializing = false;
            }
        }


        public UI_Scene ShowScene(ESceneUIType sceneType) => ShowCommon(_sceneCache, sceneType, ref _currentScene);
        public T ShowScene<T>(ESceneUIType sceneType) where T : UI_Scene
        {
            UI_Scene scene = ShowScene(sceneType);

            if (scene == null)
                return null;

            T typedScene = scene as T;
            if (typedScene == null)
            {
                Debug.LogError($"[UIManager] Scene of type {sceneType} is not of type {typeof(T).Name}");
                return null;
            }

            return typedScene;
        }

        public async UniTask<UI_Scene> ShowSceneWithFadeAsync(ESceneUIType sceneType, float? duration = null, Color? overrideColor = null, CancellationToken token = default)
        {
            if (_fadeController == null)
            {
                Debug.LogWarning("[UIManager] UIFadeController가 설정되지 않아 즉시 전환합니다.");
                return ShowScene(sceneType);
            }

            if (_isSceneTransitioning)
            {
                Debug.LogWarning("[UIManager] 다른 씬 전환이 진행 중입니다.");
                return _currentScene;
            }

            _isSceneTransitioning = true;

            try
            {
                await _fadeController.FadeOutAsync(duration, overrideColor, token);
                var scene = ShowScene(sceneType);
                await _fadeController.FadeInAsync(duration, token);
                return scene;
            }
            finally
            {
                _isSceneTransitioning = false;
            }
        }

        public async UniTask<T> ShowSceneWithFadeAsync<T>(ESceneUIType sceneType, float? duration = null, Color? overrideColor = null, CancellationToken token = default) where T : UI_Scene
        {
            var scene = await ShowSceneWithFadeAsync(sceneType, duration, overrideColor, token);

            if (scene == null)
                return null;

            T typedScene = scene as T;
            if (typedScene == null)
            {
                Debug.LogError($"[UIManager] Scene of type {sceneType} is not of type {typeof(T).Name}");
                return null;
            }

            return typedScene;
        }

        public async UniTask<UI_Popup> ShowPopupWithFadeAsync(EPopupUIType popupType, bool remember = true, float? duration = null, Color? overrideColor = null, CancellationToken token = default, Func<UI_Popup, UniTask> setupBeforeFadeIn = null)
        {
            if (_fadeController == null)
            {
                Debug.LogWarning("[UIManager] UIFadeController가 설정되지 않아 팝업을 즉시 전환합니다.");
                return ShowPopup(popupType, remember);
            }

            await _fadeController.FadeOutAsync(duration, overrideColor, token);
            var popup = ShowPopup(popupType, remember);
            if (popup != null && setupBeforeFadeIn != null)
                await setupBeforeFadeIn(popup);
            await _fadeController.FadeInAsync(duration, token);
            return popup;
        }

        public async UniTask<T> ShowPopupWithFadeAsync<T>(EPopupUIType popupType, bool remember = true, float? duration = null, Color? overrideColor = null, CancellationToken token = default, Func<T, UniTask> setupBeforeFadeIn = null) where T : UI_Popup
        {
            var popup = await ShowPopupWithFadeAsync(popupType, remember, duration, overrideColor, token, async p =>
            {
                if (setupBeforeFadeIn != null && p is T typed)
                    await setupBeforeFadeIn(typed);
            });

            if (popup == null)
                return null;

            T typedPopup = popup as T;
            if (typedPopup == null)
            {
                Debug.LogError($"[UIManager] Popup of type {popupType} is not of type {typeof(T).Name}");
                return null;
            }

            return typedPopup;
        }

        public UniTask RunWithFadeAsync(Action action, float? duration = null, Color? overrideColor = null, CancellationToken token = default)
        {
            return RunWithFadeAsync(() =>
            {
                action?.Invoke();
                return UniTask.CompletedTask;
            }, duration, overrideColor, token);
        }

        public async UniTask RunWithFadeAsync(Func<UniTask> action, float? duration = null, Color? overrideColor = null, CancellationToken token = default)
        {
            if (action == null)
                return;

            if (_fadeController == null)
            {
                await action();
                return;
            }

            await _fadeController.FadeOutAsync(duration, overrideColor, token);
            try
            {
                await action();
            }
            finally
            {
                await _fadeController.FadeInAsync(duration, token);
            }
        }
        public void CloseCurrentScene() => CloseCommon(ref _currentScene);

        #region Popup
        public UI_Popup ShowPopup(EPopupUIType popupType, bool remember = true)
        {
            if (!_popupCache.TryGetValue(popupType, out UI_Popup popup))
            {
                Debug.LogError($"Popup of type {popupType} not found!");
                return null;
            }

            if (_currentPopup != null && ReferenceEquals(_currentPopup, popup))
            {
                if (!_currentPopup.gameObject.activeSelf)
                {
                    _currentPopup.Show();
                    _currentPopup.transform.SetAsLastSibling();
                }
                return popup;
            }

            if (remember && popup.RememberInHistory)
                _popupHistory.Push(popup);

            _currentPopup = popup;
            _currentPopup.Show();
            _currentPopup.transform.SetAsLastSibling();
            return popup;
        }

        /// <summary>
        /// 제네릭을 통해 원하는 타입의 팝업을 반환
        /// </summary>
        public T ShowPopup<T>(EPopupUIType popupType, bool remember = true) where T : UI_Popup
        {
            UI_Popup popup = ShowPopup(popupType, remember);

            if (popup == null)
                return null;

            T typedPopup = popup as T;
            if (typedPopup == null)
            {
                Debug.LogError($"[UIManager] Popup of type {popupType} is not of type {typeof(T).Name}");
                return null;
            }

            return typedPopup;
        }

        public void CloseCurrentPopup()
        {
            ClosePopup(_currentPopup);
        }

        public void ClosePopup(UI_Popup popup)
        {
            if (popup == null)
                return;

            bool wasCurrent = ReferenceEquals(_currentPopup, popup);

            popup.Hide();

            if (wasCurrent)
            {
                if (_popupHistory.Count > 0 && ReferenceEquals(_popupHistory.Peek(), popup))
                    _popupHistory.Pop();

                _currentPopup = _popupHistory.Count > 0 ? _popupHistory.Peek() : null;
                return;
            }

            RemovePopupFromHistory(popup);
        }

        private void RemovePopupFromHistory(UI_Popup popup)
        {
            if (popup == null || _popupHistory.Count == 0)
                return;

            var buffer = new Stack<UI_Popup>(_popupHistory.Count);
            bool removed = false;

            while (_popupHistory.Count > 0)
            {
                var top = _popupHistory.Pop();
                if (!removed && ReferenceEquals(top, popup))
                {
                    removed = true;
                    continue;
                }

                buffer.Push(top);
            }

            while (buffer.Count > 0)
            {
                _popupHistory.Push(buffer.Pop());
            }
        }

        public void ClosePopups(int remain)
        {
            while (_popupHistory.Count > remain)
            {
                CloseCurrentPopup();
            }
        }

        public void CloseAllPopups() => ClosePopups(remain: 0);
        #endregion

        #region UI Pool
        public T RentUI<T>(T prefab, Transform parent) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError("[UIManager] RentUI 호출 시 prefab이 null 입니다.");
                return null;
            }

            EnsurePoolLayer();
            var type = typeof(T);

            if (_uiPool.TryGetValue(type, out var stack) && stack.Count > 0)
            {
                var instance = (T)stack.Pop();
                AttachToParent(instance.transform, parent);
                if (instance is UI_Base reusableFromPool)
                    reusableFromPool.OnRentFromPool();
                instance.gameObject.SetActive(true);
                return instance;
            }

            var created = Instantiate(prefab, parent);
            if (created is UI_Base createdBase)
                createdBase.OnRentFromPool();
            return created;
        }

        public void ReturnUI<T>(T instance) where T : Component
        {
            if (instance == null)
                return;

            EnsurePoolLayer();

            if (instance is UI_Base reusable)
                reusable.OnReturnToPool();

            instance.gameObject.SetActive(false);
            AttachToParent(instance.transform, _poolLayer);

            var type = typeof(T);
            if (!_uiPool.TryGetValue(type, out var stack))
            {
                stack = new Stack<Component>();
                _uiPool[type] = stack;
            }
            stack.Push(instance);
        }

        private void EnsurePoolLayer()
        {
            if (_poolLayer != null)
                return;

            var poolObject = new GameObject("[UI Pool]");
            _poolLayer = poolObject.transform;
            _poolLayer.SetParent(transform, false);
            _poolLayer.gameObject.SetActive(false);
        }

        private static void AttachToParent(Transform target, Transform parent)
        {
            if (target == null)
                return;

            if (parent == null)
                target.SetParent(null);
            else
                target.SetParent(parent, false);
        }
        #endregion

        public T GetCurrentScene<T>() where T : UI_Scene => _currentScene as T;
        public T GetCurrentPopup<T>() where T : UI_Popup => _currentPopup as T;

        public bool IsPopupActive(EPopupUIType popupType)
        {
            if (_popupCache.TryGetValue(popupType, out var popup))
            {
                return popup != null && popup.gameObject.activeInHierarchy;
            }
            return false;
        }

        /// <summary>
        /// 현재 활성화된 팝업이 있는지 확인합니다.
        /// </summary>
        public bool HasAnyPopupActive()
        {
            return _currentPopup != null && _currentPopup.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 캐시된 씬 UI를 가져옵니다 (Show 전에 데이터 설정 시 사용)
        /// </summary>
        public T GetCachedScene<T>(ESceneUIType sceneType) where T : UI_Scene
        {
            if (_sceneCache.TryGetValue(sceneType, out var scene))
            {
                return scene as T;
            }
            return null;
        }

        private TBase ShowCommon<TBase, TEnum>(Dictionary<TEnum, TBase> cache, TEnum type, ref TBase current)
         where TBase : UI_Base
         where TEnum : Enum
        {
            if (!cache.TryGetValue(type, out var target))
            {
                Debug.LogError($"UI of type {type} not found! Available types: {string.Join(", ", cache.Keys)}");
                return null;
            }

            if (current != null)
            {
                if (ReferenceEquals(current, target))
                    return target;
                current.Hide();
            }

            current = target;
            current.Show();
            return target;
        }

        private void CloseCommon<TBase>(ref TBase current) where TBase : UI_Base
        {
            if (current == null)
                return;

            current.Hide();
            current = null;
        }

        private async UniTask InitializeUIElements<TBase, TEnum, TEntry>(List<TEntry> entries, Transform layer, Dictionary<TEnum, TBase> cache, IProgress<float> progress = null)
            where TBase : UI_Base
            where TEnum : Enum
            where TEntry : IUIEntry<TEnum>
        {
            foreach (var entry in entries)
            {
                Debug.Log($"[UIManager] Initializing UI: Type={entry.Type}, GUID={entry.PrefabReference.AssetGUID}");
                var instance = await LoadAndInstantiateAsync<TBase>(entry.PrefabReference, layer);
                if (instance != null)
                {
                    // 초기화 대기 중 1프레임 노출 방지: 먼저 비활성화 후 초기화
                    instance.gameObject.SetActive(false);
                    await instance.InitializeAsync();
                    instance.Hide();
                    cache.Add(entry.Type, instance);
                    Debug.Log($"[UIManager] Successfully loaded and cached: {entry.Type}");
                }
                else
                {
                    Debug.LogError($"[UIManager] Failed to load UI: Type={entry.Type}, GUID={entry.PrefabReference.AssetGUID}");
                }

                _processedItems++;
                _initializationProgress = _totalItems > 0 ? (float)_processedItems / _totalItems : 1f;
                progress?.Report(_initializationProgress);
            }
        }

        private async UniTask<T> LoadAndInstantiateAsync<T>(AssetReferenceGameObject assetRef, Transform layer) where T : UI_Base
        {
			try
			{
				Debug.Log($"[UIManager] Loading asset from Addressables: GUID={assetRef.AssetGUID}");
				// 이미 로드된 경우 OperationHandle 사용
				GameObject asset;
				if (assetRef.OperationHandle.IsValid())
				{
					asset = assetRef.OperationHandle.Result as GameObject;
					if (asset == null)
					{
						// 핸들이 유효하지만 결과가 null인 경우 재요청
						asset = await assetRef.LoadAssetAsync<GameObject>().ToUniTask();
					}
				}
				else
				{
					asset = await assetRef.LoadAssetAsync<GameObject>().ToUniTask();
				}
				T prefab = asset.GetComponent<T>();
				if (prefab != null)
				{
					T instance = Instantiate(prefab, layer);
					// 런타임 표준화: 부모 Canvas 기준으로 RectTransform/스케일/중첩 Canvas 정리
					//NormalizeUIHierarchy(instance.transform as RectTransform);
					Debug.Log($"[UIManager] Successfully instantiated from Addressables: {typeof(T).Name}");
					return instance;
				}
				else
				{
					Debug.LogError($"[UIManager] Asset loaded but component {typeof(T).Name} not found on GameObject");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[UIManager] Error loading UI from Addressables {assetRef.AssetGUID}: {ex.Message}");
#if UNITY_EDITOR
				// Addressables 카탈로그 미빌드/키 미등록 등으로 실패 시 에디터 에셋으로 폴백
				Debug.LogWarning($"[UIManager] Attempting editor fallback for GUID={assetRef.AssetGUID}");
				try
				{
					var editorAsset = assetRef.editorAsset as GameObject;
					if (editorAsset != null)
					{
						Debug.Log($"[UIManager] Editor asset found: {editorAsset.name}");
						T prefab = editorAsset.GetComponent<T>();
						if (prefab != null)
						{
							T instance = Instantiate(prefab, layer);
							// 에디터 폴백 시에도 동일하게 표준화 처리
						//	NormalizeUIHierarchy(instance.transform as RectTransform);
							Debug.Log($"[UIManager] Successfully instantiated from editor asset: {typeof(T).Name}");
							return instance;
						}
						else
						{
							Debug.LogError($"[UIManager] Editor asset has no component {typeof(T).Name}");
						}
					}
					else
					{
						Debug.LogError($"[UIManager] Editor asset is null for GUID={assetRef.AssetGUID}");
					}
				}
				catch (Exception editorEx)
				{
					Debug.LogError($"[UIManager] Editor fallback failed: {editorEx.Message}");
				}
#endif
			}
			return null;
        }
    }
}
