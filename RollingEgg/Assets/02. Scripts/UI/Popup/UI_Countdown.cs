using Cysharp.Threading.Tasks;
using RollingEgg.UI;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

namespace RollingEgg
{
    public class UI_Countdown : UI_Popup
    {
        [SerializeField] private TMP_Text _countdownText;

        private CancellationTokenSource _countdownCts;
        public override void OnHide()
        {
            if (_countdownCts != null)
            {
                _countdownCts.Cancel();
                _countdownCts.Dispose();
                _countdownCts = null;
            }
        }

        public async UniTask StartCountdownAsync(int count = 3, Action callback = null)
        {
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = new CancellationTokenSource();

            var token = _countdownCts.Token;

            try
            {
                // 3, 2, 1, Start 표시
                for (int i = count; i > 0; i--)
                {
                    token.ThrowIfCancellationRequested();
                    _countdownText.text = i.ToString();
                    await UniTask.Delay(1000, cancellationToken: token);
                }

                token.ThrowIfCancellationRequested();
                _countdownText.text = "START!";
                await UniTask.Delay(500, cancellationToken: token);

                token.ThrowIfCancellationRequested();
                //UIManager.Instance.CloseCurrentPopup();
                var uiManager = UIManager.Instance;
                if (uiManager == null)
                {
                    Hide();
                    return;
                }
    
                uiManager.ClosePopup(this);
                callback?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 카운트다운이 취소된 경우 정상 처리
                Debug.Log("[UI_Countdown] 카운트다운이 취소되었습니다.");
            }
        }
    }
}
