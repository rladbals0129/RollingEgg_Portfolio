using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace RollingEgg.Core
{
    /// <summary>
    /// 도감 관리 서비스 인터페이스
    /// 진화체 도감 등록, 중복 처리, 버프 적용 기능을 제공
    /// </summary>
    public interface ICollectionService
    {
        /// <summary>
        /// 서비스 초기화
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// 도감 등록
        /// </summary>
        /// <param name="formId">진화체 ID</param>
        /// <returns>등록 성공 여부 (중복 시 false)</returns>
        bool RegisterEvolutionForm(int formId);

        /// <summary>
        /// 도감 조회 - 특정 진화체가 등록되어 있는지 확인
        /// </summary>
        /// <param name="formId">진화체 ID</param>
        /// <returns>등록 여부</returns>
        bool IsFormRegistered(int formId);

        /// <summary>
        /// 등록된 진화체 목록 조회
        /// </summary>
        /// <param name="eggType">알 타입 필터 (null이면 전체)</param>
        /// <returns>등록된 진화체 ID 목록</returns>
        List<int> GetRegisteredForms(string eggType = null);

        /// <summary>
        /// 진화체 정보 조회
        /// </summary>
        /// <param name="formId">진화체 ID</param>
        /// <returns>진화체 정보</returns>
        EvolutionForm GetFormInfo(int formId);

        /// <summary>
        /// 중복 진화체 처리 (재화 지급)
        /// </summary>
        /// <param name="formId">진화체 ID</param>
        /// <returns>지급된 재화량</returns>
        int ProcessDuplicateForm(int formId);

        /// <summary>
        /// 도감 버프 적용
        /// </summary>
        void ApplyCollectionBuffs();

        /// <summary>
        /// 공용 재화 버프(%) 조회
        /// </summary>
        float GetCommonCurrencyBuffPercent();

        /// <summary>
        /// 알 타입별 전용 재화 버프(%) 조회
        /// </summary>
        /// <param name="eggType">알 타입 문자열</param>
        /// <returns>버프 수치(%)</returns>
        float GetSpecialCurrencyBuffPercent(string eggType);

        /// <summary>
        /// 중복 진화체 보상 버프(%) 조회
        /// </summary>
        float GetDuplicateRewardBuffPercent();

        /// <summary>
        /// 데이터 저장
        /// </summary>
        UniTask SaveDataAsync();

        /// <summary>
        /// 데이터 로드
        /// </summary>
        UniTask LoadDataAsync();
    }
}
