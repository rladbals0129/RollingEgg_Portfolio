using Cysharp.Threading.Tasks;
using RollingEgg;
using RollingEgg.UI;
using RollingEgg.Util;
using UnityEngine;

namespace RollingEgg.Lobby
{
    public enum LobbyInteractionType
    {
        None,
        StageSelect,
        Nurture,
        Portal,
        Mirror
    }

    /// <summary>
    /// 상호작용 가능한 오브젝트
    /// 플레이어가 가까이 오면 상호작용 UI를 표시합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string _interactionMessage = "E"; // 상호작용 메시지
        [Header("Action Settings")]
        [SerializeField] private LobbyInteractionType _interactionType = LobbyInteractionType.None;
        [SerializeField] private int _targetEggId = 1;
        [SerializeField] private string _overrideEggType = string.Empty;
        [Header("Portal Settings")]
        [SerializeField] private Transform _portalDestinationPoint;
        [SerializeField] private BoxCollider2D _portalDestinationBoundary;
        [SerializeField] private bool _portalSnapCameraInstantly = true;
        [Header("Animation Settings")]
        [SerializeField] private float _minAnimationSpeed = 0.8f;
        [SerializeField] private float _maxAnimationSpeed = 1.2f;

        private static InteractableObject _currentInteractionOwner;
        private bool _canInteract = false; // 상호작용 가능 여부
        private UI_Lobby _lobbyUI;
        private Collider2D _collider2D;
        private Transform _cachedPlayer;
        private Camera _mainCamera;
        private bool _isPortaling;
        private bool _isBlockedByPopup;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            _mainCamera = Camera.main;
            ConfigureInteractionCollider();
        }

        private void Start()
        {
            TryCacheLobbyUI();
            
            // Nurture 타입인 경우 애니메이션 랜덤 속도 적용
            if (_interactionType == LobbyInteractionType.Nurture)
            {
                ApplyEggAnimationSpeed();
            }
        }

        private void Update()
        {
            _isBlockedByPopup = IsInteractionBlocked();
            if (_isBlockedByPopup)
            {
                if (_currentInteractionOwner == this)
                    HideInteractionHint();
                _canInteract = false;
                return;
            }

            TryCacheLobbyUI();
            HandleMouseClickInteraction();

            if (_canInteract && Input.GetKeyDown(KeyCode.E))
            {
                OnInteract();
            }

            if (_lobbyUI == null)
                return;

            if (_canInteract)
            {
                if (_currentInteractionOwner == null)
                {
                    ShowInteractionHint();
                }

                if (_currentInteractionOwner == this)
                {
                    _lobbyUI.ShowInteractionUI(transform.position);
                }

            }
            else if (_currentInteractionOwner == this)
            {
                HideInteractionHint();
            }
        }

        /// <summary>
        /// 상호작용 실행
        /// </summary>
        private void OnInteract()
        {
            if (_lobbyUI == null)
            {
                Debug.LogWarning("[InteractableObject] Lobby UI를 찾을 수 없습니다.");
                return;
            }

            switch (_interactionType)
            {
                case LobbyInteractionType.StageSelect:
                    HandleStageInteraction();
                    break;
                case LobbyInteractionType.Nurture:
                    HandleNurtureInteraction();
                    break;
                case LobbyInteractionType.Portal:
                    HandlePortalInteraction();
                    break;
                case LobbyInteractionType.Mirror:
                    HandleMirrorInteraction();
                    break;
                default:
                    Debug.Log($"상호작용: {gameObject.name}");
                    break;
            }
        }

        private void HandleStageInteraction()
        {
            if (_targetEggId <= 0)
            {
                Debug.LogWarning("[InteractableObject] 유효하지 않은 알 ID입니다.");
                return;
            }

            _lobbyUI.HandleStageInteraction(_targetEggId, _overrideEggType);
        }

        private void HandleNurtureInteraction()
        {
            int eggId = _targetEggId <= 0 ? 1 : _targetEggId;
            _lobbyUI.HandleNurtureInteraction(eggId, _overrideEggType);
        }

        /// <summary>
        /// 알 애니메이션에 랜덤 속도를 적용합니다.
        /// </summary>
        private void ApplyEggAnimationSpeed()
        {
            AnimationUtil.ApplyRandomSpeedToGameObject(gameObject, _minAnimationSpeed, _maxAnimationSpeed, true);
        }

        private void HandleMirrorInteraction()
        {
            var popup = UIManager.Instance.ShowPopup(EPopupUIType.Collection);
            if (popup == null)
            {
                Debug.LogWarning("[InteractableObject] 도감 팝업을 열 수 없습니다.");
            }
        }

        private void ShowInteractionHint()
        {
            if (_lobbyUI == null)
                return;

            if (_currentInteractionOwner != this)
            {
                _currentInteractionOwner = this;
                _lobbyUI.SetInteractionText(GetInteractionMessage());
            }

            _lobbyUI.ShowInteractionUI(transform.position);
        }

        private bool IsInteractionBlocked()
        {
            return UIManager.Instance != null && UIManager.Instance.HasAnyPopupActive();
        }

        private void HideInteractionHint()
        {
            if (_currentInteractionOwner != this)
                return;

            _currentInteractionOwner = null;
            _lobbyUI?.HideInteractionUI();
            _lobbyUI?.SetInteractionText(string.Empty);
        }

        private void OnDisable()
        {
            HideInteractionHint();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            _canInteract = true;
            _cachedPlayer = other.transform;
            ShowInteractionHint();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (!_canInteract)
            {
                _canInteract = true;
                _cachedPlayer = other.transform;
                ShowInteractionHint();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            _canInteract = false;
            if (_cachedPlayer != null && other.transform == _cachedPlayer)
            {
                _cachedPlayer = null;
            }
            HideInteractionHint();
        }

        private void TryCacheLobbyUI()
        {
            if (_lobbyUI == null && UIManager.Instance != null)
            {
                _lobbyUI = UIManager.Instance.GetCurrentScene<UI_Lobby>();
            }

            if (_lobbyUI != null && _canInteract && _currentInteractionOwner == null)
            {
                // UI를 늦게 찾은 경우 즉시 힌트 표시
                ShowInteractionHint();
            }
        }

        private string GetInteractionMessage()
        {
            return string.IsNullOrWhiteSpace(_interactionMessage) ? "E" : _interactionMessage;
        }

        private void HandleMouseClickInteraction()
        {
            // 팝업이 열려있으면 터치 상호작용 차단
            if (IsInteractionBlocked())
                return;

            if (!Input.GetMouseButtonUp(0))
                return;

            if (!IsPointerOverInteractionCollider())
                return;

            TryCacheLobbyUI();
            if (_lobbyUI == null)
                return;

            OnInteract();
        }

        private bool IsPointerOverInteractionCollider()
        {
            if (_collider2D == null)
                return false;

            Camera cam = Camera.main != null ? Camera.main : _mainCamera;
            if (cam == null)
                return false;

            Vector3 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pointerPosition = new Vector2(worldPoint.x, worldPoint.y);
            return _collider2D.OverlapPoint(pointerPosition);
        }

        private void ConfigureInteractionCollider()
        {
            if (_collider2D == null)
                return;

            _collider2D.isTrigger = true;
        }

        private void HandlePortalInteraction()
        {
            if (_isPortaling)
                return;

            HandlePortalWithFadeAsync().Forget();
        }

        private async UniTaskVoid HandlePortalWithFadeAsync()
        {
            _isPortaling = true;
            try
            {
                if (UIManager.Instance != null)
                {
                    await UIManager.Instance.RunWithFadeAsync(async () =>
                    {
                        TeleportPlayer();
                        await UniTask.Yield(); // 1프레임 보장 (카메라/바운더리 갱신)
                    });
                }
                else
                {
                    TeleportPlayer();
                }
            }
            finally
            {
                _isPortaling = false;
            }
        }

        private void TeleportPlayer()
        {
            if (_cachedPlayer == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                _cachedPlayer = playerObj != null ? playerObj.transform : null;
            }

            if (_cachedPlayer == null)
            {
                Debug.LogWarning("[InteractableObject] 포탈에 사용할 플레이어 참조가 없습니다.");
                return;
            }

            if (_portalDestinationPoint == null)
            {
                Debug.LogWarning("[InteractableObject] 포탈 목적지가 설정되지 않았습니다.", this);
                return;
            }

            Vector3 targetPosition = _portalDestinationPoint.position;
            targetPosition.z = _cachedPlayer.position.z;
            _cachedPlayer.position = targetPosition;

            var rigidbody = _cachedPlayer.GetComponent<Rigidbody2D>();
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector2.zero;
                rigidbody.angularVelocity = 0f;
            }

            var controller = _cachedPlayer.GetComponent<LobbyPlayerController>();
            if (controller != null && _portalDestinationBoundary != null)
            {
                controller.SetMapBoundary(_portalDestinationBoundary);
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var lobbyCamera = mainCamera.GetComponent<LobbyCamera>();
                if (lobbyCamera != null && _portalDestinationBoundary != null)
                {
                    lobbyCamera.mapBoundary = _portalDestinationBoundary;
                    if (_portalSnapCameraInstantly)
                    {
                        Vector3 camPos = lobbyCamera.transform.position;
                        lobbyCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, camPos.z);
                    }
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 상호작용 범위 시각화 (에디터 전용)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            var collider = GetComponent<Collider2D>();
            if (collider == null)
                return;

            if (collider is CircleCollider2D circle)
            {
                Vector3 center = circle.transform.TransformPoint(circle.offset);
                float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
                float radius = Mathf.Max(0.01f, circle.radius * scale);
                Gizmos.DrawWireSphere(center, radius);
            }
            else
            {
                var bounds = collider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }


    
}
