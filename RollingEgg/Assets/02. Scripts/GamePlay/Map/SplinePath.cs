using RollingEgg.Util;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;


namespace RollingEgg
{
    public enum EPathType
    {
        None,

        L1,
        L2,
        L3,
        L4,
        L5,
    }


    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(LineRenderer))]
    public class SplinePath : MonoBehaviour
    {
        [Header("## Component Settings")]
        [SerializeField] private SplineContainer _splineContainer;
        [SerializeField] private LineRenderer _lineRenderer;

        [Header("## Path Settings")]
        [SerializeField] private float _lineWidth;          // Line 두께

        [Range(1, 50)]
        [SerializeField] private int _lineResoultion = 10;  // Line 해상도

        [SerializeField] private Color _lineColor;          // Line 색깔

        private bool _isUpdating = false;

        public SplineContainer SplineContainer => _splineContainer;
        public LineRenderer LineRenderer => _lineRenderer;

        private void OnEnable()
        {
#if UNITY_EDITOR
            Spline.Changed += OnSplineChanged;

            UpdateLineRenderer();
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            Spline.Changed -= OnSplineChanged;
#endif
        }

        /// <summary>
        /// 스플라인의 특정 지점(t: 0~1)의 월드 좌표를 반환합니다.
        /// </summary>
        public Vector3 GetPointAt(float t)
        {
            if (_splineContainer == null || _splineContainer.Spline == null)
                return Vector3.zero;

            var spline = _splineContainer.Spline;
            if (spline.Count < 2)
                return Vector3.zero;

            // t 값을 0~1 사이로 클램프
            t = Mathf.Clamp01(t);

            Vector3 localPos = spline.EvaluatePosition(t);
            return transform.TransformPoint(localPos);
        }

        /// <summary>
        /// 스플라인의 특정 지점(t: 0~1)에서의 접선 방향(월드 좌표)을 반환합니다.
        /// </summary>
        public Vector3 GetTangentAt(float t)
        {
            if (_splineContainer == null || _splineContainer.Spline == null)
                return Vector3.right;

            var spline = _splineContainer.Spline;
            if (spline.Count < 2)
                return Vector3.right;

            // t 값을 0~1 사이로 클램프
            t = Mathf.Clamp01(t);

            // 로컬 좌표에서 접선 가져오기
            float3 localTangent = spline.EvaluateTangent(t);
            Vector3 worldTangent = transform.TransformDirection((Vector3)localTangent);

            // 정규화
            if (worldTangent.magnitude > 0.01f)
            {
                return worldTangent.normalized;
            }

            return Vector3.right; // 기본값
        }

#if UNITY_EDITOR
        public void HandleSplineUpdate()
        {
            if (_isUpdating)
                return;

            _isUpdating = true;

            try
            {
                UpdateLineRenderer();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void Initialize()
        {
            _splineContainer = gameObject.GetOrAddComponent<SplineContainer>();
            _lineRenderer = gameObject.GetOrAddComponent<LineRenderer>();

            UpdateLineRenderer();
        }

        public void UpdateLineRenderer()
        {
            if (_splineContainer == null || _splineContainer.Spline == null || _lineRenderer == null)
                return;

            var spline = _splineContainer.Spline;
            if (spline.Count < 2)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = false;

                return;
            }

            // 라인 두께 설정
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;

            // 라인 색깔 설정
            _lineRenderer.startColor = _lineColor;
            _lineRenderer.endColor = _lineColor;

            // Spline에서 포인트 추출하여 LineRenderer에 설정
            int pointCount = spline.Count * _lineResoultion;
            _lineRenderer.enabled = true;
            _lineRenderer.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                Vector3 point = spline.EvaluatePosition(t);
                _lineRenderer.SetPosition(i, point);
            }
        }
        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            if (_splineContainer != null && _splineContainer.Spline == spline)
            {
                HandleSplineUpdate();
            }
        }
#endif
    }
}