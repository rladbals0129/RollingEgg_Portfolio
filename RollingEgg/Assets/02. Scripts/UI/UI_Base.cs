using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RollingEgg.UI
{
    public class UI_Base : MonoBehaviour
    {
        protected Canvas _canvas;

        public virtual void Show()
        {
            gameObject.SetActive(true);
            OnShow();
        }

        public virtual void OnShow()
        {
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
            OnHide();
        }

        public virtual void OnHide()
        {
        }

        public virtual void Initialize() { }

        public virtual async UniTask InitializeAsync() { await UniTask.Yield(); }

        /// <summary>
        /// UI가 풀에서 꺼내졌을 때 호출된다.
        /// </summary>
        public virtual void OnRentFromPool() { }

        /// <summary>
        /// UI가 풀로 반환될 때 호출된다.
        /// </summary>
        public virtual void OnReturnToPool() { }
    }
}
