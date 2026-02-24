using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using RollingEgg.Core;
using RollingEgg.Data;
using RollingEgg.Util;
using RollingEgg.UI;
using Cysharp.Threading.Tasks;
using System.Linq;

namespace RollingEgg
{
    public enum EPlayerState
    {
        Idle, Running, Dead
    }

    public enum EColorType
    {
        None,

        // 오방색 (5가지)
        Yellow,     // 노랑 
        Blue,       // 파랑
        Red,        // 빨강
        White,      // 흰색
        Black,      // 검정색

        // 추가 8가지 색상
        Orange,     // 주황색
        Green,      // 초록색
        Purple,     // 보라색
        Pink,       // 분홍색
        Brown,      // 갈색
        Cyan,       // 청록색
        Magenta,    // 자홍색
        Lime,       // 라임색
    }


    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _baseSpeed = 5f;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _offsetY = 0.5f;
        private float _speedMultiplier = 1f;

        [Header("Player Settings")]
        [SerializeField] private EPlayerState _playerState = EPlayerState.Idle;
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;

        [Header("Health")]
        private float _maxHealth;
        private float _currentHealth;
        private float _reduceHealth;

        // ColorKey
        private ColorKeyMapping _colorKeyMapping;
        private HashSet<EColorKeyType> _allowedKeyTypes = new HashSet<EColorKeyType>(); // StageRow의 keys만 허용
        private HashSet<KeyCode> _allowedKeyCodes = new HashSet<KeyCode>(); // 허용된 KeyCode들

        // SplinePath
        private SplinePath _splinePath;
        private float _splineProgress = 0f;

        // SafeZone
        private HashSet<EColorKeyType> _accumulatedPressedKeys = new HashSet<EColorKeyType>(); // 누적된 키 입력
        private Dictionary<EJudgmentType, int> _judgmentCounts = new();
        private Dictionary<EJudgmentType, int> _judgmentScores = new();
        private SafeZone _currentSafeZone;
        private int _currentComboCount = 0;
        private int _maxComboCount = 0;
        private int _totalColorChangeCount = 0;

        private bool _isInSafezone = false;
        private bool _wasAllKeysPressedLastFrame = false;

        private UI_RunningHUD _runningHUD;
        private StageTableSO.StageRow _stageConfig;

        private IAudioService _audioService;
        private ISettingsService _settingService;
        private IEventBus _eventBus;
        private IRunningService _runningService;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _runningHUD.OnClickSetting();
            }

            if (_playerState == EPlayerState.Running)
            {
                HandleInput();
                MoveAlongPath();
                DecreaseHealthOverTime();
            }
        }

        public void Initialize(SplinePath splinePath, StageTableSO.StageRow stageConfig, ColorKeyMapping colorKeyMapping)
        {
            _audioService = ServiceLocator.Get<IAudioService>();
            _settingService = ServiceLocator.Get<ISettingsService>();
            _eventBus = ServiceLocator.Get<IEventBus>();
            _runningService = ServiceLocator.Get<IRunningService>();

            _stageConfig = stageConfig;
            _colorKeyMapping = colorKeyMapping;

            if (_stageConfig != null)
            {
                _speedMultiplier = _stageConfig.speed > 0f ? _stageConfig.speed : 1f;
                _maxHealth = _stageConfig.hp > 0f ? _stageConfig.hp : 100f;
                _reduceHealth = _stageConfig.reduceHP > 0f ? _stageConfig.reduceHP : 5f;
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _runningHUD = UIManager.Instance.GetCurrentScene<UI_RunningHUD>();

            _splinePath = splinePath;

            SetupAllowedKeys(colorKeyMapping);
            ResetSetting(stageConfig);
        }

        public void ResetSetting(StageTableSO.StageRow stageConfig)
        {
            // 플레이어 초기화
            _playerState = EPlayerState.Idle;
            _moveSpeed = _baseSpeed * _speedMultiplier;

            // SplinePath 초기화 및 시작 위치 설정
            if (_splinePath != null && _splinePath.SplineContainer != null)
            {
                _splineProgress = 0f;

                Vector3 worldStartPos = _splinePath.GetPointAt(_splineProgress);
                worldStartPos.y += _offsetY; // offset 적용
                transform.position = worldStartPos;
            }

            // 진행도 초기화
            _runningHUD.UpdateProgressGauge(0f);

            // 체력 초기화 및 업데이트
            _currentHealth = _maxHealth;
            _runningHUD.UpdateHealthGauge(_currentHealth, _maxHealth);

            // 콤보 UI 초기화
            _runningHUD.UpdateComboText(0);

            // 플레이어 애니메이션 업데이트
            UpdateAnimation();

            // Judgment 카운터 및 스코어 초기화
            _judgmentCounts.Clear();
            _judgmentCounts[EJudgmentType.MISS] = 0;
            _judgmentCounts[EJudgmentType.BAD] = 0;
            _judgmentCounts[EJudgmentType.GOOD] = 0;
            _judgmentCounts[EJudgmentType.GREAT] = 0;
            _judgmentCounts[EJudgmentType.PERFECT] = 0;

            _judgmentScores.Clear();
            _judgmentScores[EJudgmentType.MISS] = 0;
            _judgmentScores[EJudgmentType.BAD] = 0;
            _judgmentScores[EJudgmentType.GOOD] = 0;
            _judgmentScores[EJudgmentType.GREAT] = 0;
            _judgmentScores[EJudgmentType.PERFECT] = 0;

            _currentComboCount = 0;
            _maxComboCount = 0;
            _totalColorChangeCount = 0;

            _isInSafezone = false;
        }

        /// <summary>
        /// ColorKeyMapping을 기반으로 허용된 키 설정
        /// </summary>
        private void SetupAllowedKeys(ColorKeyMapping colorKeyMapping)
        {
            _allowedKeyTypes.Clear();
            _allowedKeyCodes.Clear();

            if (colorKeyMapping == null || colorKeyMapping.IsEmpty)
            {
                Debug.LogWarning("[PlayerController] ColorKeyMapping이 null이거나 비어있습니다.");
                return;
            }

            var allKeys = colorKeyMapping.GetAllKeys();
            foreach (EColorKeyType colorKeyType in allKeys)
            {
                _allowedKeyTypes.Add(colorKeyType);

                // EColorKeyType을 KeyCode로 변환
                KeyCode keyCode = _settingService.GetColorKey(colorKeyType);
                if (keyCode != KeyCode.None)
                {
                    _allowedKeyCodes.Add(keyCode);
                }
            }

            Debug.Log($"[PlayerController] 허용된 키 설정 완료: {_allowedKeyTypes.Count}개");
        }

        public void StartAutoRunning()
        {
            _playerState = EPlayerState.Running;
            UpdateAnimation();
            Debug.Log("[PlayerController] 자동 달리기 시작!");
        }

        public void StopAutoRunning()
        {
            _playerState = EPlayerState.Idle;
            UpdateAnimation();
            Debug.Log("[PlayerController] 자동 달리기 중지!");
        }

        public void InSafeZone(SafeZone safeZone)
        {
            _isInSafezone = true;
            _currentSafeZone = safeZone;
            _wasAllKeysPressedLastFrame = false;
            _accumulatedPressedKeys.Clear();
        }

        public void OutSafeZone()
        {
            _isInSafezone = false;
            _currentSafeZone = null;
            _accumulatedPressedKeys.Clear();
        }

        public void OnEndPointReached()
        {
            _playerState = EPlayerState.Idle;
            _maxComboCount = Mathf.Max(_maxComboCount, _currentComboCount);

            UpdateAnimation();

            var snapshot = CreateScoreSnapshot();
            PublishRunningCompleted(snapshot, true);
        }

        private void CheckSimultaneousKeyInput()
        {
            if (_currentSafeZone == null)
                return;

            // SafeZone의 ColorKeyTypes 가져오기
            var requiredKeyTypes = _currentSafeZone.ColorKeyTypes;
            if (requiredKeyTypes == null || requiredKeyTypes.Count == 0)
                return;

            // 현재 프레임에 새로 눌린 키들 체크 (SafeZone 진입 시점에 눌려있던 키 제외)
            foreach (EColorKeyType keyType in _allowedKeyTypes)
            {
                KeyCode keyCode = _settingService.GetColorKey(keyType);
                if (keyCode != KeyCode.None && Input.GetKeyDown(keyCode))
                {
                    _accumulatedPressedKeys.Add(keyType);
                }
            }

            // 이미 판정을 했으면 다시 하지 않음
            if (_wasAllKeysPressedLastFrame)
                return;

            if (_accumulatedPressedKeys.Count < requiredKeyTypes.Count)
                return;

            // 누적된 키의 개수와 종류가 필요한 키와 동일한지 확인
            bool keysMatch = _accumulatedPressedKeys.Count == requiredKeyTypes.Count;

            if (keysMatch)
            {
                // 누적된 키가 필요한 키와 정확히 일치하는지 확인
                foreach (EColorKeyType requiredKeyType in requiredKeyTypes)
                {
                    if (!_accumulatedPressedKeys.Contains(requiredKeyType))
                    {
                        keysMatch = false;
                        break;
                    }
                }
            }

            // 판정 처리
            if (keysMatch)
            {
                // 정확히 일치 - 정상 판정
                ProcessSafeZoneInput(new List<EColorKeyType>(requiredKeyTypes));
            }
            else
            {
                // 불일치 - MISS 판정
                ProcessSafeZoneInput(new List<EColorKeyType>(_accumulatedPressedKeys), EJudgmentType.MISS);
            }

            // 누적된 키 클리어 및 다음 윈도우 시작
            _accumulatedPressedKeys.Clear();
            _wasAllKeysPressedLastFrame = true;
        }

        /// <summary>
        /// SafeZone 입력 처리 (모든 키가 동시에 눌렸을 때)
        /// </summary>
        private void ProcessSafeZoneInput(List<EColorKeyType> pressedKeyTypes, EJudgmentType? forcedJudgment = null)
        {
            if (_currentSafeZone == null)
                return;

            // SafeZone 내부에서 판정 처리
            EJudgmentType judgment = forcedJudgment ?? _currentSafeZone.GetJudgmentType(transform.position);

            // 판정 처리
            ProcessJudgment(judgment);

            // 쿨타임 적용 (각 키에 대해)
            foreach (EColorKeyType keyType in pressedKeyTypes)
            {
                var colorKeyUI = _runningHUD.GetColorKeyUI(keyType);
                if (colorKeyUI != null && _stageConfig != null)
                {
                    float cooldown = _stageConfig.changeColorCooltime;
                    if (cooldown > 0f)
                    {
                        colorKeyUI.StartCooldown(cooldown).Forget();
                    }
                }
            }

            // SafeZone 삭제
            _currentSafeZone.SafeZoneDestory();
            OutSafeZone();

            Debug.Log($"[PlayerController] SafeZone 처리 완료: {pressedKeyTypes.Count}개 키 동시 입력, 판정: {judgment}");
        }

        private RunningScoreSnapshot CreateScoreSnapshot()
        {
            var counts = new Dictionary<EJudgmentType, int>(_judgmentCounts);
            var scores = new Dictionary<EJudgmentType, int>(_judgmentScores);

            int baseScore = 0;
            foreach (var score in _judgmentScores.Values)
            {
                baseScore += score;
            }

            int hpScore = ScoreUtil.CalculateHPScore(_currentHealth, _maxHealth);
            int comboBonus = _maxComboCount;
            int totalScore = Mathf.Max(0, baseScore + hpScore + comboBonus);
            var rank = ScoreUtil.GetRankByScore(totalScore);

            return new RunningScoreSnapshot
            {
                judgmentCounts = counts,
                judgmentScores = scores,
                currentHP = _currentHealth,
                maxHP = _maxHealth,
                baseScore = baseScore,
                hpScore = hpScore,
                comboBonus = comboBonus,
                maxComboCount = _maxComboCount,
                totalColorChangeCount = _totalColorChangeCount,
                totalScore = totalScore,
                clearRank = rank
            };
        }

        private void PublishRunningCompleted(RunningScoreSnapshot snapshot, bool isCleared)
        {
            if (_eventBus == null)
                return;

            int eggId = _runningService != null ? _runningService.CurrentEggId : -1;
            string eggType = _runningService != null ? _runningService.CurrentEggType : string.Empty;
            int stageId = _runningService != null ? _runningService.CurrentStageId : -1;
            var evt = new RunningGameCompletedEvent
            {
                eggId = eggId,
                eggType = eggType,
                stageId = stageId,
                isCleared = isCleared,
                score = snapshot,
                distance = 0f,
                playTime = 0f,
                clearTime = 0f,
                colorDistances = Array.Empty<int>()
            };

            _eventBus.Publish(evt);
        }

        private void HandleInput()
        {
            // SafeZone이 없으면 입력 무시
            if (!_isInSafezone || _currentSafeZone == null)
                return;

            // SafeZone의 KeyTypes 개수만큼 동시 입력 체크
            CheckSimultaneousKeyInput();
        }

        private void MoveAlongPath()
        {
            if (_splinePath == null || _splinePath.SplineContainer == null)
                return;

            var spline = _splinePath.SplineContainer.Spline;
            if (spline.Count < 2)
                return;

            // 스플라인 길이 계산
            float splineLength = spline.GetLength();
            if (splineLength <= 0f)
                return;

            // 진행도 업데이트
            float progressDelta = (_moveSpeed * Time.deltaTime) / splineLength;
            _splineProgress = Mathf.Clamp01(_splineProgress + progressDelta);

            // 현재 위치 업데이트
            Vector3 newWorldPosition = _splinePath.GetPointAt(_splineProgress);
            newWorldPosition.y += _offsetY;
            transform.position = newWorldPosition;

            // ProgressGaugeBar 업데이트
            if (_runningHUD != null)
            {
                _runningHUD.UpdateProgressGauge(_splineProgress);
            }
        }

        /// <summary>
        /// reduceHP 값에 따라 체력을 지속적으로 감소시킴
        /// </summary>
        private void DecreaseHealthOverTime()
        {
            if (_reduceHealth <= 0f)
                return;

            // 초당 _healthDecreaseRate만큼 체력 감소
            float healthChange = -_reduceHealth * Time.deltaTime;
            ChangeHealth(healthChange);
        }

        private void UpdateAnimation()
        {
            if (_animator == null)
                return;

            _animator.SetBool("IsRunning", _playerState == EPlayerState.Running);
        }

        private void UpdateSpeed()
        {
            // 캐릭터 타입별 속도 적용
            float baseSpeed = _baseSpeed * _speedMultiplier;
            _moveSpeed = ComboUtil.CalculateSpeedWithCombo(baseSpeed, _currentComboCount);

            if (_animator == null)
                return;

            if (_playerState == EPlayerState.Running)
            {
                // 기본 속도 대비 비율로 애니메이션 속도 설정
                float animationSpeed = _moveSpeed / _baseSpeed;

                //// 최소/최대 제한 (선택사항)
                //animationSpeed = Mathf.Clamp(animationSpeed, 0.5f, 2f);

                _animator.speed = animationSpeed;
            }
            else
            {
                _animator.speed = 1f;
            }
        }

        private void ApplyJudgmentHealthEffect(EJudgmentType judgment)
        {
            if (_stageConfig == null)
                return;

            float healthChange = judgment switch
            {
                EJudgmentType.PERFECT => _stageConfig.perfectHP,
                EJudgmentType.GREAT => _stageConfig.greatHP,
                EJudgmentType.GOOD => _stageConfig.goodHP,
                EJudgmentType.BAD => _stageConfig.badHP,
                EJudgmentType.MISS => _stageConfig.missHP,
                _ => 0f
            };

            if (healthChange != 0f)
            {
                ChangeHealth(healthChange);
            }
        }

        private void ChangeHealth(float healthChange)
        {
            _currentHealth += healthChange;
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);

            // Health Bar 업데이트
            if (_runningHUD != null)
            {
                _runningHUD.UpdateHealthGauge(_currentHealth, _maxHealth);
            }

            // 체력이 0 이하가 되면 사망 처리
            if (_currentHealth <= 0f)
            {
                Debug.Log("[PlayerController] 체력 모두 소진");
                _playerState = EPlayerState.Dead;
                UpdateAnimation();
                _maxComboCount = Mathf.Max(_maxComboCount, _currentComboCount);
                var snapshot = CreateScoreSnapshot();
                PublishRunningCompleted(snapshot, false);
                UIManager.Instance.ShowPopup(EPopupUIType.RunningPopup);
            }
        }

        /// <summary>
        /// Judgment를 처리 (ComboCount, 카운터 증가, UI 표시 등)
        /// </summary>
        private void ProcessJudgment(EJudgmentType judgment)
        {
            // ComboCount 처리
            if (judgment >= EJudgmentType.GOOD)
            {
                _currentComboCount++;
            }
            else
            {
                _maxComboCount = Mathf.Max(_maxComboCount, _currentComboCount);
                _currentComboCount = 0;
            }

            // 콤보 UI 업데이트
            if (_runningHUD != null)
            {
                _runningHUD.UpdateComboText(_currentComboCount);
            }

            // Judgment 카운터 증가
            _judgmentCounts[judgment]++;
            _totalColorChangeCount++;

            // 콤보 배율 적용 점수 계산 (PERFECT/GREAT에만 적용)
            int baseScore = ScoreUtil.GetBaseScore(judgment);
            int finalScore = baseScore;
            if (judgment == EJudgmentType.PERFECT || judgment == EJudgmentType.GREAT)
            {
                finalScore = ComboUtil.CalculateScoreWithCombo(baseScore, _currentComboCount);
            }

            // 점수 저장
            if (!_judgmentScores.ContainsKey(judgment))
            {
                _judgmentScores[judgment] = 0;
            }
            _judgmentScores[judgment] += finalScore;

            // 플레이어 속도 업데이트
            UpdateSpeed();

            // 판정별 체력 변화 적용 (StageRow의 설정값 사용)
            ApplyJudgmentHealthEffect(judgment);

            // Judgment SFX 재생
            if (_audioService != null)
            {
                ESFXKey sfxKey = AudioKeyUtil.GetSFXKeyByJudgment(judgment);
                if (sfxKey != ESFXKey.None)
                {
                    _audioService.PlaySFX(sfxKey).Forget();
                }
            }

            // UI에 판정 표시
            _runningHUD.ShowJudgment(judgment);
        }

        /// <summary>
        /// 선택한 ColorKey가 쿨타임 인지 확인
        /// </summary>
        private bool IsColorKeyCooldown(EPathType pathType)
        {
            var colorKeyUI = _runningHUD.GetColorKeyUI(EColorKeyType.None);
            return colorKeyUI.IsCooldown;
        }
    }
}