using UnityEngine;
// Pixel Perfect Camera 네임스페이스 추가 (필요시)
// using UnityEngine.U2D; 

namespace RollingEgg.Lobby // 네임스페이스 정리 추천에 따라 적용
{
    public class LobbyCamera : MonoBehaviour
    {
        [Header("Targets")]
        public Transform target;           // 따라갈 캐릭터
        public BoxCollider2D mapBoundary;  // 맵의 전체 영역

        private Camera cam;
        private float camHalfHeight;
        private float camHalfWidth;

        void Start()
        {
            cam = GetComponent<Camera>();

            if (mapBoundary == null)
            {
                Debug.LogError("Map Boundary가 할당되지 않았습니다!");
                return;
            }

            // Pixel Perfect Camera가 설정한 orthographicSize를 사용하여 카메라 크기 계산
            UpdateCameraSize();
        }

        void LateUpdate()
        {
            if (target == null || mapBoundary == null) return;

            // Pixel Perfect Camera가 런타임에 orthographicSize를 변경할 수 있으므로 매 프레임 갱신
            UpdateCameraSize();

            // 1. 타겟 따라가기
            Vector3 desiredPosition = target.position;
            desiredPosition.z = transform.position.z;

            // 2. 맵 밖으로 나가지 않게 가두기 (Clamping)
            Bounds bounds = mapBoundary.bounds;

            float minX = bounds.min.x + camHalfWidth;
            float maxX = bounds.max.x - camHalfWidth;

            float minY = bounds.min.y + camHalfHeight;
            float maxY = bounds.max.y - camHalfHeight;

            // 맵이 카메라보다 작을 경우 중앙 고정
            if (minX > maxX)
            {
                float mid = (bounds.min.x + bounds.max.x) / 2f;
                minX = mid;
                maxX = mid;
            }
            if (minY > maxY)
            {
                float mid = (bounds.min.y + bounds.max.y) / 2f;
                minY = mid;
                maxY = mid;
            }

            float clampedX = Mathf.Clamp(desiredPosition.x, minX, maxX);
            float clampedY = Mathf.Clamp(desiredPosition.y, minY, maxY);

            transform.position = new Vector3(clampedX, clampedY, desiredPosition.z);
        }

        /// <summary>
        /// Pixel Perfect Camera가 설정한 orthographicSize를 기반으로 카메라 반너비/반높이를 계산합니다.
        /// 맵 바운더리를 기준으로 클램핑에 사용됩니다.
        /// </summary>
        private void UpdateCameraSize()
        {
            // Pixel Perfect Camera가 설정한 orthographicSize를 사용 (직접 수정하지 않음)
            camHalfHeight = cam.orthographicSize;
            camHalfWidth = camHalfHeight * cam.aspect;
        }
    }
}
