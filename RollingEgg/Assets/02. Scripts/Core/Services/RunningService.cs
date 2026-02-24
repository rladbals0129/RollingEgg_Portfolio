using Cysharp.Threading.Tasks;
using RollingEgg.UI;
using RollingEgg.Data;
using RollingEgg;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RollingEgg.Util;

namespace RollingEgg.Core
{
    public class RunningService : MonoBehaviour, IRunningService
    {
        private GameObject _currentBackgroundInstance;
        private ParallaxBackground _parallaxBackground;
        private PlayerController _currentPlayer;
        private ColorKeyMapping _currentColorKeyMapping;

        private int _stageNumber;
        private int _currentEggId = -1;
        private string _currentEggType = string.Empty;

        private IAudioService _audioService;
        private IMapService _mapService;
        private IStageService _stageService;
        private IPlayerService _playerService;

        public int CurrentEggId => _currentEggId;
        public string CurrentEggType => _currentEggType;
        public int CurrentStageId => _stageNumber;
        public ColorKeyMapping GetCurrentColorKeyMapping() => _currentColorKeyMapping;

        public async UniTask InitializeAsync()
        {
            Debug.Log("[RunningService] 초기화 시작...");

            // 서비스 의존성 주입
            _audioService = ServiceLocator.Get<IAudioService>();
            _mapService = ServiceLocator.Get<IMapService>();
            _stageService = ServiceLocator.Get<IStageService>();
            _playerService = ServiceLocator.Get<IPlayerService>();

            Debug.Log("[RunningService] 초기화 완료");

            await UniTask.Yield();
        }

        public async UniTask OnStartRunning(int eggId, int stageNumber)
        {
            if (eggId <= 0)
            {
                Debug.LogError("[RunningService] 유효하지 않은 eggId 입니다.");
                return;
            }

            // 인스턴스 준비
            var prepareResult = await PrepareRunningInstancesAsync(eggId, stageNumber);
            if (!prepareResult)
            {
                return;
            }

            // 카운트다운 시작
            await StartCountdownAsync();
        }

        /// <summary>
        /// 맵, 플레이어, 배경 등 러닝 게임에 필요한 인스턴스들을 준비
        /// </summary>
        public async UniTask<bool> PrepareRunningInstancesAsync(int eggId, int stageNumber)
        {
            if (_stageService == null)
            {
                Debug.LogError("[RunningService] StageService가 등록되지 않았습니다.");
                return false;
            }

            var stageRow = _stageService.GetStage(stageNumber);
            if (stageRow == null)
            {
                Debug.LogError($"[RunningService] stageId {stageNumber} 데이터를 찾을 수 없습니다.");
                return false;
            }

            if (_mapService == null)
            {
                Debug.LogError("[RunningService] MapService가 등록되지 않았습니다.");
                return false;
            }

            // 맵 프리팹 가져오기
            var mapPrefab = stageRow.mapPrefab;
            if (mapPrefab == null)
            {
                Debug.LogError($"[RunningService] stageId {stageNumber}에 연결된 맵 프리팹이 없습니다.");
                return false;
            }

            // 기존 오브젝트 및 데이터 정리
            Dispose();

            // 데이터 할당
            _currentEggId = eggId;
            _stageNumber = stageNumber;

            // ColorKeyMapping 생성
            _currentColorKeyMapping = CreateColorKeyMapping(stageRow);

            // 맵 인스턴스 생성
            var mapInstance = _mapService.CreateMapInstance(mapPrefab);
            if (mapInstance == null)
            {
                Debug.LogError("[RunningService] 맵 인스턴스를 생성하지 못했습니다.");
                return false;
            }
            mapInstance.Initialize(_currentColorKeyMapping);

            // 플레이어 인스턴스 생성
            var player = InstantiatePlayer();
            if (player == null)
            {
                Debug.LogError("[RunningService] 플레이어 인스턴스를 생성하지 못했습니다.");
                return false;
            }
            _currentPlayer = player;

            // 배경 인스턴스 생성 및 패럴랙스 설정
            CreateBackgroundInstance(stageRow, mapInstance);

            // 카메라 컨트롤러 찾기 및 설정
            var cameraController = Camera.main.GetComponent<CameraController>();
            if (cameraController == null)
            {
                Debug.LogError("[RunningService] CameraController를 찾을 수 없습니다!");
                return false;
            }

            // HUD 활성화 및 키 색깔 리셋
            var runningHUD = UIManager.Instance.ShowScene<UI_RunningHUD>(ESceneUIType.RunningHUD);
            runningHUD.ResetSetting(stageRow, _currentColorKeyMapping);

            // 플레이어 초기화
            player.Initialize(mapInstance.SplinePath, stageRow, _currentColorKeyMapping);
            cameraController.SetupTarget(player.transform);

            Debug.Log("[RunningService] 인스턴스 준비 완료");
            await UniTask.Yield();

            return true;
        }

        /// <summary>
        /// 카운트다운을 시작하고 완료 후 플레이어 자동 달리기를 시작
        /// </summary>
        public async UniTask StartCountdownAsync()
        {
            if (_currentPlayer == null)
            {
                Debug.LogError("[RunningService] 플레이어가 null입니다. 카운트다운을 시작할 수 없습니다.");
                return;
            }


            if (_audioService != null)
            {
                _audioService.StopBGM();
            }

            // 카운트다운 시작
            var countdownUI = UIManager.Instance.ShowPopup<UI_Countdown>(EPopupUIType.Countdown);
            await countdownUI.StartCountdownAsync(callback: () =>
            {
                EBGMKey bgmKey = AudioKeyUtil.GetBGMKeyByChapterId(_currentEggId);
                _audioService.PlayBGM(bgmKey).Forget();

                _currentPlayer.StartAutoRunning();
            });

            Debug.Log("[RunningService] 카운트다운 완료, 게임 시작");
        }

        public async UniTask OnReStartRunning()
        {
            if (_currentEggId <= 0)
            {
                Debug.LogError("[RunningService] 현재 선택된 eggId가 없습니다.");
                return;
            }

            await OnStartRunning(_currentEggId, _stageNumber);
        }

        public async UniTask OnNextStage()
        {
            if (_currentEggId <= 0)
            {
                Debug.LogError("[RunningService] 현재 선택된 eggId가 없습니다.");
                return;
            }

            int nextStageNumber = _stageNumber + 1;
            await OnStartRunning(_currentEggId, nextStageNumber);
        }

        public void PauseRunning()
        {
            Time.timeScale = 0f;
            Debug.Log("[RunningService] 게임 일시정지!");
        }

        public void ResumeRunning()
        {
            Time.timeScale = 1f;
            Debug.Log("[RunningService] 게임 재개!");
        }

        /// <summary>
        /// 특정 플레이어 타입의 스테이지가 최대 스테이지인지 확인
        /// </summary>
        public bool IsMaxStage()
        {
            if (_stageService == null)
                return false;

            if (_currentEggId <= 0)
                return false;

            var stages = _stageService.GetStagesByChapter(_currentEggId);
            if (stages == null || stages.Count == 0)
                return false;

            int maxStage = stages[stages.Count - 1].id;
            return _stageNumber >= maxStage;
        }

        public void Dispose()
        {
            _mapService?.DestroyMapInstance();
            _playerService?.DestroyPlayerInstance();
            _currentPlayer = null;

            // 배경 인스턴스 제거
            if (_currentBackgroundInstance != null)
            {
                Destroy(_currentBackgroundInstance);
                _currentBackgroundInstance = null;
                _parallaxBackground = null;
            }

            _stageNumber = 0;
            _currentEggId = -1;
            _currentEggType = string.Empty;
        }

        public void SetRunningEgg(int eggId, string eggType)
        {
            _currentEggId = eggId;
            _currentEggType = string.IsNullOrEmpty(eggType) ? string.Empty : eggType.ToLowerInvariant();
        }

        private void CreateBackgroundInstance(StageTableSO.StageRow stageRow, Map mapInstance)
        {
            if (stageRow == null || stageRow.background == null)
            {
                Debug.LogWarning("[RunningService] 이 스테이지에 배경 프리팹이 설정되지 않았습니다.");
                return;
            }

            // 배경 인스턴스 생성 (맵의 자식으로 생성)
            _currentBackgroundInstance = Instantiate(stageRow.background, mapInstance.transform);

            // 패럴랙스 설정
            _parallaxBackground = _currentBackgroundInstance.GetComponent<ParallaxBackground>();
            if (_parallaxBackground != null)
            {
                _parallaxBackground.SetupParallax(Camera.main.transform);
            }

            Debug.Log("[RunningService] 배경 생성 완료");
        }

        private PlayerController InstantiatePlayer()
        {
            if (_playerService == null)
            {
                Debug.LogError("[RunningService] PlayerService가 등록되지 않았습니다.");
                return null;
            }

            var controller = _playerService.CreatePlayerInstance();
            _currentPlayer = controller;
            return controller;
        }

        private ColorKeyMapping CreateColorKeyMapping(StageTableSO.StageRow stageRow)
        {
            if (stageRow == null || stageRow.keys == null || stageRow.keys.Count == 0)
            {
                Debug.LogWarning("[RunningService] StageRow 또는 keys가 비어있습니다.");
                return new ColorKeyMapping(new Dictionary<EColorKeyType, EColorType>());
            }

            int chapterId = stageRow.id / 100;
            EColorType mainColor = PathColorUtil.GetMainColorByEggId(chapterId);
            int subColorCount = stageRow.keys.Count - 1;
            List<EColorType> subColors = subColorCount > 0
                ? PathColorUtil.GetRandomSubColorsByEggId(chapterId, subColorCount)
                : new List<EColorType>();

            var mapping = new Dictionary<EColorKeyType, EColorType>();

            for (int i = 0; i < stageRow.keys.Count; i++)
            {
                string keyString = stageRow.keys[i];
                if (string.IsNullOrWhiteSpace(keyString))
                    continue;

                if (System.Enum.TryParse<EColorKeyType>(keyString.Trim().ToUpperInvariant(), out EColorKeyType colorKeyType))
                {
                    EColorType colorToAssign = (i == 0) ? mainColor :
                        (i - 1 < subColors.Count ? subColors[i - 1] : mainColor);
                    mapping[colorKeyType] = colorToAssign;
                }
            }

            Debug.Log($"[RunningService] ColorKeyMapping 생성 완료: {mapping.Count}개 매핑");
            return new ColorKeyMapping(mapping);
        }
    }

    /// <summary>
    /// ColorKeyType과 ColorType의 매칭 데이터를 관리하는 클래스
    /// </summary>
    [System.Serializable]
    public class ColorKeyMapping
    {
        private readonly Dictionary<EColorKeyType, EColorType> _keyToColorMap;

        public ColorKeyMapping(Dictionary<EColorKeyType, EColorType> mapping)
        {
            _keyToColorMap = new Dictionary<EColorKeyType, EColorType>(mapping ?? new Dictionary<EColorKeyType, EColorType>());
        }

        /// <summary>
        /// ColorKeyType에 해당하는 ColorType을 반환
        /// </summary>
        public EColorType GetColor(EColorKeyType keyType)
        {
            return _keyToColorMap.TryGetValue(keyType, out var color) ? color : EColorType.None;
        }

        /// <summary>
        /// ColorType에 해당하는 ColorKeyType 목록을 반환
        /// </summary>
        public List<EColorKeyType> GetKeysByColor(EColorType colorType)
        {
            return _keyToColorMap.Where(kvp => kvp.Value == colorType)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// 모든 ColorKeyType 목록을 반환
        /// </summary>
        public IReadOnlyList<EColorKeyType> GetAllKeys()
        {
            return _keyToColorMap.Keys.ToList();
        }

        /// <summary>
        /// 매핑이 비어있는지 확인
        /// </summary>
        public bool IsEmpty => _keyToColorMap.Count == 0;

        /// <summary>
        /// 매핑 개수
        /// </summary>
        public int Count => _keyToColorMap.Count;
    }
}
