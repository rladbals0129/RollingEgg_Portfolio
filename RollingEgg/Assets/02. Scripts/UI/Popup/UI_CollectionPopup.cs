using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.Data;

namespace RollingEgg.UI
{
    public class UI_CollectionPopup : UI_Popup
    {
        [Header("References")]
        [SerializeField] private UI_CollectionSlot _slotPrefab;
        [SerializeField] private Transform _contentRoot;
        
        [Header("Tabs")]
        [SerializeField] private Toggle _tabBlue;
        [SerializeField] private Toggle _tabRed;
        [SerializeField] private Toggle _tabWhite;
        [SerializeField] private Toggle _tabBlack;
        [SerializeField] private Toggle _tabYellow;

        [Header("Tooltip")]
        [SerializeField] private GameObject _panelTooltip;
        [SerializeField] private TMP_Text _txtTooltipDesc;
        [SerializeField] private RectTransform _tooltipRect;

        [Header("Buttons")]
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnBackToLobby;
        [SerializeField] private Button _btnOption;

        private const string TABLE_COLLECTION = "UI_Collection";
        private const string LOCKED_DESC_FALLBACK = "아직 획득하지 못한 진화체입니다.";
        private const string NO_BUFF_FALLBACK = "적용된 버프가 없습니다.";

        private ICollectionService _collectionService;
        private ILocalizationService _localizationService;
        private IAudioService _audioService;

        private EvolvedFormTableSO _tableSO;
        
        private List<UI_CollectionSlot> _spawnedSlots = new List<UI_CollectionSlot>();
        private string _currentEggType = "blue"; // 기본 선택
        private CancellationTokenSource _tooltipCts;

        public override async UniTask InitializeAsync()
        {
            await base.InitializeAsync();

            _collectionService = ServiceLocator.Get<ICollectionService>();
            _localizationService = ServiceLocator.Get<ILocalizationService>();
            _audioService = ServiceLocator.Get<IAudioService>();
            var resourceService = ServiceLocator.Get<IResourceService>();

            if (resourceService != null)
            {
                try
                {
                    _tableSO = await resourceService.LoadAssetAsync<EvolvedFormTableSO>("EvolvedFormTableSO");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[UI_CollectionPopup] Failed to load table: {ex.Message}");
                }
            }

            BindEvents();
        }

        private void BindEvents()
        {
            if (_btnClose != null)
                _btnClose.onClick.AddListener(CloseSelf);
            
            if (_btnBackToLobby != null)
                _btnBackToLobby.onClick.AddListener(OnBackToLobby);
            
            if (_btnOption != null)
                _btnOption.onClick.AddListener(OnOption);

            // 탭 이벤트
            if (_tabBlue != null) _tabBlue.onValueChanged.AddListener((isOn) => { if (isOn) OnTabChanged("blue"); });
            if (_tabRed != null) _tabRed.onValueChanged.AddListener((isOn) => { if (isOn) OnTabChanged("red"); });
            if (_tabWhite != null) _tabWhite.onValueChanged.AddListener((isOn) => { if (isOn) OnTabChanged("white"); });
            if (_tabBlack != null) _tabBlack.onValueChanged.AddListener((isOn) => { if (isOn) OnTabChanged("black"); });
            if (_tabYellow != null) _tabYellow.onValueChanged.AddListener((isOn) => { if (isOn) OnTabChanged("yellow"); });
        }

        public override void OnShow()
        {
            base.OnShow();
            
            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged += OnLocaleChanged;
            }

            // 팝업이 열릴 때 기본 탭(파란 알)으로 초기화하거나 이전 상태 기억
            if (_tabBlue != null) _tabBlue.isOn = true;
            
            HideTooltipPanel();
            RefreshList(_currentEggType);
        }

        public override void OnHide()
        {
            base.OnHide();

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged -= OnLocaleChanged;
            }

            CancelTooltipTask();
            HideTooltipPanel();
        }

        private void OnLocaleChanged()
        {
            RefreshList(_currentEggType);
        }

        private void OnTabChanged(string eggType)
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            _currentEggType = eggType;
            RefreshList(eggType);
        }

        private void RefreshList(string eggType)
        {
            HideTooltipPanel();

            if (_slotPrefab == null || _contentRoot == null)
            {
                Debug.LogWarning("[UI_CollectionPopup] 슬롯 프리팹 또는 콘텐츠 루트가 지정되지 않았습니다.");
                return;
            }

            var uiManager = UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError("[UI_CollectionPopup] UIManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            // 기존 슬롯 반환
            foreach (var slot in _spawnedSlots)
            {
                if (slot == null)
                    continue;

                uiManager.ReturnUI(slot);
            }
            _spawnedSlots.Clear();

            if (_tableSO == null) return;

            // 데이터 필터링
            var targetRows = _tableSO.Rows
                .Where(r => r != null && r.eggType.Equals(eggType, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 슬롯 생성
            foreach (var row in targetRows)
            {
                var slot = uiManager.RentUI(_slotPrefab, _contentRoot);
                if (slot == null)
                    continue;

                bool isUnlocked = _collectionService.IsFormRegistered(row.id);

                slot.Init(OnSlotHoverEnter, OnSlotHoverExit);
                slot.SetData(row, isUnlocked);

                _spawnedSlots.Add(slot);
            }

            RebuildContentLayout();
        }

        #region Tooltip Logic
        private void OnSlotHoverEnter(EvolvedFormTableSO.EvolvedFormRow data, bool isUnlocked, RectTransform slotRect)
        {
            if (_panelTooltip == null || _txtTooltipDesc == null)
                return;

            CancelTooltipTask();
            _tooltipCts = new CancellationTokenSource();
            ShowTooltipAsync(data, isUnlocked, slotRect, _tooltipCts.Token).Forget();
        }

        private void OnSlotHoverExit()
        {
            CancelTooltipTask();
            HideTooltipPanel();
        }

        private async UniTask ShowTooltipAsync(EvolvedFormTableSO.EvolvedFormRow data, bool isUnlocked, RectTransform slotRect, CancellationToken token)
        {
            string tooltipMessage = await BuildTooltipMessageAsync(data, isUnlocked, token);
            if (token.IsCancellationRequested || string.IsNullOrEmpty(tooltipMessage))
                return;

            if (_panelTooltip == null || _txtTooltipDesc == null)
                return;

            if (isUnlocked)
            {
                _txtTooltipDesc.text = $"<color=#00FF00>{tooltipMessage}</color>";
            }
            else
            {
                _txtTooltipDesc.text = tooltipMessage;
            }

            _panelTooltip.SetActive(true);
            UpdateTooltipPosition(slotRect);
        }

        private void UpdateTooltipPosition(RectTransform slotRect)
        {
            // 구현 간소화: 툴팁을 슬롯 바로 위에 띄움
            // 실제로는 화면 밖으로 나가는지 체크 필요
            if (_tooltipRect == null) return;

            // 슬롯의 월드 좌표
            Vector3 slotWorldPos = slotRect.position;
            
            // 툴팁을 슬롯 위쪽으로 살짝 띄움
            // 로컬 좌표 변환 등이 필요할 수 있으나, 툴팁이 같은 Canvas 안에 있고 Overlay라면 월드 좌표 사용 가능
            // 혹은 Screen Space Camera 모드에 따라 다름.
            
            // 간단히 마우스 위치 활용 (EventSystem) 혹은 슬롯 위치 활용
            // 여기서는 슬롯 위치 기준
            _tooltipRect.position = slotWorldPos;
            // 오프셋 적용 (슬롯 높이의 절반 + 툴팁 높이의 절반 등)
            // RectTransform 피벗 설정에 따라 다름
        }

        private async UniTask<string> BuildTooltipMessageAsync(EvolvedFormTableSO.EvolvedFormRow data, bool isUnlocked, CancellationToken token)
        {
            if (_localizationService == null)
                return isUnlocked ? BuildBuffDescriptionFallback(data) : LOCKED_DESC_FALLBACK;

            try
            {
                if (!isUnlocked)
                {
                    string lockedDesc = await _localizationService
                        .GetAsync(TABLE_COLLECTION, "collection_slot_locked_desc")
                        .AttachExternalCancellation(token);

                    return string.IsNullOrEmpty(lockedDesc) ? LOCKED_DESC_FALLBACK : lockedDesc;
                }

                string buffDescription = await GetBuffDescriptionAsync(data, token);
                if (!string.IsNullOrEmpty(buffDescription))
                    return buffDescription;

                string fallbackBuff = BuildBuffDescriptionFallback(data);
                if (!string.IsNullOrEmpty(fallbackBuff))
                    return fallbackBuff;

                string noBuff = await _localizationService
                    .GetAsync(TABLE_COLLECTION, "collection_tooltip_no_buff")
                    .AttachExternalCancellation(token);

                return string.IsNullOrEmpty(noBuff) ? NO_BUFF_FALLBACK : noBuff;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
        }

        private async UniTask<string> GetBuffDescriptionAsync(EvolvedFormTableSO.EvolvedFormRow row, CancellationToken token)
        {
            if (row == null || row.buffType == EvolvedFormTableSO.BuffType.None || row.buffValuePercent <= 0f)
                return string.Empty;

            string targetEggType = string.IsNullOrWhiteSpace(row.buffTargetEggType) ? row.eggType : row.buffTargetEggType;
            string valueText = $"+{row.buffValuePercent:0.#}%";

            try
            {
                switch (row.buffType)
                {
                    case EvolvedFormTableSO.BuffType.CommonCurrencyGain:
                        return await _localizationService
                            .GetAsync(TABLE_COLLECTION, "collection_buff_common_currency",
                                new { Value = valueText })
                            .AttachExternalCancellation(token);
                    case EvolvedFormTableSO.BuffType.SpecialCurrencyGain:
                        string colorLabel = await GetEggColorLabelAsync(targetEggType, token);
                        return await _localizationService
                            .GetAsync(TABLE_COLLECTION, "collection_buff_special_currency",
                                new
                                {
                                    ColorLabel = colorLabel,
                                    Value = valueText
                                })
                            .AttachExternalCancellation(token);
                    case EvolvedFormTableSO.BuffType.DuplicateRewardBonus:
                        return await _localizationService
                            .GetAsync(TABLE_COLLECTION, "collection_buff_duplicate_reward",
                                new { Value = valueText })
                            .AttachExternalCancellation(token);
                    default:
                        return string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionPopup] 버프 설명 로컬라이즈 실패: {ex.Message}");
                return string.Empty;
            }
        }

        private async UniTask<string> GetEggColorLabelAsync(string eggType, CancellationToken token)
        {
            if (_localizationService == null)
                return GetEggColorLabelFallback(eggType);

            string colorKey = ResolveColorLabelKey(eggType);

            try
            {
                string localized = await _localizationService
                    .GetAsync(TABLE_COLLECTION, colorKey)
                    .AttachExternalCancellation(token);

                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionPopup] 색상 라벨 로컬라이즈 실패: {ex.Message}");
            }

            return GetEggColorLabelFallback(eggType);
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

        private string BuildBuffDescriptionFallback(EvolvedFormTableSO.EvolvedFormRow row)
        {
            if (row == null || row.buffType == EvolvedFormTableSO.BuffType.None || row.buffValuePercent <= 0f)
                return string.Empty;

            string targetEggType = string.IsNullOrWhiteSpace(row.buffTargetEggType) ? row.eggType : row.buffTargetEggType;
            string valueText = $"+{row.buffValuePercent:0.#}%";

            switch (row.buffType)
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

        private void CancelTooltipTask()
        {
            if (_tooltipCts == null)
                return;

            _tooltipCts.Cancel();
            _tooltipCts.Dispose();
            _tooltipCts = null;
        }

        #endregion

        private void OnBackToLobby()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
            uiManager.ShowScene(ESceneUIType.Lobby);
        }

        private void OnOption()
        {
            // 옵션 팝업 출력
            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        private void HideTooltipPanel()
        {
            CancelTooltipTask();
            if (_panelTooltip != null)
                _panelTooltip.SetActive(false);
        }

        private void CloseSelf()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Debug.LogWarning("[UI_CollectionPopup] UIManager를 찾을 수 없어 직접 Hide를 호출합니다.");
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
        }

        private void RebuildContentLayout()
        {
            if (_contentRoot == null)
                return;

            if (!(_contentRoot is RectTransform contentRect))
                contentRect = _contentRoot.GetComponent<RectTransform>();

            if (contentRect == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();
        }
    }
}

