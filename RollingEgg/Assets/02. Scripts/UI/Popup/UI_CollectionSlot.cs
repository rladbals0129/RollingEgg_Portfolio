using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RollingEgg.Data;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using RollingEgg.Core;

namespace RollingEgg.UI
{
    public class UI_CollectionSlot : UI_Base, IPointerEnterHandler, IPointerExitHandler
    {
        private const string TABLE_EVOLVED_FORMS = "EvolvedForm";
        private const string TABLE_EGGS = "Eggs";
        private const string TABLE_COLLECTION = "UI_Collection";

        [Header("UI Components")]
        [SerializeField] private Image _imgIcon;
        [SerializeField] private TMP_Text _txtName;
        [SerializeField] private TMP_Text _txtDesc; 

        private EvolvedFormTableSO.EvolvedFormRow _data;
        private bool _isUnlocked;
        
        private Action<EvolvedFormTableSO.EvolvedFormRow, bool, RectTransform> _onHoverEnter;
        private Action _onHoverExit;

        private AsyncOperationHandle<Sprite> _iconHandle;
        private CancellationTokenSource _localizeCts;

        private void OnDestroy()
        {
            OnReturnToPool();
        }

        private void OnDisable()
        {
            CancelLocalizationTasks();
        }

        public void Init(Action<EvolvedFormTableSO.EvolvedFormRow, bool, RectTransform> onHoverEnter, Action onHoverExit)
        {
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;
        }

        public void SetData(EvolvedFormTableSO.EvolvedFormRow data, bool isUnlocked)
        {
            CancelLocalizationTasks();

            _data = data;
            _isUnlocked = isUnlocked;

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_data == null)
                return;

            if (_isUnlocked)
            {
                if (_txtDesc != null)
                    _txtDesc.text = string.Empty;

                ScheduleSlotInfoUpdate();
            }
            else
            {
                ScheduleLockedInfoUpdate();
            }

            LoadIcon();
        }

        private void ScheduleSlotInfoUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (_txtName != null)
                _txtName.text = _data?.nameLocKey ?? string.Empty;

            CancelLocalizationTasks();
            _localizeCts = new CancellationTokenSource();
            UpdateSlotInfoAsync(_localizeCts.Token).Forget();
        }

        private void ScheduleLockedInfoUpdate()
        {
            if (_txtName != null)
                _txtName.text = "???";

            if (_txtDesc != null)
                _txtDesc.text = "아직 획득하지 못한 진화체입니다.";

            if (!Application.isPlaying)
                return;

            CancelLocalizationTasks();
            _localizeCts = new CancellationTokenSource();
            UpdateLockedInfoAsync(_localizeCts.Token).Forget();
        }

        private async UniTask UpdateSlotInfoAsync(CancellationToken token)
        {
            if (_data == null)
                return;

            if (!ServiceLocator.HasService<ILocalizationService>())
                return;

            var locService = ServiceLocator.Get<ILocalizationService>();
            if (locService == null)
                return;

            string evolvedName = _data.nameLocKey;

            try
            {
                string localized = await locService
                    .GetAsync(TABLE_EVOLVED_FORMS, _data.nameLocKey)
                    .AttachExternalCancellation(token);

                if (!string.IsNullOrEmpty(localized))
                    evolvedName = localized;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionSlot] 이름 로컬라이즈 실패: {ex.Message}");
            }

            if (token.IsCancellationRequested)
                return;

            if (_txtName != null)
                _txtName.text = evolvedName;

            if (_txtDesc == null)
                return;

            string eggNameKey = GetEggNameKey(_data.eggType);
            string eggName = eggNameKey;

            try
            {
                string localizedEgg = await locService
                    .GetAsync(TABLE_EGGS, eggNameKey)
                    .AttachExternalCancellation(token);

                if (!string.IsNullOrEmpty(localizedEgg))
                    eggName = localizedEgg;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionSlot] 알 이름 로컬라이즈 실패: {ex.Message}");
            }

            if (token.IsCancellationRequested)
                return;

            string localizedDesc = string.Empty;
            try
            {
                localizedDesc = await GetUnlockedDescriptionAsync(locService, eggName, evolvedName, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!string.IsNullOrEmpty(localizedDesc))
            {
                _txtDesc.text = localizedDesc;
            }
            else
            {
                string josa = HasJongseong(evolvedName) ? "이" : "가";
                _txtDesc.text = $"{eggName}을 키웠더니 {evolvedName}{josa} 되었다.";
            }
        }

        private async UniTask UpdateLockedInfoAsync(CancellationToken token)
        {
            if (!ServiceLocator.HasService<ILocalizationService>())
                return;

            var locService = ServiceLocator.Get<ILocalizationService>();
            if (locService == null)
                return;

            try
            {
                string localizedName = await locService
                    .GetAsync(TABLE_COLLECTION, "collection_slot_locked_name")
                    .AttachExternalCancellation(token);

                if (!token.IsCancellationRequested && _txtName != null && !string.IsNullOrEmpty(localizedName))
                    _txtName.text = localizedName;

                string localizedDesc = await locService
                    .GetAsync(TABLE_COLLECTION, "collection_slot_locked_desc")
                    .AttachExternalCancellation(token);

                if (!token.IsCancellationRequested && _txtDesc != null && !string.IsNullOrEmpty(localizedDesc))
                    _txtDesc.text = localizedDesc;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionSlot] 잠금 정보 로컬라이즈 실패: {ex.Message}");
            }
        }

        private async UniTask<string> GetUnlockedDescriptionAsync(ILocalizationService locService, string eggName, string evolvedName, CancellationToken token)
        {
            if (locService == null)
                return string.Empty;

            try
            {
                string localeCode = locService.GetCurrentLocaleCode();
                string particle = NeedsKoreanParticle(localeCode) ? (HasJongseong(evolvedName) ? "이" : "가") : string.Empty;

                string localized = await locService
                    .GetAsync(TABLE_COLLECTION, "collection_slot_unlocked_desc",
                        new
                        {
                            EggName = eggName,
                            EvolvedName = evolvedName,
                            ParticleSubject = particle
                        })
                    .AttachExternalCancellation(token);

                return localized;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI_CollectionSlot] 슬롯 설명 로컬라이즈 실패: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetEggNameKey(string eggType)
        {
            if (string.IsNullOrEmpty(eggType)) return "egg_blue"; // default
            return eggType.ToLowerInvariant() switch
            {
                "blue" => "egg_blue",
                "red" => "egg_red",
                "white" => "egg_white",
                "black" => "egg_black",
                "yellow" => "egg_yellow",
                _ => $"egg_{eggType.ToLowerInvariant()}"
            };
        }

        /// <summary>
        /// 한글 종성(받침) 유무 확인
        /// </summary>
        private bool HasJongseong(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            char last = text[text.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return false;
            return (last - 0xAC00) % 28 != 0;
        }

        private bool NeedsKoreanParticle(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode))
                return false;

            return localeCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadIcon()
        {
            ReleaseIcon();

            if (_imgIcon != null)
            {
                _imgIcon.sprite = null;
                _imgIcon.enabled = false;
            }

            if (_data == null || _data.icon == null || !_data.icon.RuntimeKeyIsValid())
            {
                return;
            }

            if (_data.icon.OperationHandle.IsValid())
            {
                Addressables.ResourceManager.Acquire(_data.icon.OperationHandle);
                _iconHandle = _data.icon.OperationHandle.Convert<Sprite>();
                if (_iconHandle.IsDone)
                {
                    OnIconLoaded(_iconHandle);
                }
                else
                {
                    _iconHandle.Completed += OnIconLoaded;
                }
                return;
            }

            _iconHandle = _data.icon.LoadAssetAsync<Sprite>();
            _iconHandle.Completed += OnIconLoaded;
        }

        private void OnIconLoaded(AsyncOperationHandle<Sprite> handle)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                return;
            }

            if (!this || _imgIcon == null)
            {
                return;
            }

            _imgIcon.sprite = handle.Result;
            _imgIcon.enabled = true;
            _imgIcon.color = _isUnlocked ? Color.white : Color.black;
        }

        private void ReleaseIcon()
        {
            if (_iconHandle.IsValid())
            {
                _iconHandle.Completed -= OnIconLoaded;
                Addressables.Release(_iconHandle);
            }

            if (_imgIcon != null)
            {
                _imgIcon.sprite = null;
                _imgIcon.enabled = false;
            }

            _iconHandle = default;
        }

        private void CancelLocalizationTasks()
        {
            if (_localizeCts == null)
                return;

            _localizeCts.Cancel();
            _localizeCts.Dispose();
            _localizeCts = null;
        }

        public override void OnRentFromPool()
        {
            base.OnRentFromPool();
            gameObject.SetActive(true);
        }

        public override void OnReturnToPool()
        {
            base.OnReturnToPool();
            CancelLocalizationTasks();
            ReleaseIcon();

            _data = null;
            _isUnlocked = false;
            _onHoverEnter = null;
            _onHoverExit = null;

            if (_txtName != null) _txtName.text = string.Empty;
            if (_txtDesc != null) _txtDesc.text = string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_data == null) return;
            _onHoverEnter?.Invoke(_data, _isUnlocked, GetComponent<RectTransform>());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoverExit?.Invoke();
        }
    }
}