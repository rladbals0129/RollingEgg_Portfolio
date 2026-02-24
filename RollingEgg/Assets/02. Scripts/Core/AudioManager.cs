using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Core
{
    #region Audio Key
    /// <summary>
    /// BGM 오디오 키 (Addressable Key와 동일)
    /// </summary>
    public enum EBGMKey
    {
        None = 0,

        // Title Scene
        BGM_Title,
        BGM_Lobby,
        BGM_Nurture,

        // Running Scene
        BGM_Running_Blue,
        BGM_Running_Red,
        BGM_Running_White,
        BGM_Running_Black,
        BGM_Running_Yellow,

        // Result Scene
        BGM_Result,
    }

    /// <summary>
    /// SFX 오디오 키 (Addressable Key와 동일)
    /// </summary>
    public enum ESFXKey
    {
        None = 0,

        // UI
        SFX_ButtonClick,
        SFX_ButtonHover,
        SFX_PopupOpen,
        SFX_PopupClose,

        // Running Game
        SFX_ColorChange,
        SFX_Judgment_Perfect,
        SFX_Judgment_Great,
        SFX_Judgment_Good,
        SFX_Judgment_Bad,
        SFX_Judgment_Miss,
        SFX_SafeZone_Enter,
        SFX_SafeZone_Exit,
        SFX_Combo,

        // Result
        SFX_Result_Appear,
        SFX_Result_Rank,
    }
    #endregion

    /// <summary>
    /// 오디오 재생 및 관리를 담당하는 서비스 인터페이스
    /// </summary>
    public interface IAudioService
    {
        UniTask InitializeAsync();

        // BGM 관련
        UniTask PlayBGM(EBGMKey bgmKey, bool loop = true);
        void StopBGM();
        void PauseBGM();
        void ResumeBGM();
        bool IsBGMPlaying { get; }

        // SFX 관련
        UniTask PlaySFX(ESFXKey sfxKey, float volumeScale = 1f);
        UniTask PlaySFXOneShot(ESFXKey sfxKey, float volumeScale = 1f);

        // 볼륨 조절
        float MasterVolume { get; set; }
        float BGMVolume { get; set; }
        float SFXVolume { get; set; }

        // 볼륨 저장/로드
        UniTask SaveVolumeSettingsAsync();
    }

    public class AudioManager : MonoBehaviour, IAudioService
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Volume Settings")]
        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private float _bgmVolume = 1f;
        [SerializeField] private float _sfxVolume = 1f;

        private IResourceService _resourceService;
        private ISettingsService _settingsService;
        private Dictionary<string, AudioClip> _audioClipCache = new Dictionary<string, AudioClip>();

        public bool IsBGMPlaying => _bgmSource != null && _bgmSource.isPlaying;

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }

        public float BGMVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }

        public async UniTask InitializeAsync()
        {
            _resourceService = ServiceLocator.Get<IResourceService>();
            _settingsService = ServiceLocator.Get<ISettingsService>();

            // AudioSource 초기화
            if (_bgmSource == null)
            {
                GameObject bgmObject = new GameObject("BGM AudioSource");
                bgmObject.transform.SetParent(transform);
                _bgmSource = bgmObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
            }

            if (_sfxSource == null)
            {
                GameObject sfxObject = new GameObject("SFX AudioSource");
                sfxObject.transform.SetParent(transform);
                _sfxSource = sfxObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
            }

            // 볼륨 설정 로드
            await LoadVolumeSettingsAsync();

            UpdateVolume();

            Debug.Log("[AudioService] 초기화 완료");
            await UniTask.Yield();
        }

        public async UniTask PlayBGM(EBGMKey bgmKey, bool loop = true)
        {
            if (bgmKey == EBGMKey.None)
            {
                Debug.LogWarning("[AudioService] BGM 키가 None입니다.");
                return;
            }

            string addressableKey = bgmKey.ToAddressableKey();
            AudioClip clip = await LoadAudioClipAsync(addressableKey);

            if (clip == null)
            {
                Debug.LogWarning($"[AudioService] BGM을 로드할 수 없습니다: {bgmKey} ({addressableKey})");
                return;
            }

            if (_bgmSource != null)
            {
                _bgmSource.clip = clip;
                _bgmSource.loop = loop;
                _bgmSource.Play();
                Debug.Log($"[AudioService] BGM 재생: {bgmKey}");
            }
        }

        public void StopBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
                Debug.Log("[AudioService] BGM 정지");
            }
        }

        public void PauseBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
                Debug.Log("[AudioService] BGM 일시정지");
            }
        }

        public void ResumeBGM()
        {
            if (_bgmSource != null && !_bgmSource.isPlaying)
            {
                _bgmSource.UnPause();
                Debug.Log("[AudioService] BGM 재개");
            }
        }

        public async UniTask PlaySFX(ESFXKey sfxKey, float volumeScale = 1f)
        {
            if (sfxKey == ESFXKey.None)
            {
                Debug.LogWarning("[AudioService] SFX 키가 None입니다.");
                return;
            }

            string addressableKey = sfxKey.ToAddressableKey();
            AudioClip clip = await LoadAudioClipAsync(addressableKey);

            if (clip == null)
            {
                Debug.LogWarning($"[AudioService] SFX를 로드할 수 없습니다: {sfxKey} ({addressableKey})");
                return;
            }

            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(clip, volumeScale);
            }
        }

        public async UniTask PlaySFXOneShot(ESFXKey sfxKey, float volumeScale = 1f)
        {
            await PlaySFX(sfxKey, volumeScale);
        }

        private async UniTask<AudioClip> LoadAudioClipAsync(string audioKey)
        {
            if (string.IsNullOrEmpty(audioKey))
                return null;

            // 캐시 확인
            if (_audioClipCache.TryGetValue(audioKey, out AudioClip cachedClip))
            {
                return cachedClip;
            }

            // 리소스 로드
            if (_resourceService != null)
            {
                AudioClip clip = await _resourceService.LoadAssetAsync<AudioClip>(audioKey);
                if (clip != null)
                {
                    _audioClipCache[audioKey] = clip;
                    return clip;
                }
            }

            return null;
        }

        private void UpdateVolume()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = _masterVolume * _bgmVolume;
            }

            if (_sfxSource != null)
            {
                _sfxSource.volume = _masterVolume * _sfxVolume;
            }
        }

        private async UniTask LoadVolumeSettingsAsync()
        {
            if (_settingsService == null)
                return;

            // SettingsService에 볼륨 설정이 있다면 로드
            // TODO: SettingsService에 볼륨 필드 추가 시 연동
            await UniTask.Yield();
        }

        public async UniTask SaveVolumeSettingsAsync()
        {
            if (_settingsService == null)
                return;

            // SettingsService에 볼륨 설정 저장
            // TODO: SettingsService에 볼륨 필드 추가 시 연동
            await UniTask.Yield();
        }

        private void OnDestroy()
        {
            // 캐시 정리
            _audioClipCache.Clear();
        }
    }
}
