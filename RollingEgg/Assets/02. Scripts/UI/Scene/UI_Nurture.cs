using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using RollingEgg.UI;
using RollingEgg.Core;
using RollingEgg.Data;

namespace RollingEgg
{
    /// <summary>
    /// 육성 화면 UI
    /// - 재화/경험치/스탯 표시 및 5개 행동 슬롯 제공
    /// - EventBus 구독으로 실시간 갱신 (폴링/Update() 최소화)
    /// </summary>
    public class UI_Nurture : UI_Scene
    {
        [Header("알 선택")]
        [SerializeField] private int _eggId = 1; // 기본 알 ID
        [SerializeField] private string _defaultEggType = "blue"; // 기본 알 타입
        [SerializeField] private Button _eggButton; // 알 버튼

        [Header("알 애니메이션")]
        [SerializeField] private Animator _eggAnimator; // 알 애니메이션 컨트롤러

        [Header("StatInfo 표시")]
        [SerializeField] private Image _statInfoImage; // StatInfo 이미지
        [SerializeField] private Image[] _statColorImages; // 스탯별 색상 이미지 (용기, 지성, 순수, 사랑, 혼돈 순서)

        [Header("레벨 표시")]
        [SerializeField] private TextMeshProUGUI _levelText; // 레벨 표시 텍스트

        [Header("재화 표시")]
        [SerializeField] private TextMeshProUGUI _commonCurrencyText;
        [SerializeField] private TextMeshProUGUI _specialCurrencyText;
        [SerializeField] private Image _commonCurrencyIcon;
        [SerializeField] private Image _specialCurrencyIcon;

       // [Header("경험치 표시")]
      //  [SerializeField] private TextMeshProUGUI _expText;

        [Header("스탯 표시")]
        [SerializeField] private TextMeshProUGUI _statCourageText;
        [SerializeField] private TextMeshProUGUI _statWisdomText;
        [SerializeField] private TextMeshProUGUI _statPurityText;
        [SerializeField] private TextMeshProUGUI _statLoveText;
        [SerializeField] private TextMeshProUGUI _statChaosText;

        [Header("행동 대사")]
        [SerializeField] private GameObject _actionDialogueRoot;
        [SerializeField] private TextMeshProUGUI _actionDialogueText;
        [SerializeField] private float _actionDialogueDisplayDuration = 2.5f;

        [System.Serializable]
        private class ActionSlot
        {
            public Button button;
            public TextMeshProUGUI nameText;
         
            public TextMeshProUGUI costCommonText; // 공용 재화 전용 표기 텍스트(아이콘 옆 숫자)
            public TextMeshProUGUI costSpecialText; // 전용 재화 전용 표기 텍스트(아이콘 옆 숫자)
            [HideInInspector] public int actionId;
            [HideInInspector] public string nameLocKey;
        }

        [Header("행동 슬롯(5개)")]
        [SerializeField] private ActionSlot[] _actionSlots = new ActionSlot[5];

        [Header("진화하기 버튼")]
        [SerializeField] private Button _evolutionButton; // 진화하기 버튼

        [Header("도감 버튼")]
        [SerializeField] private Button _collectionButton; // 도감 버튼

        [Header("Placeholder UI")]
        [SerializeField] private CanvasGroup _contentCanvasGroup;
        [SerializeField] private Image _eggPlaceholderImage;
        [SerializeField] private Sprite _currencyPlaceholderIcon;
        [SerializeField] private Color _statPlaceholderColor = new Color(1f, 1f, 1f, 0.25f);

        private IEventBus _eventBus;
        private IAudioService _audioService;
        private ICurrencyService _currencyService;
    
        private IGrowthActionService _growthActionService;
		private ILocalizationService _localizationService;

        private string _eggType = string.Empty;

		private const string TABLE_GROWTH_ACTIONS = "GrowthActions"; // String Table 이름
        private const string TABLE_STAT = "Stat";

        private IResourceService _resourceService;
        private const int COMMON_CURRENCY_ID_DEFAULT = 1;
        private int _commonCurrencyId = -1;
        private int _currentSpecialCurrencyId = -1;
        private string _currentSpecialCurrencyType = string.Empty;

        private readonly Dictionary<int, Sprite> _currencyIconCache = new();
        private readonly Dictionary<int, AssetReferenceSprite> _currencyIconReferences = new();
        private readonly Dictionary<int, UniTask<Sprite>> _currencyIconLoadingTasks = new();
        private readonly List<string> _dialogueKeyBuffer = new();
        private readonly Dictionary<StatType, string> _statNameCache = new();
        private RuntimeAnimatorController _currentAnimatorController;
        private System.Threading.CancellationTokenSource _loadCancellationToken;
        private CancellationTokenSource _dialogueCancellationToken;
        private int _currentLoadedEggId = -1;
        private bool _isSceneChanging;

        private const int EVOLUTION_REQUIRED_LEVEL = 5; // 진화 가능 최소 레벨
        public override async UniTask InitializeAsync()
        {
            _eventBus = ServiceLocator.Get<IEventBus>();
            _audioService = ServiceLocator.Get<IAudioService>();
            _currencyService = ServiceLocator.Get<ICurrencyService>();
            _growthActionService = ServiceLocator.Get<IGrowthActionService>();
			_localizationService = ServiceLocator.Get<ILocalizationService>();
            _resourceService = ServiceLocator.Get<IResourceService>();

            if (_eggId <= 0 || _eggId > 5)
            {
                 _eggId = 1; // 기본값
                 _defaultEggType = "blue"; // 기본값
            }
    
            // eggType이 비어있으면 서비스에서 가져오기
            if (string.IsNullOrEmpty(_defaultEggType))
            {
                _defaultEggType = _growthActionService.GetEggType(_eggId);
                if (string.IsNullOrEmpty(_defaultEggType))
                {
                    _defaultEggType = "blue"; // 기본값
                }
            }

            // 알이 없으면 기본 알 생성 (유효한 eggId일 때만)
            if (_eggId > 0 && !_growthActionService.HasEgg(_eggId))
            {
                _growthActionService.CreateEgg(_eggId);
            }

            _eggType = _growthActionService.GetEggType(_eggId);
            UpdateCurrencyIds();
            await UniTask.Yield();
        }

        public override void OnShow()
        {
            if (_audioService != null)
            {
                _audioService.PlayBGM(EBGMKey.BGM_Nurture);
            }

            SetContentVisible(false);
            if (_eventBus != null)
            {
                _eventBus.Subscribe<CurrencyBalanceChangedEvent>(OnCurrencyChanged);
                _eventBus.Subscribe<StatChangedEvent>(OnStatChanged);
                _eventBus.Subscribe<GrowthActionPerformedEvent>(OnActionPerformed);
                _eventBus.Subscribe<EvolutionAttemptEvent>(OnEvolutionAttempt);
                _eventBus.Subscribe<EvolutionCompletedEvent>(OnEvolutionCompleted);
                _eventBus.Subscribe<EvolutionFailedEvent>(OnEvolutionFailed);
                _eventBus.Subscribe<LevelChangedEvent>(OnLevelChanged);
            }

			// 로캘 변경 시 UI 갱신
			if (_localizationService != null)
			{
				_localizationService.OnLocaleChanged += OnLocaleChanged;
			}

            // 버튼 핸들러 등록
            for (int i = 0; i < _actionSlots.Length; i++)
            {
                int index = i;
                if (_actionSlots[i]?.button != null)
                {
                    _actionSlots[i].button.onClick.AddListener(() => OnClickAction(index));
                }
            }

            // 알 버튼 핸들러 등록
            if (_eggButton != null)
            {
                _eggButton.onClick.AddListener(OnClickEgg);
            }

            // 진화하기 버튼 핸들러 등록
            if (_evolutionButton != null)
            {
                _evolutionButton.onClick.AddListener(OnClickEvolution);
            }

            // 도감 버튼 핸들러 등록
            if (_collectionButton != null)
            {
                _collectionButton.onClick.AddListener(OnClickCollection);
            }

            // 비동기 데이터 로딩 후 UI 표시
            LoadAndShowDataAsync().Forget();
        }

        /// <summary>
        /// 데이터 로딩 후 UI 표시 (잔상 방지)
        /// </summary>
        private async UniTaskVoid LoadAndShowDataAsync()
        {
            // UI를 먼저 클리어 (잔상 방지)
            ClearUI();

            try
            {
                // 애니메이션 로드 완료까지 대기
                await LoadEggAnimationAsync(_eggId);
                
                // UI 갱신
                RefreshAll();
                SetContentVisible(true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UI_Nurture] 데이터 로딩 실패: {e.Message}");
            }
        }

        /// <summary>
        /// UI를 초기 상태로 클리어 (잔상 방지)
        /// </summary>
        private void ClearUI()
        {
            SetEggVisualState(showPlaceholder: true);

            // 애니메이터 초기화
            if (_eggAnimator != null)
            {
                _eggAnimator.runtimeAnimatorController = null;
            }

            // StatInfo 이미지 비활성화
            if (_statInfoImage != null)
            {
                _statInfoImage.gameObject.SetActive(false);
            }

            // 레벨 클리어
            if (_levelText != null)
            {
                _levelText.text = "Lv: -";
            }

            // 재화 클리어
            if (_commonCurrencyText != null)
                _commonCurrencyText.text = "공용 재화: -";
            if (_specialCurrencyText != null)
                _specialCurrencyText.text = "전용 재화: -";
            if (_commonCurrencyIcon != null)
            {
                ApplyCurrencyPlaceholder(_commonCurrencyIcon);
            }
            if (_specialCurrencyIcon != null)
            {
                ApplyCurrencyPlaceholder(_specialCurrencyIcon);
            }

            // 스탯 클리어
            if (_statCourageText != null) _statCourageText.text = "용기: -";
            if (_statWisdomText != null) _statWisdomText.text = "지성: -";
            if (_statPurityText != null) _statPurityText.text = "순수: -";
            if (_statLoveText != null) _statLoveText.text = "사랑: -";
            if (_statChaosText != null) _statChaosText.text = "혼돈: -";

            // 행동 슬롯 클리어
            foreach (var slot in _actionSlots)
            {
                if (slot == null) continue;
                if (slot.nameText != null) slot.nameText.text = "";
                if (slot.costCommonText != null) slot.costCommonText.text = "";
                if (slot.costSpecialText != null) slot.costSpecialText.text = "";
                if (slot.button != null) slot.button.interactable = false;
            }

            HideActionDialogueImmediate();
        }

        public override void OnHide()
        {
            SetContentVisible(false);
                // 진행 중인 로드 작업 취소
            _loadCancellationToken?.Cancel();
            _loadCancellationToken?.Dispose();
            _loadCancellationToken = null;
            CancelDialogueDisplay();
            HideActionDialogueImmediate();
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<CurrencyBalanceChangedEvent>(OnCurrencyChanged);
                _eventBus.Unsubscribe<StatChangedEvent>(OnStatChanged);
                _eventBus.Unsubscribe<GrowthActionPerformedEvent>(OnActionPerformed);
                _eventBus.Unsubscribe<EvolutionAttemptEvent>(OnEvolutionAttempt);
                _eventBus.Unsubscribe<EvolutionCompletedEvent>(OnEvolutionCompleted);
                _eventBus.Unsubscribe<EvolutionFailedEvent>(OnEvolutionFailed);
                _eventBus.Unsubscribe<LevelChangedEvent>(OnLevelChanged);

            }

			if (_localizationService != null)
			{
				_localizationService.OnLocaleChanged -= OnLocaleChanged;
			}

            // 버튼 핸들러 해제
            for (int i = 0; i < _actionSlots.Length; i++)
            {
                if (_actionSlots[i]?.button != null)
                {
                    _actionSlots[i].button.onClick.RemoveAllListeners();
                }
            }

            // 알 버튼 핸들러 해제
            if (_eggButton != null)
            {
                _eggButton.onClick.RemoveAllListeners();
            }

            // 진화하기 버튼 핸들러 해제
            if (_evolutionButton != null)
            {
                _evolutionButton.onClick.RemoveAllListeners();
            }

            // 도감 버튼 핸들러 해제
            if (_collectionButton != null)
            {
                _collectionButton.onClick.RemoveAllListeners();
            }

    
        }
        private void OnDestroy()
        {
            // 오직 UI가 완전히 파괴될 때만 리소스 해제
            if (_currentAnimatorController != null && _currentLoadedEggId > 0)
            {
                var eggInfo = _growthActionService?.GetEggInfo(_currentLoadedEggId);
                if (eggInfo != null && eggInfo.animatorController != null)
                {
                    if (eggInfo.animatorController.IsValid() &&
                        eggInfo.animatorController.OperationHandle.IsValid() &&
                        eggInfo.animatorController.OperationHandle.IsDone)
                    {
                        eggInfo.animatorController.ReleaseAsset();
                        Debug.Log($"[UI_Nurture] OnDestroy - 애니메이션 해제 완료 - ID: {_currentLoadedEggId}");
                    }
                }
                _currentAnimatorController = null;
                _currentLoadedEggId = -1;
            }

            ReleaseCurrencyIcons();
            CancelDialogueDisplay();
        }


        private void RefreshAll()
        {
            _eggType = _growthActionService.GetEggType(_eggId);
            UpdateCurrencyIds();
            RefreshCurrencies();
            RefreshExperience();
            RefreshStats();
            RefreshActions();
            RefreshLevel();
            RefreshEvolutionButton();
        }

        /// <summary>
        /// 레벨 표시 갱신
        /// </summary>
        private void RefreshLevel()
        {
            if (_levelText != null)
            {
                int level = _growthActionService.GetEggLevel(_eggId);
                _levelText.text = $"LV: {level}";
            }
        }

        /// <summary>
        /// 진화하기 버튼 활성화 조건 갱신
        /// </summary>
        private void RefreshEvolutionButton()
        {
            if (_evolutionButton == null)
                return;

            int level = _growthActionService.GetEggLevel(_eggId);
          
            bool isBlueEgg = _eggType == "blue";
            bool canEvolve = level >= EVOLUTION_REQUIRED_LEVEL && isBlueEgg;

            _evolutionButton.interactable = canEvolve && isBlueEgg;
        }

        private void RefreshCurrencies()
        {
            if (_currencyService == null)
                return;

            EnsureCurrencyIds();

            if (_commonCurrencyText != null)
            {
                int common = _currencyService.GetCommonCurrencyAmount();
                _commonCurrencyText.text = $"{common}";
            }

            string type = string.IsNullOrEmpty(_eggType) ? _defaultEggType : _eggType;

            if (_specialCurrencyText != null)
            {
                int special = _currencyService.GetSpecialCurrencyAmount(_eggId, type);
                _specialCurrencyText.text = $"{special}";
            }

            RefreshCurrencyIconsAsync().Forget();
        }

        private void EnsureCurrencyIds()
        {
            if (_currencyService == null)
                return;

            if (_commonCurrencyId <= 0)
            {
                _commonCurrencyId = DetermineCommonCurrencyId();
            }

            var targetType = string.IsNullOrEmpty(_eggType) ? _defaultEggType : _eggType;
            if (!string.Equals(_currentSpecialCurrencyType, targetType, StringComparison.OrdinalIgnoreCase) ||
                _currentSpecialCurrencyId <= 0)
            {
                _currentSpecialCurrencyType = targetType;
                _currentSpecialCurrencyId = DetermineSpecialCurrencyId(targetType);
            }
        }

        private async UniTaskVoid RefreshCurrencyIconsAsync()
        {
            if (_currencyService == null)
                return;

            EnsureCurrencyIds();

            await UniTask.WhenAll(
                SetCurrencyIconAsync(_commonCurrencyId, _commonCurrencyIcon),
                SetCurrencyIconAsync(_currentSpecialCurrencyId, _specialCurrencyIcon));
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

            if (_currencyIconCache.TryGetValue(currencyId, out var cachedSprite))
                return cachedSprite;

            var currencyInfo = _currencyService.GetCurrencyInfo(currencyId);
            if (currencyInfo?.icon == null)
                return null;

            var iconReference = currencyInfo.icon;

            if (_currencyIconReferences.TryGetValue(currencyId, out var registeredReference) &&
                registeredReference != iconReference)
            {
                ReleaseCurrencyIconReference(currencyId, registeredReference);
                _currencyIconReferences.Remove(currencyId);
                _currencyIconCache.Remove(currencyId);
            }

            if (_currencyIconCache.TryGetValue(currencyId, out cachedSprite))
                return cachedSprite;

            // 이미 다른 곳에서 로드된 경우 재사용
            if (iconReference.Asset != null && iconReference.Asset is Sprite preloadedSprite)
            {
                _currencyIconCache[currencyId] = preloadedSprite;
                _currencyIconReferences[currencyId] = iconReference;
                return preloadedSprite;
            }

            if (iconReference.OperationHandle.IsValid())
            {
                var handle = iconReference.OperationHandle;
                if (handle.IsDone && handle.Result is Sprite handleSprite)
                {
                    _currencyIconCache[currencyId] = handleSprite;
                    _currencyIconReferences[currencyId] = iconReference;
                    return handleSprite;
                }
            }

            if (_currencyIconLoadingTasks.TryGetValue(currencyId, out var existingTask))
            {
                return await existingTask;
            }

            var loadTask = iconReference
                .LoadAssetAsync<Sprite>()
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            _currencyIconLoadingTasks[currencyId] = loadTask;

            try
            {
                var loadedSprite = await loadTask;
                if (loadedSprite != null)
                {
                    _currencyIconCache[currencyId] = loadedSprite;
                    _currencyIconReferences[currencyId] = iconReference;
                }
                return loadedSprite;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[UI_Nurture] 재화 아이콘 로드 취소 - CurrencyId: {currencyId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI_Nurture] 재화 아이콘 로드 실패 - CurrencyId: {currencyId}, err: {e.Message}");
            }
            finally
            {
                _currencyIconLoadingTasks.Remove(currencyId);
            }

            return null;
        }

        private void UpdateCurrencyIds()
        {
            if (_currencyService == null)
                return;

            _commonCurrencyId = DetermineCommonCurrencyId();

            var targetType = string.IsNullOrEmpty(_eggType) ? _defaultEggType : _eggType;
            _currentSpecialCurrencyType = targetType;
            _currentSpecialCurrencyId = DetermineSpecialCurrencyId(targetType);
        }

        private int DetermineCommonCurrencyId()
        {
            if (_currencyService == null)
                return -1;

            var info = _currencyService.GetCurrencyInfo(COMMON_CURRENCY_ID_DEFAULT);
            if (info != null)
                return COMMON_CURRENCY_ID_DEFAULT;

            var allCurrencies = _currencyService.GetAllCurrencyAmounts();
            foreach (var kvp in allCurrencies)
            {
                var row = _currencyService.GetCurrencyInfo(kvp.Key);
                if (row == null)
                    continue;

                if (string.IsNullOrEmpty(row.type) ||
                    row.type == "0" ||
                    string.Equals(row.type, "common", StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return -1;
        }

        private int DetermineSpecialCurrencyId(string eggType)
        {
            if (_currencyService == null || string.IsNullOrEmpty(eggType))
                return -1;

            var allCurrencies = _currencyService.GetAllCurrencyAmounts();
            foreach (var kvp in allCurrencies)
            {
                var row = _currencyService.GetCurrencyInfo(kvp.Key);
                if (row == null)
                    continue;

                if (string.Equals(row.type, eggType, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return eggType.ToLowerInvariant() switch
            {
                "blue" => 2,
                "red" => 3,
                "white" => 4,
                "black" => 5,
                "yellow" => 6,
                _ => -1
            };
        }

        private void ReleaseCurrencyIcons()
        {
            foreach (var kvp in _currencyIconReferences)
            {
                ReleaseCurrencyIconReference(kvp.Key, kvp.Value);
            }

            _currencyIconReferences.Clear();
            _currencyIconCache.Clear();
            _currencyIconLoadingTasks.Clear();
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
                    Debug.Log($"[UI_Nurture] 재화 아이콘 해제 - CurrencyId: {currencyId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI_Nurture] 재화 아이콘 해제 실패 - CurrencyId: {currencyId}, err: {e.Message}");
            }
        }

        private void RefreshExperience()
        {
         //   if (_expText == null)
          //      return;

          //  float exp = _experienceService.GetExp(_eggId);
          //  float max = _experienceService.GetMaxExp(_eggId);
         //   float percent = max > 0f ? (exp / max * 100f) : 0f;
         //   _expText.text = $"경험치: {exp:0}/{max:0} ({percent:0}%)";
        }

        private void RefreshStats()
        {
            RefreshStatsAsync().Forget();
        }

        private async UniTask RefreshStatsAsync()
        {
            if (_growthActionService == null)
                return;

            int[] stats = _growthActionService.GetEggStats(_eggId);
            if (stats == null || stats.Length == 0)
                return;

            await UpdateStatTextAsync(_statCourageText, StatType.Courage, GetStatArrayValue(stats, 0));
            await UpdateStatTextAsync(_statWisdomText, StatType.Wisdom, GetStatArrayValue(stats, 1));
            await UpdateStatTextAsync(_statPurityText, StatType.Purity, GetStatArrayValue(stats, 2));
            await UpdateStatTextAsync(_statLoveText, StatType.Love, GetStatArrayValue(stats, 3));
            await UpdateStatTextAsync(_statChaosText, StatType.Chaos, GetStatArrayValue(stats, 4));

            // StatInfo 이미지의 색상 이미지들 설정
            RefreshStatColorImages();
        }

        private static int GetStatArrayValue(int[] stats, int index)
        {
            if (stats == null || index < 0 || index >= stats.Length)
                return 0;
            return stats[index];
        }

        private async UniTask UpdateStatTextAsync(TextMeshProUGUI target, StatType statType, int statValue)
        {
            if (target == null || _growthActionService == null)
                return;

            var statInfo = _growthActionService.GetStatInfo(statType);
            string fallback = GetDefaultStatDisplayName(statType);
            string localizedName = await GetLocalizedStatNameAsync(statType, statInfo?.nameLocKey, fallback);

            target.text = $"{localizedName}: {statValue}";
            // 텍스트는 검은색으로 유지
            target.color = Color.black;
        }

        private async UniTask<string> GetLocalizedStatNameAsync(StatType statType, string locKey, string fallback)
        {
            string defaultName = string.IsNullOrEmpty(fallback) ? statType.ToString() : fallback;

            if (_statNameCache.TryGetValue(statType, out var cached))
            {
                return cached;
            }

            if (_localizationService == null || string.IsNullOrEmpty(locKey))
            {
                _statNameCache[statType] = defaultName;
                return defaultName;
            }

            try
            {
                string localized = await _localizationService.GetAsync(TABLE_STAT, locKey);
                if (string.IsNullOrEmpty(localized))
                {
                    localized = defaultName;
                }

                _statNameCache[statType] = localized;
                return localized;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI_Nurture] 스탯 이름 로컬라이즈 실패 - key:{locKey}, err:{e.Message}");
                _statNameCache[statType] = defaultName;
                return defaultName;
            }
        }

        private static string GetDefaultStatDisplayName(StatType statType)
        {
            return statType switch
            {
                StatType.Courage => "용기",
                StatType.Wisdom => "지성",
                StatType.Purity => "순수",
                StatType.Love => "사랑",
                StatType.Chaos => "혼돈",
                _ => statType.ToString()
            };
        }

        private void ClearStatNameCache()
        {
            _statNameCache.Clear();
        }

        /// <summary>
        /// StatInfo 색상 이미지들 갱신
        /// </summary>
        private void RefreshStatColorImages()
        {
            if (_statColorImages == null || _statColorImages.Length == 0)
                return;

            var statTypes = new[] { StatType.Courage, StatType.Wisdom, StatType.Purity, StatType.Love, StatType.Chaos };

            for (int i = 0; i < Mathf.Min(_statColorImages.Length, statTypes.Length); i++)
            {
                if (_statColorImages[i] != null)
                {
                    var statInfo = _growthActionService.GetStatInfo(statTypes[i]);
                    if (statInfo != null)
                    {
                        // 스탯 색상을 적용하되 알파값은 최대로 설정
                        _statColorImages[i].color = new Color(statInfo.color.r, statInfo.color.g, statInfo.color.b, 1f);
                    }
                    else
                    {
                        _statColorImages[i].color = _statPlaceholderColor;
                    }
                }
            }
        }

		private void RefreshActions()
        {
			RefreshActionsAsync().Forget();
        }
        /// <summary>
        /// 선택된 알 정보를 설정 (Show 전에 호출)
        /// </summary>
        public void SetSelectedEgg(int eggId, string eggType)
        {
            _eggId = eggId;
            _defaultEggType = eggType;
            _eggType = eggType;
            UpdateCurrencyIds();

            // 알이 없으면 생성
            if (_growthActionService != null && !_growthActionService.HasEgg(_eggId))
            {
                _growthActionService.CreateEgg(_eggId);
            }

            Debug.Log($"[UI_Nurture] 알 설정됨 - ID: {_eggId}, Type: {_eggType}");
        }

		private async UniTask RefreshActionsAsync()
		{
			// TODO 육성행동 선택할때마다 레벨+1
			int eggLevel = _growthActionService.GetEggLevel(_eggId);
			var actions = _growthActionService.GetAvailableActions(_eggId, eggLevel, count: _actionSlots.Length);

			for (int i = 0; i < _actionSlots.Length; i++)
			{
				var slot = _actionSlots[i];
				if (slot == null)
					continue;

				if (i < actions.Count && actions[i] != null)
				{
					var a = actions[i];
					slot.actionId = a.id;
					slot.nameLocKey = a.nameLocKey;
					if (slot.nameText != null)
					{
						var localizedName = await GetLocalizedActionNameAsync(a.nameLocKey);
						slot.nameText.text = string.IsNullOrEmpty(localizedName) ? a.nameLocKey : localizedName;
					}

             
                    if (slot.costCommonText != null || slot.costSpecialText != null)
                    {
                        if (slot.costCommonText != null)
                            slot.costCommonText.text = a.costCommon.ToString();
                        if (slot.costSpecialText != null)
                            slot.costSpecialText.text = a.costSpecial.ToString();
                    }
                
					if (slot.button != null) slot.button.interactable = _currencyService.CanAffordAction(a, _eggId, _eggType);
				}
				else
				{
					slot.actionId = 0;
					slot.nameLocKey = string.Empty;
					if (slot.nameText != null) slot.nameText.text = "-";
                    if (slot.costCommonText != null) slot.costCommonText.text = string.Empty;
                    if (slot.costSpecialText != null) slot.costSpecialText.text = string.Empty;
              
					if (slot.button != null) slot.button.interactable = false;
				}
			}
		}

		private UniTask<string> GetLocalizedActionNameAsync(string key)
		{
			return GetLocalizedGrowthActionsEntryAsync(key, "name");
		}

		private UniTask<string> GetLocalizedActionLineAsync(string key)
		{
			return GetLocalizedGrowthActionsEntryAsync(key, "line");
		}

		private async UniTask<string> GetLocalizedGrowthActionsEntryAsync(string key, string contextLabel)
		{
			if (string.IsNullOrEmpty(key))
				return string.Empty;

			try
			{
				if (_localizationService == null)
					return key;
				var result = await _localizationService.GetAsync(TABLE_GROWTH_ACTIONS, key);
				return result;
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[UI_Nurture] 로컬라이즈 실패({contextLabel}) - key:{key}, err:{e.Message}");
				return key; // 실패 시 키를 표시(디버깅용)
			}
		}

		private void OnLocaleChanged()
		{
			// 언어가 바뀌면 행동 슬롯 텍스트를 즉시 갱신
			RefreshActionNames();
            ClearStatNameCache();
            RefreshStats();
		}

		private void RefreshActionNames()
		{
			RefreshActionTextsAsync().Forget();
		}

		private async UniTask RefreshActionTextsAsync()
		{
			for (int i = 0; i < _actionSlots.Length; i++)
			{
				var slot = _actionSlots[i];
				if (slot == null || slot.nameText == null)
					continue;

				if (!string.IsNullOrEmpty(slot.nameLocKey))
				{
					var localizedName = await GetLocalizedActionNameAsync(slot.nameLocKey);
					slot.nameText.text = string.IsNullOrEmpty(localizedName) ? slot.nameLocKey : localizedName;
				}
			}
		}
 
        private void OnClickAction(int index)
        {
            if (index < 0 || index >= _actionSlots.Length)
                return;

            var slot = _actionSlots[index];
            if (slot == null || slot.actionId <= 0)
                return;

            PerformActionAsync(slot.actionId).Forget();
        }

        private async UniTaskVoid PerformActionAsync(int actionId)
        {
            var result = await _growthActionService.PerformActionAsync(_eggId, actionId);
            if (!result.success)
            {
                Debug.LogWarning($"[UI_Nurture] 행동 실패: {result.errorMessage}");
                RefreshCurrencies();
                RefreshActions();
                return;
            }

            // 성공 시 UI 갱신
            RefreshCurrencies();
            RefreshStats();
            RefreshActions();
            ShowActionDialogueAsync(result.action).Forget();
        }

        /// <summary>
        /// 알 버튼 클릭 시 StatInfo 표시
        /// </summary>
        private void OnClickEgg()
        {
            if (_statInfoImage != null)
            {
                bool isActive = _statInfoImage.gameObject.activeSelf;
                _statInfoImage.gameObject.SetActive(!isActive);

                // StatInfo가 활성화될 때 스탯 정보 갱신
                if (!isActive)
                {
                    RefreshStats();
                }
            }
        }

        /// <summary>
        /// 진화하기 버튼 클릭 시 팝업 표시
        /// </summary>
        private void OnClickEvolution()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            OnClickEvolutionAsync().Forget();
        }

        /// <summary>
        /// 도감 버튼 클릭 시 팝업 표시
        /// </summary>
        private void OnClickCollection()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Collection);
        }

        /// <summary>
        /// 진화하기 버튼 클릭 처리 (비동기)
        /// </summary>
        private async UniTask OnClickEvolutionAsync()
        {
            var popup = UIManager.Instance.ShowPopup<UI_EvolutionPopup>(EPopupUIType.EvolutionPopup);

            if (popup != null)
            {
                // 현재 알 정보 전달
                int eggId = _eggId;
                string eggType = _growthActionService.GetEggType(eggId);
                int nurtureLevel = _growthActionService.GetEggLevel(eggId);
                int[] currentStats = _growthActionService.GetEggStats(eggId);

                // 진화 확인 팝업 표시
                await popup.ShowConfirmAsync(eggId, eggType, nurtureLevel, currentStats);

            }
        }

        // ===== EventBus 콜백들 =====
        private void OnCurrencyChanged(CurrencyBalanceChangedEvent evt)
        {
            RefreshCurrencies();
            RefreshActions();
        }

    

        private void OnStatChanged(StatChangedEvent evt)
        {
            if (evt.eggId != _eggId) return;
            RefreshStats();
        }

        private void OnActionPerformed(GrowthActionPerformedEvent evt)
        {
            if (evt.eggId != _eggId) return;
            RefreshActions();
        }

        private void OnEvolutionAttempt(EvolutionAttemptEvent evt)
        {
            if (evt.eggId != _eggId) return;
            // 필요 시 진화 버튼 활성화 등 처리 (UI 요소가 준비되면 연결)
        }

        private void OnEvolutionCompleted(EvolutionCompletedEvent evt)
        {
            if (evt.eggId != _eggId) return;
            // 진화 완료 후 초기화된 스탯/경험치 반영
            RefreshAll();
        }

        private void OnEvolutionFailed(EvolutionFailedEvent evt)
        {
            if (evt.eggId != _eggId) return;
            // 실패 메시지 노출 필요 시 팝업 연동 예정
        }

        /// <summary>
        /// 레벨 변경 이벤트 처리
        /// </summary>
        private void OnLevelChanged(LevelChangedEvent evt)
        {
            if (evt.eggId != _eggId) return;
            RefreshLevel();
            RefreshEvolutionButton();
        }
        
      

        /// <summary>
        /// 선택된 알의 애니메이션 컨트롤러를 로드하고 Animator에 할당
        /// </summary>
        private async UniTask LoadEggAnimationAsync(int eggId)
        {
            if (_eggAnimator == null)
    {
        Debug.LogWarning("[UI_Nurture] Animator 컴포넌트가 할당되지 않았습니다.");
        return;
    }

    // 같은 알이면 캐시 재사용
    if (_currentLoadedEggId == eggId && _currentAnimatorController != null)
    {
        _eggAnimator.runtimeAnimatorController = _currentAnimatorController;
        SetEggVisualState(showPlaceholder: false);
        Debug.Log($"[UI_Nurture] 이미 로드된 알 - ID: {eggId}, 캐시된 애니메이션 재활용");
        return;
    }

    // 이전 로드 작업 취소
    _loadCancellationToken?.Cancel();
    _loadCancellationToken?.Dispose();
    _loadCancellationToken = new System.Threading.CancellationTokenSource();

    Debug.Log($"[UI_Nurture] 애니메이션 로드 시작 - ID: {eggId}");

    try
    {
        var eggInfo = _growthActionService.GetEggInfo(eggId);
        if (eggInfo == null)
        {
            Debug.LogError($"[UI_Nurture] 알 ID {eggId}에 대한 정보를 찾을 수 없습니다.");
            return;
        }

        if (eggInfo.animatorController == null)
        {
            Debug.LogError($"[UI_Nurture] 알 ID {eggId}의 animatorController가 null입니다. EggTableSO에서 animatorController를 설정해주세요.");
            return;
        }

        // AssetReference 유효성 검사
        if (!eggInfo.animatorController.RuntimeKeyIsValid())
        {
            string assetGuid = eggInfo.animatorController.AssetGUID;
            Debug.LogError($"[UI_Nurture] 알 ID {eggId}의 애니메이션 컨트롤러 RuntimeKey가 유효하지 않습니다.");
            Debug.LogError($"[UI_Nurture] AssetGUID: {assetGuid}");
            Debug.LogError($"[UI_Nurture] 해결 방법: Unity 에디터에서 'Assets/08. Animations/Egg_bk/eggtype.controller' 파일을 Addressable로 마크하거나, EggTableSO의 animatorController 참조를 올바르게 설정해주세요.");
            return;
        }

        // 디버그 정보 출력
        string guid = eggInfo.animatorController.AssetGUID;
        Debug.Log($"[UI_Nurture] 애니메이션 컨트롤러 정보 - ID: {eggId}, GUID: {guid}, RuntimeKey: {eggInfo.animatorController.RuntimeKey}");

        // 이전 애니메이션 해제 (다른 알로 전환할 때만)
        if (_currentAnimatorController != null && _currentLoadedEggId > 0 && _currentLoadedEggId != eggId)
        {
            var previousEggInfo = _growthActionService.GetEggInfo(_currentLoadedEggId);
            if (previousEggInfo != null && previousEggInfo.animatorController != null)
            {
                if (previousEggInfo.animatorController.IsValid() && 
                    previousEggInfo.animatorController.OperationHandle.IsValid())
                {
                    previousEggInfo.animatorController.ReleaseAsset();
                    Debug.Log($"[UI_Nurture] 이전 애니메이션 해제 완료 - ID: {_currentLoadedEggId}");
                }
            }
            _currentAnimatorController = null;
            _currentLoadedEggId = -1;
        }

        RuntimeAnimatorController loadedController = null;

        // 이미 로드된 경우 체크
        if (eggInfo.animatorController.IsValid() && 
            eggInfo.animatorController.OperationHandle.IsValid() &&
            eggInfo.animatorController.OperationHandle.IsDone &&
            eggInfo.animatorController.Asset != null)
        {
            // 캐시된 에셋 사용
            loadedController = eggInfo.animatorController.Asset as RuntimeAnimatorController;
            Debug.Log($"[UI_Nurture] 캐시된 애니메이션 사용 - ID: {eggId}, Controller: {loadedController?.name}");
        }
        else
        {
            // 새로 로드
            Debug.Log($"[UI_Nurture] LoadAssetAsync 호출 시작 - ID: {eggId}, GUID: {eggInfo.animatorController.AssetGUID}");
            try
            {
                loadedController = await eggInfo.animatorController
                    .LoadAssetAsync<RuntimeAnimatorController>()
                    .ToUniTask(cancellationToken: _loadCancellationToken.Token);
                Debug.Log($"[UI_Nurture] LoadAssetAsync 완료 - Controller: {loadedController?.name}");
            }
            catch (UnityEngine.AddressableAssets.InvalidKeyException)
            {
                Debug.LogError($"[UI_Nurture] InvalidKeyException 발생 - ID: {eggId}");
                Debug.LogError($"[UI_Nurture] AssetGUID: {eggInfo.animatorController.AssetGUID}");
                Debug.LogError($"[UI_Nurture] RuntimeKey: {eggInfo.animatorController.RuntimeKey}");
                Debug.LogError($"[UI_Nurture] 해결 방법:");
                Debug.LogError($"[UI_Nurture] 1. Unity 에디터에서 Window > Asset Management > Addressables > Groups를 엽니다.");
                Debug.LogError($"[UI_Nurture] 2. 'Assets/08. Animations/Egg_bk/RedEgg.controller' 파일을 찾아 Addressable로 마크합니다.");
                Debug.LogError($"[UI_Nurture] 3. 또는 EggTableSO에서 해당 알의 animatorController 참조를 올바른 Addressable 에셋으로 다시 설정합니다.");
                throw; // 예외를 다시 던져서 상위 catch에서 처리
            }
        }

        // 취소되었으면 종료
        if (_loadCancellationToken.Token.IsCancellationRequested)
        {
            Debug.Log($"[UI_Nurture] 로드 취소됨 - ID: {eggId}");
            return;
        }

        if (loadedController != null)
        {
            _eggAnimator.runtimeAnimatorController = loadedController;
            _currentAnimatorController = loadedController;
            _currentLoadedEggId = eggId;
            SetEggVisualState(showPlaceholder: false);
            Debug.Log($"[UI_Nurture] 알 애니메이션 로드 완료 - ID: {eggId}, Controller: {loadedController.name}");
        }
        else
        {
            Debug.LogError($"[UI_Nurture] 애니메이션 컨트롤러 로드 실패 - ID: {eggId}");
        }
    }
    catch (System.OperationCanceledException)
    {
        Debug.Log($"[UI_Nurture] 애니메이션 로드 취소됨 - ID: {eggId}");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[UI_Nurture] 애니메이션 로드 중 오류 발생 - ID: {eggId}, Error: {e.Message}");
        Debug.LogError($"[UI_Nurture] StackTrace: {e.StackTrace}");
    }
        }

        // 씬 전환 버튼 핸들러(버튼 연결용)
        public void OnClickBackToLobby()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            // 로비 복귀 시 알방 스폰 포인트에서 시작하도록 요청
            LobbyEntryPoint.RequestSpawnAtEggRoom();
            ChangeSceneWithFade(ESceneUIType.Lobby).Forget();
        }

        public void OnClickGoToPlay()
        {
            ChangeSceneWithFade(ESceneUIType.Play).Forget();
        }

        public void OnClickSetting()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        private async UniTaskVoid ChangeSceneWithFade(ESceneUIType target)
        {
            if (_isSceneChanging)
                return;

            _isSceneChanging = true;
            try
            {
                await UIManager.Instance.ShowSceneWithFadeAsync(target);
            }
            finally
            {
                _isSceneChanging = false;
            }
        }

        private void SetContentVisible(bool isVisible)
        {
            if (_contentCanvasGroup == null)
                return;

            _contentCanvasGroup.alpha = isVisible ? 1f : 0f;
            _contentCanvasGroup.interactable = isVisible;
            _contentCanvasGroup.blocksRaycasts = isVisible;
        }

        private void SetEggVisualState(bool showPlaceholder)
        {
            if (_eggPlaceholderImage != null)
                _eggPlaceholderImage.gameObject.SetActive(showPlaceholder);

            if (_eggAnimator != null)
                _eggAnimator.gameObject.SetActive(!showPlaceholder);
        }

        private void ApplyCurrencyPlaceholder(Image target)
        {
            if (target == null)
                return;

            if (_currencyPlaceholderIcon != null)
            {
                target.sprite = _currencyPlaceholderIcon;
                target.enabled = true;
            }
            else
            {
                target.sprite = null;
                target.enabled = false;
            }
        }

        private void CancelDialogueDisplay()
        {
            if (_dialogueCancellationToken != null)
            {
                try
                {
                    if (!_dialogueCancellationToken.IsCancellationRequested)
                    {
                        _dialogueCancellationToken.Cancel();
                    }
                }
                finally
                {
                    _dialogueCancellationToken.Dispose();
                    _dialogueCancellationToken = null;
                }
            }
        }

        private void HideActionDialogueImmediate()
        {
            if (_actionDialogueText != null)
            {
                _actionDialogueText.text = string.Empty;
            }

            SetActionDialogueActive(false);
        }

        private void SetActionDialogueActive(bool isActive)
        {
            if (_actionDialogueRoot != null)
            {
                if (_actionDialogueRoot.activeSelf != isActive)
                {
                    _actionDialogueRoot.SetActive(isActive);
                }
                return;
            }

            if (_actionDialogueText != null && _actionDialogueText.gameObject.activeSelf != isActive)
            {
                _actionDialogueText.gameObject.SetActive(isActive);
            }
        }

        private string GetRandomDialogueKey(GrowthConditionPoolSO.ConditionEntry action)
        {
            if (action == null)
                return string.Empty;

            _dialogueKeyBuffer.Clear();

            if (!string.IsNullOrWhiteSpace(action.line1))
                _dialogueKeyBuffer.Add(action.line1);
            if (!string.IsNullOrWhiteSpace(action.line2))
                _dialogueKeyBuffer.Add(action.line2);
            if (!string.IsNullOrWhiteSpace(action.line3))
                _dialogueKeyBuffer.Add(action.line3);

            if (_dialogueKeyBuffer.Count == 0)
                return string.Empty;

            int index = UnityEngine.Random.Range(0, _dialogueKeyBuffer.Count);
            return _dialogueKeyBuffer[index];
        }

        private async UniTaskVoid ShowActionDialogueAsync(GrowthConditionPoolSO.ConditionEntry action)
        {
            CancelDialogueDisplay();

            if (action == null)
            {
                HideActionDialogueImmediate();
                return;
            }

            string lineKey = GetRandomDialogueKey(action);
            if (string.IsNullOrEmpty(lineKey))
            {
                HideActionDialogueImmediate();
                return;
            }

            var currentCts = new CancellationTokenSource();
            _dialogueCancellationToken = currentCts;
            var token = currentCts.Token;

            try
            {
                string localizedLine = await GetLocalizedActionLineAsync(lineKey);

                if (_dialogueCancellationToken != currentCts || token.IsCancellationRequested)
                {
                    return;
                }

                if (string.IsNullOrEmpty(localizedLine))
                {
                    localizedLine = lineKey;
                }

                if (_actionDialogueText != null)
                {
                    _actionDialogueText.text = localizedLine;
                }

                SetActionDialogueActive(true);

                if (_actionDialogueDisplayDuration <= 0f)
                {
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_actionDialogueDisplayDuration), cancellationToken: token);

                if (_dialogueCancellationToken != currentCts || token.IsCancellationRequested)
                {
                    return;
                }

                HideActionDialogueImmediate();
            }
            catch (OperationCanceledException)
            {
                // 취소는 무시
            }
            finally
            {
                if (_dialogueCancellationToken == currentCts)
                {
                    currentCts.Dispose();
                    _dialogueCancellationToken = null;
                }
                else
                {
                    currentCts.Dispose();
                }
            }
        }
    }
}


