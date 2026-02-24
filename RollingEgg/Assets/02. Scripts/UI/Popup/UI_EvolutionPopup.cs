using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using RollingEgg.Core;
using RollingEgg.Data;
using RollingEgg.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    /// <summary>
    /// 진화 팝업 UI
    /// - 진화 조건 확인 및 표시
    /// - 진화 시도 및 결과 처리
    /// - 진화 애니메이션 및 효과 표시
    /// 
    /// 두 가지 상태:
    /// 1. 진화 확인 (ConfirmState): 예상 진화 결과 3종 표시
    /// 2. 진화 완료 (ResultState): 최종 진화 결과 표시
    /// </summary>
    public class UI_EvolutionPopup : UI_Popup
    {
        // ==================== 진화 확인 단계 UI ====================
        [Header("진화 확인 패널")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TextMeshProUGUI _confirmTitleText;
        [SerializeField] private TextMeshProUGUI _confirmMessageText;

        [Header("예상 진화 결과 (3종)")]
        [SerializeField] private EvolutionOutcomeSlot[] _outcomeSlots = new EvolutionOutcomeSlot[3];

        [Header("진화 확인 버튼")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _evolveButton;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;
        [SerializeField] private TextMeshProUGUI _evolveButtonText;

        // ==================== 진화 완료 단계 UI ====================
        [Header("진화 완료 패널")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TextMeshProUGUI _resultTitleText;
        
        [Header("진화 전후 이미지")]
        [SerializeField] private Image _beforeEggImage;
        [SerializeField] private TextMeshProUGUI _beforeEggNameText;
        [SerializeField] private Image _evolutionArrowImage;
        [SerializeField] private Image _afterFormImage;
        [SerializeField] private TextMeshProUGUI _afterFormNameText;

        [Header("진화체 대사 및 결과 메시지")]
        [SerializeField] private TextMeshProUGUI _formDialogueText;
        [SerializeField] private TextMeshProUGUI _resultMessageText;

        [Header("진화 완료 버튼")]
        [SerializeField] private Button _confirmResultButton;
       // [SerializeField] private TextMeshProUGUI _confirmResultButtonText;

        [Header("플레이스홀더")]
        [SerializeField] private Sprite _defaultOutcomeIcon;
        [SerializeField] private Sprite _defaultResultIcon;

        // ==================== 진화 예상 슬롯 구조 ====================
        [System.Serializable]
        public class EvolutionOutcomeSlot
        {
            public GameObject root;
            public Image formIcon;
            public TextMeshProUGUI formNameText;
            public TextMeshProUGUI probabilityText;
        }

        // ==================== 서비스 참조 ====================
        private IEvolutionService _evolutionService;
        private ICollectionService _collectionService;
        private IGrowthActionService _growthActionService;
        private IResourceService _resourceService;
        private ILocalizationService _localizationService;
        private IAudioService _audioService;
        private IEventBus _eventBus;

        private const string TABLE_EVOLUTION = "UI_Evolution";
        private const string TABLE_COLLECTION = "UI_Collection";
        private const string TABLE_EVOLVED_FORM = "EvolvedForm";

        private const string FALLBACK_CONFIRM_TITLE = "진화";
        private const string FALLBACK_CONFIRM_MESSAGE = "알에 큰 변화가 생길 것 같아요!\n지금 진화에 도전할까요?";
        private const string FALLBACK_CANCEL_BUTTON = "지금 안 할래요";
        private const string FALLBACK_EVOLVE_BUTTON = "지금 진화하자!";
        private const string FALLBACK_RESULT_TITLE = "진화 완료";
        private const string FALLBACK_FORM_DIALOGUE_TEMPLATE = "안녕 나는 {0}!";
        private const string FALLBACK_RESULT_NEW = "새로운 진화체를 획득하여\n도감에 등록되었습니다.";
        private const string FALLBACK_RESULT_DUPLICATE_TEMPLATE = "중복 진화체를 획득하여\n{0}개를 획득하였습니다.";

        // ==================== 팝업 상태 ====================
        private int _currentEggId;
        private string _currentEggType;
        private int _currentNurtureLevel;
        private int[] _currentStats;
        private List<PredictedEvolutionOutcome> _predictedOutcomes;
        private EvolutionResult _evolutionResult;
        private CancellationTokenSource _localeRefreshCts;

        public override async UniTask InitializeAsync()
        {
            // 서비스 가져오기
            _evolutionService = ServiceLocator.Get<IEvolutionService>();
            _collectionService = ServiceLocator.Get<ICollectionService>();
            _growthActionService = ServiceLocator.Get<IGrowthActionService>();
            _resourceService = ServiceLocator.Get<IResourceService>();
            _localizationService = ServiceLocator.Get<ILocalizationService>();
            _audioService = ServiceLocator.Get<IAudioService>();
            _eventBus = ServiceLocator.Get<IEventBus>();

            // 버튼 이벤트 등록 (null 체크 추가)
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelButtonClicked);
            else
                Debug.LogWarning("[UI_EvolutionPopup] _cancelButton이 연결되지 않았습니다.");

            if (_evolveButton != null)
                _evolveButton.onClick.AddListener(OnEvolveButtonClicked);
            else
                Debug.LogWarning("[UI_EvolutionPopup] _evolveButton이 연결되지 않았습니다.");

            if (_confirmResultButton != null)
                _confirmResultButton.onClick.AddListener(OnConfirmResultButtonClicked);
            else
                Debug.LogWarning("[UI_EvolutionPopup] _confirmResultButton이 연결되지 않았습니다.");

            ResetUIState();
            Debug.Log("[UI_EvolutionPopup] 초기화 완료");
            await UniTask.Yield();
        }

        /// <summary>
        /// 진화 확인 팝업 표시
        /// </summary>
        public async UniTask ShowConfirmAsync(int eggId, string eggType, int nurtureLevel, int[] currentStats)
        {
            _currentEggId = eggId;
            _currentEggType = eggType;
            _currentNurtureLevel = nurtureLevel;
            _currentStats = currentStats;

            ResetConfirmPanel();

            // 진화 조건 체크
            var condition = _evolutionService.CheckEvolutionCondition(eggId, nurtureLevel);
            if (!condition.canEvolve)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 진화 조건 미충족: {condition.errorMessage}");
                return;
            }

            // 예상 진화 결과 가져오기
            _predictedOutcomes = _evolutionService.GetPredictedEvolutionOutcomes(eggId, eggType, nurtureLevel, currentStats);
            
            if (_predictedOutcomes == null || _predictedOutcomes.Count == 0)
            {
                Debug.LogWarning("[UI_EvolutionPopup] 예상 진화 결과를 가져올 수 없습니다.");
                return;
            }

            // UI 업데이트
            await UpdateConfirmPanelAsync();

            // 진화 확인 패널 표시
            _confirmPanel.SetActive(true);
            _resultPanel.SetActive(false);

            // 애니메이션 (옵션)
            _confirmPanel.transform.localScale = Vector3.zero;
            _confirmPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            Debug.Log($"[UI_EvolutionPopup] 진화 확인 팝업 표시: 알ID={eggId}, 예상결과={_predictedOutcomes.Count}개");
        }

        /// <summary>
        /// 진화 확인 패널 UI 업데이트
        /// </summary>
        private async UniTask UpdateConfirmPanelAsync()
        {
            // 타이틀 및 메시지
            if (_confirmTitleText != null)
                _confirmTitleText.text = await GetEvolutionTextAsync("evolution_confirm_title", FALLBACK_CONFIRM_TITLE);

            if (_confirmMessageText != null)
                _confirmMessageText.text = await GetEvolutionTextAsync("evolution_confirm_message", FALLBACK_CONFIRM_MESSAGE);

            // 버튼 텍스트
            if (_cancelButtonText != null)
                _cancelButtonText.text = await GetEvolutionTextAsync("evolution_button_cancel", FALLBACK_CANCEL_BUTTON);

            if (_evolveButtonText != null)
                _evolveButtonText.text = await GetEvolutionTextAsync("evolution_button_confirm", FALLBACK_EVOLVE_BUTTON);

            // 예상 진화 결과 슬롯 업데이트
            for (int i = 0; i < _outcomeSlots.Length; i++)
            {
                if (i < _predictedOutcomes.Count)
                {
                    var outcome = _predictedOutcomes[i];
                    var slot = _outcomeSlots[i];

                    ApplyOutcomePlaceholder(slot);

                    slot.root.SetActive(true);

                    // 진화체 이름 (로컬라이제이션)
                    string formName = await _localizationService.GetAsync(TABLE_EVOLVED_FORM, outcome.formName);
                    if (string.IsNullOrEmpty(formName))
                        formName = outcome.formName; // 폴백
                    slot.formNameText.text = $"{formName}";

                    // 확률 표시
                    slot.probabilityText.text = $"{outcome.probabilityPercent:F0}%";

                    // 아이콘 로드 (Addressables)
                    if (!string.IsNullOrEmpty(outcome.iconAddress))
                    {
                        try
                        {
                            var sprite = await _resourceService.LoadAssetAsync<Sprite>(outcome.iconAddress);
                            if (sprite != null)
                            {
                                slot.formIcon.sprite = sprite;
                                slot.formIcon.enabled = true;
                            }
                            else
                            {
                                ApplyOutcomePlaceholder(slot);
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[UI_EvolutionPopup] 아이콘 로드 실패: {outcome.iconAddress}, {e.Message}");
                            ApplyOutcomePlaceholder(slot);
                        }
                    }
                    else
                    {
                        ApplyOutcomePlaceholder(slot);
                    }
                }
                else
                {
                    // 슬롯 비활성화
                    _outcomeSlots[i].root.SetActive(false);
                    ApplyOutcomePlaceholder(_outcomeSlots[i]);
                }
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// "지금 안 할래요" 버튼 클릭
        /// </summary>
        private void OnCancelButtonClicked()
        {
            Debug.Log("[UI_EvolutionPopup] 진화 취소");

            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            ClosePopup();
        }

        /// <summary>
        /// "지금 진화하자!" 버튼 클릭
        /// </summary>
        private async void OnEvolveButtonClicked()
        {
            Debug.Log("[UI_EvolutionPopup] 진화 시도");

            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick).Forget();

            // 버튼 비활성화 (중복 클릭 방지)
            _evolveButton.interactable = false;
            _cancelButton.interactable = false;

            // 진화 실행
            _evolutionResult = await _evolutionService.AttemptEvolutionAsync(_currentEggId, _currentNurtureLevel, _currentStats);

            if (_evolutionResult.success)
            {
                // 진화 완료 패널 표시
                await ShowResultAsync(_evolutionResult);

                // 진화 완료 후 알 리셋
                _evolutionService.ResetEggAfterEvolution(_currentEggId, _evolutionResult.evolutionFormId);
            }
            else
            {
                Debug.LogError($"[UI_EvolutionPopup] 진화 실패: {_evolutionResult.errorMessage}");
                ClosePopup();
            }

            // 버튼 다시 활성화
            _evolveButton.interactable = true;
            _cancelButton.interactable = true;
        }

        /// <summary>
        /// 진화 완료 결과 표시
        /// </summary>
        private async UniTask ShowResultAsync(EvolutionResult result)
        {
            // 진화 확인 패널 숨기기
            _confirmPanel.SetActive(false);

            ResetResultPanel();
            await UpdateResultTextsAsync(result, applyImages: true);

            // 애니메이션
            _resultPanel.SetActive(true);
            _resultPanel.transform.localScale = Vector3.zero;
            _resultPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            Debug.Log($"[UI_EvolutionPopup] 진화 완료 결과 표시: {result.evolutionName}, 신규={result.isNewForm}");
            await UniTask.Yield();
        }

        /// <summary>
        /// 진화 완료 후 "확인" 버튼 클릭
        /// </summary>
        private void OnConfirmResultButtonClicked()
        {
            Debug.Log("[UI_EvolutionPopup] 진화 완료 확인");
            ClosePopup();
        }

        /// <summary>
        /// 팝업 닫기
        /// </summary>
        private void ClosePopup()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
        }

        public override void OnShow()
        {
            base.OnShow();
            SubscribeLocaleChanged();
            ResetUIState();
        }

        public override void OnHide()
        {
            base.OnHide();
            UnsubscribeLocaleChanged();
            CancelLocaleRefresh();
            ResetUIState();
        }

        private void OnDestroy()
        {
            UnsubscribeLocaleChanged();
            CancelLocaleRefresh();
        }

        private void ResetUIState()
        {
            if (_confirmPanel != null)
                _confirmPanel.SetActive(false);

            if (_resultPanel != null)
                _resultPanel.SetActive(false);

            ResetConfirmPanel();
            ResetResultPanel();
        }

        private void ResetConfirmPanel()
        {
            if (_confirmTitleText != null)
                _confirmTitleText.text = string.Empty;

            if (_confirmMessageText != null)
                _confirmMessageText.text = string.Empty;

            if (_cancelButtonText != null)
                _cancelButtonText.text = string.Empty;

            if (_evolveButtonText != null)
                _evolveButtonText.text = string.Empty;

            if (_outcomeSlots == null)
                return;

            foreach (var slot in _outcomeSlots)
            {
                if (slot == null)
                    continue;

                if (slot.root != null)
                    slot.root.SetActive(false);

                ApplyOutcomePlaceholder(slot);
            }
        }

        private void ResetResultPanel()
        {
            if (_resultTitleText != null)
                _resultTitleText.text = string.Empty;

            if (_beforeEggNameText != null)
                _beforeEggNameText.text = string.Empty;

            if (_afterFormNameText != null)
                _afterFormNameText.text = string.Empty;

            if (_formDialogueText != null)
                _formDialogueText.text = string.Empty;

            if (_resultMessageText != null)
                _resultMessageText.text = string.Empty;

            if (_afterFormImage != null)
            {
                if (_defaultResultIcon != null)
                {
                    _afterFormImage.sprite = _defaultResultIcon;
                    _afterFormImage.enabled = true;
                }
                else
                {
                    _afterFormImage.sprite = null;
                    _afterFormImage.enabled = false;
                }
            }
        }

        private void ApplyOutcomePlaceholder(EvolutionOutcomeSlot slot)
        {
            if (slot.formIcon != null)
            {
                if (_defaultOutcomeIcon != null)
                {
                    slot.formIcon.sprite = _defaultOutcomeIcon;
                    slot.formIcon.enabled = true;
                }
                else
                {
                    slot.formIcon.sprite = null;
                    slot.formIcon.enabled = false;
                }
            }

            if (slot.formNameText != null)
                slot.formNameText.text = string.Empty;

            if (slot.probabilityText != null)
                slot.probabilityText.text = string.Empty;
        }

        private async UniTask<string> BuildCollectionBuffDescriptionAsync(EvolutionForm formInfo)
        {
            if (formInfo.buffType == EvolvedFormTableSO.BuffType.None || formInfo.buffValuePercent <= 0f)
                return string.Empty;

            string targetEggType = string.IsNullOrWhiteSpace(formInfo.buffTargetEggType)
                ? formInfo.eggType
                : formInfo.buffTargetEggType;

            string valueText = $"+{formInfo.buffValuePercent:0.#}%";

            if (_localizationService == null)
                return BuildCollectionBuffDescriptionFallback(formInfo);

            try
            {
                switch (formInfo.buffType)
                {
                    case EvolvedFormTableSO.BuffType.CommonCurrencyGain:
                        return await _localizationService.GetAsync(
                            TABLE_COLLECTION,
                            "collection_buff_common_currency",
                            new { Value = valueText });
                    case EvolvedFormTableSO.BuffType.SpecialCurrencyGain:
                        string colorLabel = await GetEggColorLabelAsync(targetEggType);
                        return await _localizationService.GetAsync(
                            TABLE_COLLECTION,
                            "collection_buff_special_currency",
                            new
                            {
                                ColorLabel = colorLabel,
                                Value = valueText
                            });
                    case EvolvedFormTableSO.BuffType.DuplicateRewardBonus:
                        return await _localizationService.GetAsync(
                            TABLE_COLLECTION,
                            "collection_buff_duplicate_reward",
                            new { Value = valueText });
                    default:
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 버프 설명 로컬라이즈 실패: {ex.Message}");
                return BuildCollectionBuffDescriptionFallback(formInfo);
            }
        }

        private async UniTask UpdateResultTextsAsync(EvolutionResult result, bool applyImages)
        {
            if (result.formInfo.formId == 0)
                return;

            if (_resultTitleText != null)
                _resultTitleText.text = await GetEvolutionTextAsync("evolution_result_title", FALLBACK_RESULT_TITLE);

            string eggName = await GetEggLocalizedNameAsync();
            if (_beforeEggNameText != null)
                _beforeEggNameText.text = eggName;

            string formName = await GetFormLocalizedNameAsync(result.formInfo.formName);
            if (_afterFormNameText != null)
                _afterFormNameText.text = formName;

            if (_formDialogueText != null)
            {
                string dialogueFallback = string.Format(FALLBACK_FORM_DIALOGUE_TEMPLATE, formName);
                string dialogue = await GetFormLocalizedSpeechAsync(result.formInfo.formName, dialogueFallback);
                _formDialogueText.text = dialogue;
            }

            if (_resultMessageText != null)
            {
                string resultMessage = result.isNewForm
                    ? await GetEvolutionTextAsync("evolution_result_new", FALLBACK_RESULT_NEW)
                    : await GetEvolutionTextAsync(
                        "evolution_result_duplicate",
                        string.Format(FALLBACK_RESULT_DUPLICATE_TEMPLATE, result.duplicateReward),
                        new { RewardAmount = result.duplicateReward });

                _resultMessageText.text = resultMessage;

                string buffDescription = await BuildCollectionBuffDescriptionAsync(result.formInfo);
                if (!string.IsNullOrEmpty(buffDescription))
                    _resultMessageText.text += $"\n<color=#00FF00>{buffDescription}</color>";
            }

            if (!applyImages)
                return;

            await UpdateResultImagesAsync(result);
        }

        private async UniTask<string> GetEggLocalizedNameAsync()
        {
            if (_localizationService == null)
                return _currentEggType;

            try
            {
                string name = await _localizationService.GetAsync("Eggs", $"egg_{_currentEggType}");
                return string.IsNullOrEmpty(name) ? _currentEggType : name;
            }
            catch
            {
                return _currentEggType;
            }
        }

        private async UniTask<string> GetFormLocalizedNameAsync(string formKey)
        {
            if (_localizationService == null)
                return formKey;

            try
            {
                string name = await _localizationService.GetAsync(TABLE_EVOLVED_FORM, formKey);
                return string.IsNullOrEmpty(name) ? formKey : name;
            }
            catch
            {
                return formKey;
            }
        }

        private async UniTask<string> GetFormLocalizedSpeechAsync(string formKey, string fallback)
        {
            if (_localizationService == null || string.IsNullOrEmpty(formKey))
                return fallback;

            string speechKey = $"{formKey}_Speech";
            try
            {
                string speech = await _localizationService.GetAsync(TABLE_EVOLVED_FORM, speechKey);
                return string.IsNullOrEmpty(speech) ? fallback : speech;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 진화체 대사 로컬라이즈 실패: {speechKey}, {ex.Message}");
                return fallback;
            }
        }

        private async UniTask UpdateResultImagesAsync(EvolutionResult result)
        {
            if (_afterFormImage == null)
                return;

            if (!string.IsNullOrEmpty(result.formInfo.iconAddress))
            {
                try
                {
                    var sprite = await _resourceService.LoadAssetAsync<Sprite>(result.formInfo.iconAddress);
                    if (sprite != null)
                    {
                        _afterFormImage.sprite = sprite;
                        _afterFormImage.enabled = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UI_EvolutionPopup] 진화체 아이콘 로드 실패: {e.Message}");
                }
            }

            if (_defaultResultIcon != null)
            {
                _afterFormImage.sprite = _defaultResultIcon;
                _afterFormImage.enabled = true;
            }
            else
            {
                _afterFormImage.enabled = false;
            }
        }

        private async UniTask<string> GetEvolutionTextAsync(string key, string fallback, params object[] args)
        {
            if (_localizationService == null)
                return fallback;

            try
            {
                string localized = await _localizationService.GetAsync(TABLE_EVOLUTION, key, args ?? Array.Empty<object>());
                return string.IsNullOrEmpty(localized) ? fallback : localized;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 로컬라이즈 실패: {key}, {ex.Message}");
                return fallback;
            }
        }

        private void SubscribeLocaleChanged()
        {
            if (_localizationService == null)
                return;

            _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            _localizationService.OnLocaleChanged += HandleLocaleChanged;
        }

        private void UnsubscribeLocaleChanged()
        {
            if (_localizationService == null)
                return;

            _localizationService.OnLocaleChanged -= HandleLocaleChanged;
        }

        private void HandleLocaleChanged()
        {
            RefreshLocaleAsync().Forget();
        }

        private void CancelLocaleRefresh()
        {
            if (_localeRefreshCts == null)
                return;

            _localeRefreshCts.Cancel();
            _localeRefreshCts.Dispose();
            _localeRefreshCts = null;
        }

        private async UniTask RefreshLocaleAsync()
        {
            if (!isActiveAndEnabled)
                return;

            CancelLocaleRefresh();
            _localeRefreshCts = new CancellationTokenSource();
            var token = _localeRefreshCts.Token;

            try
            {
                if (_confirmPanel != null && _confirmPanel.activeSelf)
                {
                    await UpdateConfirmPanelAsync();
                }

                if (token.IsCancellationRequested)
                    return;

                if (_resultPanel != null && _resultPanel.activeSelf && _evolutionResult.success)
                {
                    await UpdateResultTextsAsync(_evolutionResult, applyImages: false);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 로캘 갱신 실패: {ex.Message}");
            }
            finally
            {
                CancelLocaleRefresh();
            }
        }

        private string BuildCollectionBuffDescriptionFallback(EvolutionForm formInfo)
        {
            if (formInfo.buffType == EvolvedFormTableSO.BuffType.None || formInfo.buffValuePercent <= 0f)
                return string.Empty;

            string targetEggType = string.IsNullOrWhiteSpace(formInfo.buffTargetEggType)
                ? formInfo.eggType
                : formInfo.buffTargetEggType;

            string valueText = $"+{formInfo.buffValuePercent:0.#}%";

            switch (formInfo.buffType)
            {
                case EvolvedFormTableSO.BuffType.CommonCurrencyGain:
                    return $"공용 재화 획득량 {valueText}";
                case EvolvedFormTableSO.BuffType.SpecialCurrencyGain:
                    return $"{GetEggColorLabelFallback(targetEggType)} 전용 재화 획득량 {valueText}";
                case EvolvedFormTableSO.BuffType.DuplicateRewardBonus:
                    return $"중복 보상 재화 {valueText}";
                default:
                    return string.Empty;
            }
        }

        private async UniTask<string> GetEggColorLabelAsync(string eggType)
        {
            if (_localizationService == null)
                return GetEggColorLabelFallback(eggType);

            string key = ResolveColorLabelKey(eggType);

            try
            {
                string localized = await _localizationService.GetAsync(TABLE_COLLECTION, key);
                return string.IsNullOrEmpty(localized) ? GetEggColorLabelFallback(eggType) : localized;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_EvolutionPopup] 색상 라벨 로컬라이즈 실패: {ex.Message}");
                return GetEggColorLabelFallback(eggType);
            }
        }

        private string ResolveColorLabelKey(string eggType)
        {
            if (string.IsNullOrWhiteSpace(eggType))
                return "collection_color_default_label";

            switch (eggType.Trim().ToLowerInvariant())
            {
                case "blue":
                    return "collection_color_blue_label";
                case "red":
                    return "collection_color_red_label";
                case "white":
                    return "collection_color_white_label";
                case "black":
                    return "collection_color_black_label";
                case "yellow":
                    return "collection_color_yellow_label";
                default:
                    return "collection_color_default_label";
            }
        }

        private string GetEggColorLabelFallback(string eggType)
        {
            if (string.IsNullOrWhiteSpace(eggType))
                return "전용";

            return eggType.Trim().ToLowerInvariant() switch
            {
                "blue" => "파란",
                "red" => "빨간",
                "yellow" => "노란",
                "white" => "하얀",
                "black" => "검은",
                _ => eggType
            };
        }
    }
}