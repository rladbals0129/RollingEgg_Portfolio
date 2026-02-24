using System;
using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    /// <summary>
    /// 로비 화면 UI
    /// - 육성, 플레이, 설정 등 메인 메뉴 제공
    /// </summary>
    public class UI_Lobby : UI_Scene
    {
        [SerializeField] private GameObject _actionPanel;
        [SerializeField] private UI_LobbyCurrencyPanel _currencyPanel;
        [SerializeField] private Button[] _eggButtons;

        [Header("World Settings")]
        [SerializeField] private GameObject _worldPrefab;
        private GameObject _worldInstance;

        [Header("Interaction UI Settings")]
        [SerializeField] private GameObject _interactionUIPanel;
        [SerializeField] private TMPro.TextMeshProUGUI _interactionText;
        [SerializeField] private Vector3 _uiOffset = new Vector3(0, 1.5f, 0); // 오브젝트 위 오프셋

        private Camera _mainCamera;

        private GameObject _selectedEgg;
        private bool _initialized;

        private IEventBus _eventBus;
        private IAudioService _audioService;
        private IGrowthActionService _growthActionService;
        private IRunningService _runningService;
        private IStageService _stageService;
        private bool _isSceneChanging;
        private bool _isStageOpening;

        public override async UniTask InitializeAsync()
        {
            _eventBus = ServiceLocator.Get<IEventBus>();
            _audioService = ServiceLocator.Get<IAudioService>();
            _growthActionService = ServiceLocator.Get<IGrowthActionService>();
            _runningService = ServiceLocator.Get<IRunningService>();
            if (ServiceLocator.HasService<IStageService>())
                _stageService = ServiceLocator.Get<IStageService>();

            // 메인 카메라 캐싱
            _mainCamera = Camera.main;

            // 알 버튼 초기화
            EnsureInitialized();
            if (_actionPanel != null) _actionPanel.SetActive(false);

            // InteractionUI 초기 숨김
            if (_interactionUIPanel != null)
            {
                _interactionUIPanel.SetActive(false);
            }

            if (_currencyPanel == null)
            {
                Debug.LogError("[UI_Lobby] CurrencyPanel이 할당되지 않았습니다. 인스펙터에서 설정하세요.");
            }
            else
            {
                _currencyPanel.Initialize();
            }
            await UniTask.Yield();
        }

        private void OnClickEgg(GameObject egg)
        {
            EnsureInitialized();
            
            // 같은 알을 다시 클릭하면 선택 해제
            if (_selectedEgg == egg)
            {
                _selectedEgg = null;
                if (_actionPanel != null) _actionPanel.SetActive(false);
                return;
            }

            _selectedEgg = egg;

            // 버튼 배열 인덱스로 eggId 결정 (1-based, EggTableSO의 id와 매칭)
            int eggId = GetEggIdFromGameObject(egg);

            // GrowthActionService를 통해 EggTableSO에서 eggType 가져오기
            string eggType = string.Empty;
            if (_growthActionService != null)
            {
                eggType = _growthActionService.GetEggType(eggId);
            }

            if (_actionPanel != null) _actionPanel.SetActive(true);
            Debug.Log($"{_selectedEgg.name}이 선택되었습니다. (ID: {eggId}, Type: {eggType})");
        }

        /// <summary>
        /// GameObject에서 알 ID 추출 (버튼 배열 인덱스 기반)
        /// </summary>
        private int GetEggIdFromGameObject(GameObject egg)
        {
            for (int i = 0; i < _eggButtons.Length; i++)
            {
                if (ReferenceEquals(_eggButtons[i].gameObject, egg))
                    return i + 1; // 1~5 (EggTableSO의 id와 매칭)
            }
            return 1; // 기본값
        }

        /// <summary>
        /// 선택 상태를 초기화한다. 액션 패널을 닫는다.
        /// </summary>
        public void OnClickCloseSelection()
        {
            _selectedEgg = null;
            if (_actionPanel != null) _actionPanel.SetActive(false);
        }

        public override void OnShow()
        {
            if (_audioService != null)
            {
                _audioService.PlayBGM(EBGMKey.BGM_Lobby);
            }

            // 씬이 다시 표시될 때도 초기 상태로 복귀
            EnsureInitialized();
            _selectedEgg = null;
            if (_actionPanel != null) _actionPanel.SetActive(false);

            // 월드 프리팹 소환
            if (_worldInstance == null && _worldPrefab != null)
            {
                _worldInstance = Instantiate(_worldPrefab);
            }
            else if (_worldInstance != null)
            {
                _worldInstance.SetActive(true);
            }
        }

        public override void OnHide()
        {
            // InteractionUI 숨김
            HideInteractionUI();

            // UI 꺼질 때 월드도 같이 삭제 (메모리 해제 & 상태 초기화)
            if (_worldInstance != null)
            {
                Destroy(_worldInstance);
                _worldInstance = null;
            }

            base.OnHide();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            if (_eggButtons == null)
                _eggButtons = new Button[0];

            for (int i = 0; i < _eggButtons.Length; i++)
            {
                var btn = _eggButtons[i];
                if (btn == null)
                    continue;

                var captured = btn;
                captured.onClick.AddListener(() => OnClickEgg(captured.gameObject));
            }

            _initialized = true;
        }

        public void OnClickRunningStart()
        {
            if (_selectedEgg == null)
            {
                Debug.LogWarning("[UI_Lobby] 육성할 알을 먼저 선택해주세요.");
                return;
            }

            int eggId = GetEggIdFromGameObject(_selectedEgg);
            HandleStageInteraction(eggId);
        }

        /// <summary>
        /// 육성 화면으로 이동
        /// </summary>
        public void OnClickNurture()
        {
            if (_selectedEgg == null)
            {
                Debug.LogWarning("[UI_Lobby] 육성할 알을 먼저 선택해주세요.");
                return;
            }

            int eggId = GetEggIdFromGameObject(_selectedEgg);
            HandleNurtureInteraction(eggId);
        }

        /// <summary>
        /// 상호작용으로 스테이지 선택을 실행한다.
        /// </summary>
        /// <param name="eggId">대상 알 ID</param>
        /// <param name="eggTypeOverride">알 타입 강제 지정(선택)</param>
        public void HandleStageInteraction(int eggId, string eggTypeOverride = null)
        {
            if (eggId <= 0)
            {
                Debug.LogWarning("[UI_Lobby] 유효하지 않은 알 ID입니다.");
                return;
            }

            string eggType = !string.IsNullOrEmpty(eggTypeOverride)
                ? eggTypeOverride
                : _growthActionService?.GetEggType(eggId) ?? "blue";

            OpenStageWithFadeAsync(eggId, eggType).Forget();
        }

        /// <summary>
        /// 상호작용으로 육성 화면 이동을 실행한다.
        /// </summary>
        /// <param name="eggId">대상 알 ID</param>
        /// <param name="eggTypeOverride">알 타입 강제 지정(선택)</param>
        public void HandleNurtureInteraction(int eggId, string eggTypeOverride = null)
        {
            if (eggId <= 0)
            {
                Debug.LogWarning("[UI_Lobby] 육성에 사용할 알 ID가 유효하지 않습니다.");
                return;
            }

            string eggType = !string.IsNullOrEmpty(eggTypeOverride)
                ? eggTypeOverride
                : _growthActionService?.GetEggType(eggId) ?? "blue";

            if (_growthActionService != null && !_growthActionService.HasEgg(eggId))
            {
                _growthActionService.CreateEgg(eggId);
            }

            var nurtureUI = UIManager.Instance.GetCachedScene<UI_Nurture>(ESceneUIType.Nurture);
            nurtureUI?.SetSelectedEgg(eggId, eggType);

            ChangeSceneWithFade(ESceneUIType.Nurture).Forget();
        }

        /// <summary>
        /// 상호작용으로 도감 팝업을 연다.
        /// </summary>
        public void HandleCollectionInteraction()
        {
            var popup = UIManager.Instance.ShowPopup(EPopupUIType.Collection);
            if (popup == null)
            {
                Debug.LogWarning("[UI_Lobby] 도감 팝업을 열 수 없습니다.");
            }
        }

        /// <summary>
        /// 플레이 화면으로 이동
        /// </summary>
        public void OnClickPlay()
        {
            ChangeSceneWithFade(ESceneUIType.Play).Forget();
        }

        /// <summary>
        /// 설정 팝업 열기
        /// </summary>
        public void OnClickSetting()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        /// <summary>
        /// 타이틀 화면으로 돌아가기
        /// </summary>
        public void OnClickBackToTitle()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            ChangeSceneWithFade(ESceneUIType.Title).Forget();
        }

        /// <summary>
        /// 게임 종료
        /// </summary>
        public void OnClickExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        private async UniTaskVoid ChangeSceneWithFade(ESceneUIType sceneType)
        {
            if (_isSceneChanging)
                return;

            _isSceneChanging = true;
            try
            {
                await UIManager.Instance.ShowSceneWithFadeAsync(sceneType);
            }
            finally
            {
                _isSceneChanging = false;
            }
        }

        private async UniTaskVoid OpenStageWithFadeAsync(int eggId, string eggType)
        {
            if (_isStageOpening)
                return;

            _isStageOpening = true;
            try
            {
                int chapterId = eggId; // EggTableSO의 id와 StageTableSO.chapterId가 연동됨

                var uiStage = await UIManager.Instance.ShowPopupWithFadeAsync<UI_Stage>(
                    EPopupUIType.Stage,
                    remember: true,
                    setupBeforeFadeIn: async stage =>
                    {
                        stage.OpenChapter(chapterId, eggId, eggType);
                        await UniTask.Yield(); // 레이아웃 1프레임 보장
                    });

                if (uiStage == null)
                {
                    Debug.LogWarning("[UI_Lobby] Stage UI를 열 수 없습니다.");
                    return;
                }

                _stageService?.SetLastSelection(chapterId, _stageService.GetLastSelectedStage());
            }
            finally
            {
                _isStageOpening = false;
            }
        }

        #region Interaction UI
        /// <summary>
        /// 상호작용 UI 표시 (월드 좌표를 Canvas 로컬 좌표로 변환)
        /// </summary>
        public void ShowInteractionUI(Vector3 worldPosition)
        {
            if (_interactionUIPanel == null || _mainCamera == null)
                return;

            _interactionUIPanel.SetActive(true);

            // 월드 좌표를 스크린 좌표로 변환
            Vector3 screenPosition = _mainCamera.WorldToScreenPoint(worldPosition + _uiOffset);

            // Canvas의 RectTransform 가져오기
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[UI_Lobby] Canvas를 찾을 수 없습니다.");
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform panelRect = _interactionUIPanel.GetComponent<RectTransform>();

            if (canvasRect != null && panelRect != null)
            {
                // 스크린 좌표를 Canvas의 로컬 좌표로 변환
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _mainCamera,
                    out localPoint
                );

                // Panel의 anchoredPosition 설정
                panelRect.anchoredPosition = localPoint;
            }
        }

        /// <summary>
        /// 상호작용 UI 숨김
        /// </summary>
        public void HideInteractionUI()
        {
            if (_interactionUIPanel != null)
            {
                _interactionUIPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 상호작용 텍스트 변경 (필요 시 사용)
        /// </summary>
        public void SetInteractionText(string text)
        {
            if (_interactionText != null)
            {
                _interactionText.text = text;
            }
        }
        #endregion

    }
}
