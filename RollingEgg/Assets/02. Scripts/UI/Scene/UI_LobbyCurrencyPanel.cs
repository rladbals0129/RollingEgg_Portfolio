using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RollingEgg.Core;
using UnityEngine.AddressableAssets;

namespace RollingEgg.UI
{
    /// <summary>
    /// 로비 화면에서 재화 정보를 표시하고 드롭다운으로 전용 재화를 펼쳐 보여주는 패널
    /// </summary>
    public class UI_LobbyCurrencyPanel : MonoBehaviour
    {
        [Header("공용 재화 표시")]
        [SerializeField] private Image _commonCurrencyIcon;
        [SerializeField] private TextMeshProUGUI _commonCurrencyAmountText;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private GameObject _dropdownContainer;

        [Header("전용 재화 항목")]
        [SerializeField] private SpecialCurrencyView[] _specialCurrencyViews;

        [Header("초기 설정")]
        [SerializeField] private bool _closeDropdownOnStart = true;

        [Serializable]
        private class SpecialCurrencyView
        {
            [Tooltip("CurrencyTableSO에 정의된 type (예: blue, red, white, black, yellow)")]
            public string currencyType;
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI amountText;
        }

        private ICurrencyService _currencyService;
        private bool _initialized;
        private bool _dropdownVisible;

        private const int COMMON_CURRENCY_ID_FALLBACK = 1;

        private int _commonCurrencyId = -1;
        private readonly Dictionary<string, int> _typeToCurrencyId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _idToCurrencyType = new();

        private readonly Dictionary<int, Sprite> _iconCache = new();
        private readonly Dictionary<int, AssetReferenceSprite> _iconReferences = new();
        private readonly Dictionary<int, UniTask<Sprite>> _iconLoadingTasks = new();

        /// <summary>
        /// 외부 초기화(필요 시 여러 번 호출해도 안전)
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[UI_LobbyCurrencyPanel] Initialize 재호출 - RefreshAllAsync 실행");
                RefreshAllAsync().Forget();
                return;
            }

            _currencyService = ServiceLocator.Get<ICurrencyService>();

            if (_currencyService == null)
            {
                Debug.LogError("[UI_LobbyCurrencyPanel] ICurrencyService를 찾을 수 없습니다. ServiceLocator 등록을 확인하세요.");
            }

            if (_toggleButton != null)
            {
                _toggleButton.onClick.AddListener(ToggleDropdown);
            }

            if (_dropdownContainer != null && _closeDropdownOnStart)
            {
                _dropdownContainer.SetActive(false);
                _dropdownVisible = false;
                ForceLayoutRefresh();
            }

            BuildCurrencyLookup();
            RefreshAllAsync().Forget();

            SubscribeCurrencyEvents();
            _initialized = true;
            Debug.Log("[UI_LobbyCurrencyPanel] Initialize 완료");
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                SubscribeCurrencyEvents();
                RefreshAllAsync().Forget();
            }
        }

        private void OnDisable()
        {
            UnsubscribeCurrencyEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeCurrencyEvents();

            if (_toggleButton != null)
            {
                _toggleButton.onClick.RemoveListener(ToggleDropdown);
            }

            ReleaseCurrencyIcons();
        }

        /// <summary>
        /// 드롭다운을 강제로 닫는다.
        /// </summary>
        public void CloseDropdown()
        {
            SetDropdownVisible(false);
        }

        private void BuildCurrencyLookup()
        {
            _typeToCurrencyId.Clear();
            _idToCurrencyType.Clear();

            if (_currencyService == null)
            {
                _commonCurrencyId = -1;
                return;
            }

            _commonCurrencyId = DetermineCommonCurrencyId();

            var allCurrencies = _currencyService.GetAllCurrencyAmounts();
            foreach (var kvp in allCurrencies)
            {
                var currencyInfo = _currencyService.GetCurrencyInfo(kvp.Key);
                if (currencyInfo == null)
                    continue;

                var type = currencyInfo.type;
                if (string.IsNullOrEmpty(type) ||
                    type == "0" ||
                    string.Equals(type, "common", StringComparison.OrdinalIgnoreCase))
                {
                    if (_commonCurrencyId <= 0)
                    {
                        _commonCurrencyId = kvp.Key;
                    }
                    _idToCurrencyType[kvp.Key] = "common";
                }
                else
                {
                    var trimmedType = type.Trim();
                    if (!_typeToCurrencyId.ContainsKey(trimmedType))
                    {
                        _typeToCurrencyId.Add(trimmedType, kvp.Key);
                    }

                    _idToCurrencyType[kvp.Key] = trimmedType;
                }
            }
        }

        private void SubscribeCurrencyEvents()
        {
            if (_currencyService != null)
            {
                _currencyService.OnCurrencyChanged += HandleCurrencyChanged;
            }
        }

        private void UnsubscribeCurrencyEvents()
        {
            if (_currencyService != null)
            {
                _currencyService.OnCurrencyChanged -= HandleCurrencyChanged;
            }
        }

        private void HandleCurrencyChanged(CurrencyBalanceChangedEvent evt)
        {
            if (!_initialized || _currencyService == null)
                return;

            if (evt.currencyId == _commonCurrencyId)
            {
                RefreshCommonCurrencyAsync().Forget();
                return;
            }

            if (_idToCurrencyType.TryGetValue(evt.currencyId, out var type))
            {
                RefreshSpecialCurrencyAsync(type).Forget();
            }
        }

        private void ToggleDropdown()
        {
            SetDropdownVisible(!_dropdownVisible);

            if (_dropdownVisible)
            {
                RefreshSpecialCurrenciesAsync().Forget();
            }
        }

        private void SetDropdownVisible(bool isVisible)
        {
            if (_dropdownContainer != null)
            {
                _dropdownContainer.SetActive(isVisible);
            }
            _dropdownVisible = isVisible;
            ForceLayoutRefresh();
        }

        private void ForceLayoutRefresh()
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        private async UniTask RefreshAllAsync()
        {
            if (_currencyService == null)
                return;

            BuildCurrencyLookup();

            await RefreshCommonCurrencyAsync();
            await RefreshSpecialCurrenciesAsync();
        }

        private async UniTask RefreshCommonCurrencyAsync()
        {
            if (_currencyService == null)
                return;

            int amount = _currencyService.GetCommonCurrencyAmount();
            if (_commonCurrencyAmountText != null)
            {
                _commonCurrencyAmountText.text = amount.ToString();
            }

            await SetCurrencyIconAsync(_commonCurrencyId, _commonCurrencyIcon);
        }

        private async UniTask RefreshSpecialCurrenciesAsync()
        {
            if (_specialCurrencyViews == null || _specialCurrencyViews.Length == 0)
                return;

            foreach (var view in _specialCurrencyViews)
            {
                await RefreshSpecialCurrencyAsync(view);
            }
        }

        private async UniTask RefreshSpecialCurrencyAsync(string currencyType)
        {
            if (_specialCurrencyViews == null)
                return;

            foreach (var view in _specialCurrencyViews)
            {
                if (view == null)
                    continue;

                if (string.Equals(view.currencyType, currencyType, StringComparison.OrdinalIgnoreCase))
                {
                    await RefreshSpecialCurrencyAsync(view);
                    break;
                }
            }
        }

        private async UniTask RefreshSpecialCurrencyAsync(SpecialCurrencyView view)
        {
            if (view == null)
                return;

            int currencyId = GetCurrencyIdByType(view.currencyType);
            bool hasCurrency = currencyId > 0 && _currencyService != null;

            if (view.root != null)
            {
                view.root.SetActive(hasCurrency);
            }

            if (!hasCurrency)
            {
                if (view.amountText != null)
                    view.amountText.text = "-";

                if (view.icon != null)
                {
                    view.icon.sprite = null;
                    view.icon.enabled = false;
                }

                return;
            }

            int amount = _currencyService.GetCurrencyAmount(currencyId);
            if (view.amountText != null)
            {
                view.amountText.text = amount.ToString();
            }

            await SetCurrencyIconAsync(currencyId, view.icon);
        }

        private int DetermineCommonCurrencyId()
        {
            if (_currencyService == null)
                return -1;

            var info = _currencyService.GetCurrencyInfo(COMMON_CURRENCY_ID_FALLBACK);
            if (info != null)
                return COMMON_CURRENCY_ID_FALLBACK;

            var allCurrencies = _currencyService.GetAllCurrencyAmounts();
            foreach (var kvp in allCurrencies)
            {
                var currencyInfo = _currencyService.GetCurrencyInfo(kvp.Key);
                if (currencyInfo == null)
                    continue;

                if (string.IsNullOrEmpty(currencyInfo.type) ||
                    currencyInfo.type == "0" ||
                    string.Equals(currencyInfo.type, "common", StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return -1;
        }

        private int GetCurrencyIdByType(string currencyType)
        {
            if (string.IsNullOrEmpty(currencyType))
                return -1;

            if (_typeToCurrencyId.TryGetValue(currencyType, out var id))
                return id;

            int fallback = currencyType.ToLowerInvariant() switch
            {
                "blue" => 2,
                "red" => 3,
                "white" => 4,
                "black" => 5,
                "yellow" => 6,
                _ => -1
            };

            if (fallback > 0)
            {
                var info = _currencyService?.GetCurrencyInfo(fallback);
                if (info != null)
                {
                    _typeToCurrencyId[currencyType] = fallback;
                    _idToCurrencyType[fallback] = info.type;
                    return fallback;
                }
            }

            return -1;
        }

        private async UniTask SetCurrencyIconAsync(int currencyId, Image targetImage)
        {
            if (targetImage == null)
                return;

            if (currencyId <= 0)
            {
                targetImage.sprite = null;
                targetImage.enabled = false;
                return;
            }

            var sprite = await GetCurrencyIconSpriteAsync(currencyId);
            if (targetImage == null)
                return;

            if (sprite != null)
            {
                targetImage.sprite = sprite;
                targetImage.enabled = true;
            }
            else
            {
                targetImage.sprite = null;
                targetImage.enabled = false;
            }
        }

        private async UniTask<Sprite> GetCurrencyIconSpriteAsync(int currencyId)
        {
            if (currencyId <= 0 || _currencyService == null)
                return null;

            if (_iconCache.TryGetValue(currencyId, out var cachedSprite))
                return cachedSprite;

            var currencyInfo = _currencyService.GetCurrencyInfo(currencyId);
            if (currencyInfo?.icon == null)
                return null;

            var iconReference = currencyInfo.icon;

            if (_iconReferences.TryGetValue(currencyId, out var registeredReference) &&
                registeredReference != iconReference)
            {
                ReleaseCurrencyIconReference(currencyId, registeredReference);
                _iconReferences.Remove(currencyId);
                _iconCache.Remove(currencyId);
            }

            if (_iconCache.TryGetValue(currencyId, out cachedSprite))
                return cachedSprite;

            if (iconReference.Asset != null && iconReference.Asset is Sprite preloadedSprite)
            {
                _iconCache[currencyId] = preloadedSprite;
                _iconReferences[currencyId] = iconReference;
                return preloadedSprite;
            }

            if (iconReference.OperationHandle.IsValid())
            {
                var handle = iconReference.OperationHandle;
                if (handle.IsDone && handle.Result is Sprite handleSprite)
                {
                    _iconCache[currencyId] = handleSprite;
                    _iconReferences[currencyId] = iconReference;
                    return handleSprite;
                }
            }

            if (_iconLoadingTasks.TryGetValue(currencyId, out var existingTask))
            {
                return await existingTask;
            }

            var loadTask = iconReference
                .LoadAssetAsync<Sprite>()
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            _iconLoadingTasks[currencyId] = loadTask;

            try
            {
                var loadedSprite = await loadTask;
                if (loadedSprite != null)
                {
                    _iconCache[currencyId] = loadedSprite;
                    _iconReferences[currencyId] = iconReference;
                }
                return loadedSprite;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[UI_LobbyCurrencyPanel] 아이콘 로드 취소 - CurrencyId: {currencyId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI_LobbyCurrencyPanel] 아이콘 로드 실패 - CurrencyId: {currencyId}, err: {e.Message}");
            }
            finally
            {
                _iconLoadingTasks.Remove(currencyId);
            }

            return null;
        }

        private void ReleaseCurrencyIcons()
        {
            foreach (var kvp in _iconReferences)
            {
                ReleaseCurrencyIconReference(kvp.Key, kvp.Value);
            }

            _iconReferences.Clear();
            _iconCache.Clear();
            _iconLoadingTasks.Clear();
        }

        private void ReleaseCurrencyIconReference(int currencyId, AssetReferenceSprite reference)
        {
            if (reference == null)
                return;

            try
            {
                if (reference.IsValid() &&
                    reference.OperationHandle.IsValid() &&
                    reference.OperationHandle.IsDone)
                {
                    reference.ReleaseAsset();
                    Debug.Log($"[UI_LobbyCurrencyPanel] 아이콘 해제 - CurrencyId: {currencyId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI_LobbyCurrencyPanel] 아이콘 해제 실패 - CurrencyId: {currencyId}, err: {e.Message}");
            }
        }
    }
}

