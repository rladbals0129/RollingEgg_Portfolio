using UnityEngine;

namespace RollingEgg.Util
{
    /// <summary>
    /// 애니메이션 관련 유틸리티 클래스
    /// </summary>
    public static class AnimationUtil
    {
        /// <summary>
        /// Animator에 랜덤 속도를 적용합니다.
        /// </summary>
        /// <param name="animator">대상 Animator</param>
        /// <param name="minSpeed">최소 속도</param>
        /// <param name="maxSpeed">최대 속도</param>
        public static void ApplyRandomSpeed(Animator animator, float minSpeed = 0.8f, float maxSpeed = 1.2f)
        {
            if (animator == null)
                return;

            float randomSpeed = Random.Range(minSpeed, maxSpeed);
            animator.speed = randomSpeed;
        }

        /// <summary>
        /// GameObject에서 Animator를 찾아 랜덤 속도를 적용합니다.
        /// </summary>
        /// <param name="gameObject">대상 GameObject</param>
        /// <param name="minSpeed">최소 속도</param>
        /// <param name="maxSpeed">최대 속도</param>
        /// <param name="includeChildren">자식 오브젝트도 검색할지 여부</param>
        public static void ApplyRandomSpeedToGameObject(GameObject gameObject, float minSpeed = 0.8f, float maxSpeed = 1.2f, bool includeChildren = true)
        {
            if (gameObject == null)
                return;

            Animator animator = includeChildren 
                ? gameObject.GetComponentInChildren<Animator>() 
                : gameObject.GetComponent<Animator>();

            if (animator != null)
            {
                ApplyRandomSpeed(animator, minSpeed, maxSpeed);
            }
        }
    }
}

