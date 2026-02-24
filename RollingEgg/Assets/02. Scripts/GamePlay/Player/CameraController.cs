using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using UnityEngine;

namespace RollingEgg
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Transform _target;

        [Range(0.01f, 1.0f)]
        [SerializeField] private float _smoothSpeed = 0.125f;

        [SerializeField] private Vector2 _offset;

        [Header("Cinematic Settings")]
        [SerializeField] private float _cinematicDuration = 3f;
        [SerializeField] private Ease _cinematicEase = Ease.OutQuart;

        private bool _isInCinematicMode = false;
        private Vector3 _originPosition;

        private void LateUpdate()
        {
            if (!_isInCinematicMode && _target != null)
            {
                FollowTarget();
            }
        }

        public void SetupTarget(Transform target)
        {
            _target = target;
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        }

        public async UniTask StartRunningCinematicAsync(Vector3 endPointPosition, Vector3 playerPosition)
        {
            _isInCinematicMode = true;
            _originPosition = transform.position;

            // 카메라를 EndPoint 위치로 즉시 이동
            Vector3 startPosition = new Vector3(endPointPosition.x + _offset.x, endPointPosition.y + _offset.y, transform.position.z);
            transform.position = startPosition;

            // EndPoint에서 지정된 시간만큼 고정
            await UniTask.Delay(1000);

            // 플레이어 위치로 부드럽게 이동
            Vector3 targetPosition = new Vector3(playerPosition.x + _offset.x, playerPosition.y + _offset.y, transform.position.z);
            await transform.DOMove(targetPosition, _cinematicDuration)
                .SetEase(_cinematicEase)
                .AsyncWaitForCompletion();

            // 시네마틱 완료 후 일반 모드로 전환
            _isInCinematicMode = false;
        }

        private void FollowTarget()
        {
            // 원하는 카메라 위치를 계산
            Vector3 desiredPosition = new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                transform.position.z // 카메라의 z축 위치는 변경하지 않음
            );

            // 현재 카메라 위치에서 원하는 위치로 부드럽게 이동하는 위치를 계산
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed);

            // 계산된 위치로 카메라를 이동
            transform.position = smoothedPosition;
        }
    }
}
