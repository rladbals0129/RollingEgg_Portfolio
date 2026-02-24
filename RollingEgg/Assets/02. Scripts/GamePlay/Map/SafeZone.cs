using RollingEgg.Core;
using RollingEgg.Util;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg
{
    public enum EJudgmentType
    {
        MISS,
        BAD,
        GOOD,
        GREAT,
        PERFECT,
    }

    public enum EColorKeyType
    {
        None = 0,

        S = 1,
        D = 2,
        F = 3,

        J = 10,
        K = 11,
        L = 12
    }

    [ExecuteAlways]
    public class SafeZone : MonoBehaviour
    {
        // 판정 등급 배율 상수
        private const float PERFECT_THRESHOLD = 0.25f;
        private const float GREAT_THRESHOLD = 0.4f;
        private const float GOOD_THRESHOLD = 0.6f;
        private const float BAD_THRESHOLD = 0.85f;

        [Header("SafeZone Settings")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _size = 0.3f;

        [Header("Path Types")]
        [SerializeField] private List<EColorKeyType> _colorKeyTypes = new List<EColorKeyType>();

        private CircleCollider2D _collider;

        // 원 조각들을 저장할 리스트
        private List<GameObject> _segmentObjects = new List<GameObject>();
        private List<SpriteRenderer> _segmentRenderers = new List<SpriteRenderer>();

        private bool _isActive = true;

        public float Size => _size;
        public bool IsActive => _isActive;
        public IReadOnlyList<EColorKeyType> ColorKeyTypes => _colorKeyTypes;

#if UNITY_EDITOR
        // =================================================================================
        // Editor 모드에서만 사용되는 내부 변수들
        // =================================================================================
        private List<EColorKeyType> _previousColorKeyTypes = new List<EColorKeyType>();
        private float _previousSize;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            InitializeInEditor();
#endif
        }

        private void OnTriggerEnter2D(Collider2D coll)
        {
            var player = coll.GetComponent<PlayerController>();
            if (player != null)
            {
                player.InSafeZone(this);
            }
        }

        private void OnTriggerExit2D(Collider2D coll)
        {
            var player = coll.GetComponent<PlayerController>();
            if (player != null)
            {
                player.OutSafeZone();
            }
        }

        public void UpdateSize()
        {
            transform.localScale = Vector3.one * _size;
        }

        public void UpdateVisualization(ColorKeyMapping colorKeyMapping = null)
        {
            // 기존 조각들 제거
            ClearSegments();

            if (_colorKeyTypes == null || _colorKeyTypes.Count == 0)
            {
                Debug.LogWarning("[SafeZone] ColorKeyTypes가 비어있습니다.");
                return;
            }

            int segmentCount = _colorKeyTypes.Count;
            float anglePerSegment = 360f / segmentCount;

            // 각 조각 생성
            for (int i = 0; i < segmentCount; i++)
            {
                GameObject segmentObj = new GameObject($"Segment_{i}");

                segmentObj.transform.SetParent(transform);
                segmentObj.transform.localPosition = Vector3.zero;
                segmentObj.transform.localRotation = Quaternion.identity;
                segmentObj.transform.localScale = Vector3.one;

                // SpriteRenderer 추가
                SpriteRenderer segmentRenderer = segmentObj.AddComponent<SpriteRenderer>();
                segmentRenderer.sortingOrder = 5;

                // 원형 Sprite 생성 (또는 Resources에서 로드)
                Sprite segmentSprite = CreateCircleSegmentSprite(anglePerSegment);
                segmentRenderer.sprite = segmentSprite;

                // 색상 설정
                EColorKeyType colorKeyType = _colorKeyTypes[i];
                Color segmentColor;

                if (colorKeyMapping != null && !colorKeyMapping.IsEmpty)
                {
                    // ColorKeyMapping에서 실제 색상 가져오기
                    EColorType colorType = colorKeyMapping.GetColor(colorKeyType);
                    segmentColor = PathColorUtil.GetColorFromPathColor(colorType);
                }
                else
                {
                    // 에디터 모드에서는 기본 색상 사용 (하위 호환성)
                    segmentColor = PathColorUtil.GetColorFromKeyType(colorKeyType);
                }

                segmentRenderer.color = segmentColor;

                // 회전 설정 (각 조각이 올바른 위치에 오도록)
                float startAngle = i * anglePerSegment;
                segmentObj.transform.localRotation = Quaternion.Euler(0, 0, startAngle);

                _segmentObjects.Add(segmentObj);
                _segmentRenderers.Add(segmentRenderer);
            }
        }

        /// <summary>
        /// 거리에 따른 판정 등급 계산
        /// </summary>
        public EJudgmentType GetJudgmentType(float playerX)
        {
            float normalizedDistance = GetNormalizedDistanceX(playerX);

            if (normalizedDistance <= PERFECT_THRESHOLD)
                return EJudgmentType.PERFECT;

            if (normalizedDistance <= GREAT_THRESHOLD)
                return EJudgmentType.GREAT;

            if (normalizedDistance <= GOOD_THRESHOLD)
                return EJudgmentType.GOOD;

            if (normalizedDistance <= BAD_THRESHOLD)
                return EJudgmentType.BAD;

            return EJudgmentType.MISS;
        }

        public EJudgmentType GetJudgmentType(Vector3 position)
        {
            return GetJudgmentType(position.x);
        }

        public void SafeZoneDestory()
        {
            // TODO 애니메이션 혹은 파티클
            Destroy(gameObject);
        }

        /// <summary>
        /// X좌표 거리에 따른 정규화된 거리 반환 (0.0 ~ 1.0)
        /// </summary>
        private float GetNormalizedDistanceX(float playerX)
        {
            float centerX = transform.position.x;
            float distanceX = Mathf.Abs(playerX - centerX);
            float radius = _size * 0.5f; // 반지름
            return Mathf.Clamp01(distanceX / radius); // 0.0 (중심) ~ 1.0 (가장자리)
        }

        /// <summary>
        /// 거리에 따른 판정 등급 반환 (중심으로부터의 거리 비율)
        /// </summary>
        private float GetNormalizedDistance(Vector3 position)
        {
            Vector3 center = transform.position;
            float distance = Vector3.Distance(position, center);
            float radius = _size * 0.5f; // 반지름
            return Mathf.Clamp01(distance / radius); // 0.0 (중심) ~ 1.0 (가장자리)
        }

        /// <summary>
        /// 기존 조각들 제거 (에디터/런타임 공통)
        /// </summary>
        private void ClearSegments()
        {
            foreach (var segment in _segmentObjects)
            {
                // 에디터 모드에서는 DestroyImmediate 사용
                if (!Application.isPlaying)
                {
                    DestroyImmediate(segment);
                }
                else
                {
                    Destroy(segment);
                }
            }

            _segmentObjects.Clear();
            _segmentRenderers.Clear();

            // 리스트가 비어있어도 transform의 자식 중 Segment로 보이는 것들을 제거
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("Segment_"))
                {
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                    else
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 원의 조각 Sprite를 생성 (원형을 마스크하여 조각처럼 보이게)
        /// </summary>
        private Sprite CreateCircleSegmentSprite(float angle)
        {
            Texture2D circleTexture = CreateCircleTexture(angle);
            Sprite sprite = Sprite.Create(
                circleTexture,
                new Rect(0, 0, circleTexture.width, circleTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            return sprite;
        }

        /// <summary>
        /// 원의 조각 텍스처를 생성
        /// </summary>
        private Texture2D CreateCircleTexture(float angle)
        {
            int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            float halfAngle = angle * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float angleFromCenter = Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg;

                    // 각도 정규화
                    if (angleFromCenter < 0) angleFromCenter += 360f;

                    // 조각 범위 내에 있고 원 안에 있는지 확인
                    bool inAngleRange = angleFromCenter <= halfAngle || angleFromCenter >= (360f - halfAngle);
                    bool inCircle = distance <= radius;

                    if (inCircle && inAngleRange)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

#if UNITY_EDITOR
        public void InitializeInEditor()
        {
            // 초기값 저장
            _previousSize = _size;
            _previousColorKeyTypes = new List<EColorKeyType>(_colorKeyTypes);

            // 초기 업데이트
            UpdateSize();
            UpdateVisualization();
        }

        public void HandleLiveUpdate()
        {
            bool sizeChanged = Mathf.Abs(_size - _previousSize) > 0.001f;
            bool colorKeyChanged = HasColorKeyTypesChanged();

            if (sizeChanged)
            {
                _previousSize = _size;
                UpdateSize();
            }

            if (colorKeyChanged)
            {
                _previousColorKeyTypes = new List<EColorKeyType>(_colorKeyTypes);
                UpdateVisualization();
            }
        }

        private bool HasColorKeyTypesChanged()
        {
            if (_colorKeyTypes == null)
            {
                return _previousColorKeyTypes.Count > 0;
            }

            if (_colorKeyTypes.Count != _previousColorKeyTypes.Count)
            {
                return true;
            }

            for (int i = 0; i < _colorKeyTypes.Count; i++)
            {
                if (_colorKeyTypes[i] != _previousColorKeyTypes[i])
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
