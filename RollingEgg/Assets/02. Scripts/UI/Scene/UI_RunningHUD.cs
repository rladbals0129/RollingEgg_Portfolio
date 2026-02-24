using Cysharp.Threading.Tasks;
using DG.Tweening;
using RollingEgg.Core;
using RollingEgg.Data;
using RollingEgg.UI;
using RollingEgg.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class UI_RunningHUD : UI_Scene
    {
        [Header("## ColorKey Panel")]
        [SerializeField] private List<ColorKeyUI> _colorKeyUIs = new();

        [Header("## Health Bar")]
        [SerializeField] private Image _healthFillImage;

        [Header("## Progress Bar")]
        [SerializeField] private Image _progressFillImage;

        [Header("## Combo Text")]
        [SerializeField] private TMP_Text _comboText;

        [Header("## Judgment Text")]
        [SerializeField] private TMP_Text _judgmentText;
        [SerializeField] private RectTransform _judgmentTextRect;
        [SerializeField] private float _judgmentAnimationDuration = 0.8f;   // 애니메이션 지속 시간
        [SerializeField] private float _judgmentMoveDistance = 100f;        // 위로 이동할 거리
        [SerializeField] private Ease _judgmentMoveEase;                    // 애니메이션 Ease
        private Vector2 _judgmentStartPosition;                             // 시작 위치 (아래쪽)

        private Dictionary<EColorKeyType, ColorKeyUI> _colorKeyDicts = new();

        private Tween _currentJudgmentTween;

        private IAudioService _audioService;
        private IRunningService _runningService;

        public async override UniTask InitializeAsync()
        {
            _audioService = ServiceLocator.Get<IAudioService>();
            _runningService = ServiceLocator.Get<IRunningService>();

            _colorKeyDicts.Clear();
            foreach (var colorKeyUI in _colorKeyUIs)
            {
                if (colorKeyUI == null)
                {
                    Debug.LogWarning("[UI_RunningHUD] _colorKeyUIs 리스트에 null이 포함되어 있습니다.");
                    continue;
                }

                // ColorKeyUI 초기화
                colorKeyUI.Initialize();

                // Dictionary에 추가 (EColorKeyType을 키로 사용)
                EColorKeyType keyType = colorKeyUI.ColorKeyType;

                // 중복 체크
                if (_colorKeyDicts.ContainsKey(keyType))
                {
                    Debug.LogWarning($"[UI_RunningHUD] 중복된 EColorKeyType이 발견되었습니다: {keyType}");
                    continue;
                }

                _colorKeyDicts[keyType] = colorKeyUI;
            }

            // JudgmentText 초기 위치 설정
            if (_judgmentTextRect != null)
            {
                _judgmentStartPosition = _judgmentTextRect.anchoredPosition;
                _judgmentText.gameObject.SetActive(false);
            }

            await UniTask.Yield();
        }

        public override void OnHide()
        {
            // 실행 중인 Tween 정리
            if (_currentJudgmentTween != null && _currentJudgmentTween.IsActive())
            {
                _currentJudgmentTween.Kill();
                _currentJudgmentTween = null;
            }
        }

        public void ResetSetting(StageTableSO.StageRow stageRow, ColorKeyMapping colorKeyMapping)
        {
            if (stageRow == null)
            {
                Debug.LogError("[UI_RunningHUD] StageRow가 null입니다.");
                return;
            }

            if (colorKeyMapping == null || colorKeyMapping.IsEmpty)
            {
                Debug.LogError("[UI_RunningHUD] ColorKeyMapping이 null이거나 비어있습니다.");
                return;
            }

            SetColorKeyUIs(colorKeyMapping);
        }

        private void SetColorKeyUIs(ColorKeyMapping colorKeyMapping)
        {
            if (colorKeyMapping == null || colorKeyMapping.IsEmpty)
            {
                Debug.LogError("[UI_RunningHUD] ColorKeyMapping이 null이거나 비어있습니다.");
                return;
            }

            // 모든 키 패널을 먼저 비활성화
            foreach (var keyUI in _colorKeyUIs)
            {
                if (keyUI != null)
                {
                    keyUI.gameObject.SetActive(false);
                }
            }

            // ColorKeyMapping을 순회하며 ColorKeyUI에 색상 할당
            var allKeys = colorKeyMapping.GetAllKeys();
            foreach (EColorKeyType colorKeyType in allKeys)
            {
                // Dictionary에서 ColorKeyUI 가져오기
                if (!_colorKeyDicts.TryGetValue(colorKeyType, out ColorKeyUI keyUI))
                {
                    Debug.LogWarning($"[UI_RunningHUD] EColorKeyType '{colorKeyType}'에 해당하는 ColorKeyUI를 찾을 수 없습니다.");
                    continue;
                }

                // ColorKeyMapping에서 색상 가져오기
                EColorType colorToAssign = colorKeyMapping.GetColor(colorKeyType);
                if (colorToAssign == EColorType.None)
                {
                    Debug.LogWarning($"[UI_RunningHUD] ColorKeyType '{colorKeyType}'에 해당하는 색상을 찾을 수 없습니다.");
                    continue;
                }

                // ColorKeyUI에 색상 할당 및 활성화
                keyUI.SettingPathColorKey(colorToAssign);
            }

            Debug.Log($"[UI_RunningHUD] {allKeys.Count}개의 키 패널 설정 완료");
        }

        public ColorKeyUI GetColorKeyUI(EColorKeyType colorKeyType)
        {
            if (_colorKeyDicts.TryGetValue(colorKeyType, out ColorKeyUI colorKeyUI))
            {
                return colorKeyUI;
            }

            Debug.LogWarning($"[UI_RunningHUD] EColorKeyType '{colorKeyType}'에 해당하는 ColorKeyUI를 찾을 수 없습니다.");
            return null;
        }

        public void UpdateHealthGauge(float currentValue, float maxValue)
        {
            float fillValue = (maxValue > 0) ? (currentValue / maxValue) : 0f;

            // 이미지의 fillAmount를 즉시 변경
            if (_healthFillImage != null)
            {
                _healthFillImage.fillAmount = fillValue;
            }
        }

        public void UpdateProgressGauge(float progress)
        {
            // progress는 0.0 ~ 1.0 사이의 값 (0% ~ 100%)
            float fillValue = Mathf.Clamp01(progress);

            // Progress Bar의 fillAmount 업데이트
            if (_progressFillImage != null)
            {
                _progressFillImage.fillAmount = fillValue;
            }
        }

        public void UpdateComboText(int comboCount)
        {
            if (_comboText == null)
                return;

            if (comboCount > 0)
            {
                _comboText.text = $"{comboCount} Combo";
                _comboText.gameObject.SetActive(true);
            }
            else
            {
                _comboText.text = string.Empty;
                _comboText.gameObject.SetActive(false);
            }
        }

        public void ShowJudgment(EJudgmentType judgment)
        {
            if (_judgmentText == null || _judgmentTextRect == null)
                return;

            // 이전 애니메이션 취소
            if (_currentJudgmentTween != null && _currentJudgmentTween.IsActive())
            {
                _currentJudgmentTween.Kill();
            }

            // 판정 텍스트 설정
            _judgmentText.text = judgment.ToString();

            // 시작 위치로 설정
            _judgmentTextRect.anchoredPosition = _judgmentStartPosition;

            // 색상 초기화 (알파값 1)
            Color textColor = _judgmentText.color;
            textColor.a = 1f;
            _judgmentText.color = textColor;

            // 활성화
            _judgmentText.gameObject.SetActive(true);

            // DOTween 애니메이션 시작
            Vector2 endPos = _judgmentStartPosition + Vector2.up * _judgmentMoveDistance;

            // Sequence로 위치 이동과 페이드 아웃을 동시에 실행
            _currentJudgmentTween = DOTween.Sequence()
                .Append(_judgmentTextRect.DOAnchorPosY(endPos.y, _judgmentAnimationDuration)
                .SetEase(_judgmentMoveEase)) // 빠르게 시작해서 느리게 끝남
                .Join(_judgmentText.DOFade(0f, _judgmentAnimationDuration)) // 페이드 아웃
                .OnComplete(() =>
                {
                    // 애니메이션 완료 후 비활성화
                    if (_judgmentText != null)
                    {
                        _judgmentText.gameObject.SetActive(false);
                    }
                    _currentJudgmentTween = null;
                })
                .SetAutoKill(true);
        }

        public void OnClickSetting()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.RunningSetting);
        }
    }
}