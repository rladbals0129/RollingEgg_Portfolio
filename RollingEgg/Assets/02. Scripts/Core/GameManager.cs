using RollingEgg.UI;
using RollingEgg.Core;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace RollingEgg.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private void Start()
        {
            InitializeGameAsync().Forget();
        }

        private async UniTaskVoid InitializeGameAsync()
        {
            try
            {
                Debug.Log("[GameManager] 게임 초기화 시작...");
                
                await ServiceRegisterAsync();
                await UIManager.Instance.InitializeAsync();

                // UIManager 초기화가 완료된 후 Title 씬 표시
                if (UIManager.Instance.IsInitialized)
                {
                    Debug.Log("[GameManager] 타이틀 UI 표시 중...");
                    UIManager.Instance.ShowScene(ESceneUIType.Title);
                    Debug.Log("[GameManager] 타이틀 UI 표시 완료");
                }
                else
                {
                    Debug.LogError("UIManager 초기화가 완료되지 않았습니다.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameManager] 게임 초기화 중 오류 발생: {ex.Message}");
                Debug.LogError($"[GameManager] 스택 트레이스: {ex.StackTrace}");
            }
        }

        private async UniTask ServiceRegisterAsync()
        {
            Debug.Log("[GameManager] 서비스 등록 시작...");

            // 1. 기본 서비스들 등록
            var resourceManager = new ResourceManager();
            resourceManager.Initialize();
            ServiceLocator.Register<IResourceService, ResourceManager>(resourceManager);

			var eventBus = new EventBus();
            eventBus.Initialize();
            ServiceLocator.Register<IEventBus, EventBus>(eventBus);

            // 설정 서비스 등록 및 로드 (JSON)
            var settingsService = new SettingsService();
			await settingsService.InitializeAsync();
			ServiceLocator.Register<ISettingsService, SettingsService>(settingsService);

            // 오디오 서비스
            var audioService = gameObject.AddComponent<AudioManager>();
            await audioService.InitializeAsync();
            ServiceLocator.Register<IAudioService, AudioManager>(audioService);

            // Localization 서비스 등록 (UI에서 공통 사용 - 다국어 대응)
            var localizationService = new UnityLocalizationService();
			ServiceLocator.Register<ILocalizationService, UnityLocalizationService>(localizationService);
			// 설정에 저장된 로캘 적용
			if (!string.IsNullOrEmpty(settingsService.LocaleCode))
				localizationService.SetLocale(settingsService.LocaleCode);

            // 2. 육성 시스템 서비스들 등록
            //var experienceService = new ExperienceService();
            //await experienceService.InitializeAsync();
            //ServiceLocator.Register<IExperienceService, ExperienceService>(experienceService);

            var currencyService = new CurrencyService();
            await currencyService.InitializeAsync();
            ServiceLocator.Register<ICurrencyService, CurrencyService>(currencyService);

            var growthActionService = new GrowthActionService();
            await growthActionService.InitializeAsync();
            ServiceLocator.Register<IGrowthActionService, GrowthActionService>(growthActionService);

            var stageService = new StageService();
            await stageService.InitializeAsync();
            ServiceLocator.Register<IStageService, StageService>(stageService);

            // 도감 서비스 등록 
            var collectionService = new CollectionService();
            await collectionService.InitializeAsync();
            ServiceLocator.Register<ICollectionService, CollectionService>(collectionService);

            var evolutionService = new EvolutionService();
            await evolutionService.InitializeAsync();
            ServiceLocator.Register<IEvolutionService, EvolutionService>(evolutionService);

            // 3. 러닝 게임 서비스 등록
            var playerService = new PlayerService();
            await playerService.InitializeAsync();
            ServiceLocator.Register<IPlayerService, PlayerService>(playerService);

            var mapService = new MapService();
            await mapService.InitializeAsync();
            ServiceLocator.Register<IMapService, MapService>(mapService);

            var runningService = new RunningService();
            await runningService.InitializeAsync();
            ServiceLocator.Register<IRunningService, RunningService>(runningService);

            Debug.Log("[GameManager] 서비스 등록 완료");

            // 3. 러닝 브릿지 구성요소 보장
            if (GetComponent<RunningBridge>() == null)
            {
                gameObject.AddComponent<RunningBridge>();
            }

            if (GetComponent<CurrencyDebugInput>() == null)
            {
                gameObject.AddComponent<CurrencyDebugInput>();
            }

 
        }

        private async void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                await SaveAllAsync();
            }
        }

        private async void OnApplicationQuit()
        {
            await SaveAllAsync();
        }

        private async UniTask SaveAllAsync()
        {
            // 등록 여부를 검사하고 저장을 호출
			if (ServiceLocator.HasService<ISettingsService>())
				await ServiceLocator.Get<ISettingsService>().SaveAsync();
            //if (ServiceLocator.HasService<IExperienceService>())
            //    await ServiceLocator.Get<IExperienceService>().SaveDataAsync();
            if (ServiceLocator.HasService<ICurrencyService>())
                await ServiceLocator.Get<ICurrencyService>().SaveDataAsync();
            if (ServiceLocator.HasService<IGrowthActionService>())
                await ServiceLocator.Get<IGrowthActionService>().SaveDataAsync();
            if (ServiceLocator.HasService<ICollectionService>())
                await ServiceLocator.Get<ICollectionService>().SaveDataAsync();
            if (ServiceLocator.HasService<IEvolutionService>())
                await ServiceLocator.Get<IEvolutionService>().SaveDataAsync();
            if (ServiceLocator.HasService<IStageService>())
                await ServiceLocator.Get<IStageService>().SaveDataAsync();
        }
    }
}


/*

GameManager 구현
▶구현 내용:
ㆍ서비스 등록: 기본 서비스와 육성 시스템 서비스 등록
ㆍUI 초기화: UIManager 초기화 및 타이틀 화면 표시
▶핵심 기능:
ㆍ서비스 의존성 관리: ServiceLocator를 통한 서비스 의존성 관리
ㆍUI 시스템 통합: UIManager를 통한 화면 전환 및 상호작용 관리
ㆍ알 육성 환경 제공: 육성 관련 서비스들을 통해 알 육성 환경 제공


*/