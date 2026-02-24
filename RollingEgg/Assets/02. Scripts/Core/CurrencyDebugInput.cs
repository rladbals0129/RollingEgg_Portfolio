using UnityEngine;
using RollingEgg.Core;

namespace RollingEgg.Core
{
    /// <summary>
    /// 재화 디버그 입력 처리 클래스
    /// F1~F6 키로 각 재화 타입을 추가할 수 있는 테스트 기능
    /// </summary>
    public class CurrencyDebugInput : MonoBehaviour
    {
        private ICurrencyService _currencyService;
        
        // 재화 ID 상수 (CurrencyService와 동일하게 유지)
        private const int COMMON_CURRENCY_ID = 1; // 공용 재화 ID
        
        // 테스트용 추가량
        private const int TEST_CURRENCY_AMOUNT = 1000;

        private void Start()
        {
            // CurrencyService 주입
            _currencyService = ServiceLocator.Get<ICurrencyService>();
            
            if (_currencyService == null)
            {
                Debug.LogError("[CurrencyDebugInput] CurrencyService를 찾을 수 없습니다!");
                enabled = false;
                return;
            }

            Debug.Log("[CurrencyDebugInput] 재화 디버그 입력 시스템 활성화");
            Debug.Log("[CurrencyDebugInput] F1: 공용 재화 추가, F2-F6: 각 알 타입별 전용 재화 추가");
        }

        private void Update()
        {
            // F1: 공용 재화 (기본재화) 추가
            if (Input.GetKeyDown(KeyCode.F1))
            {
                AddCommonCurrency();
            }
            
            // F2: Blue 알 전용 재화 추가
            if (Input.GetKeyDown(KeyCode.F2))
            {
                AddSpecialCurrency("blue", 2);
            }
            
            // F3: Red 알 전용 재화 추가
            if (Input.GetKeyDown(KeyCode.F3))
            {
                AddSpecialCurrency("red", 3);
            }
            
            // F4: White 알 전용 재화 추가
            if (Input.GetKeyDown(KeyCode.F4))
            {
                AddSpecialCurrency("white", 4);
            }
            
            // F5: Black 알 전용 재화 추가
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AddSpecialCurrency("black", 5);
            }
            
            // F6: Yellow 알 전용 재화 추가
            if (Input.GetKeyDown(KeyCode.F6))
            {
                AddSpecialCurrency("yellow", 6);
            }
        }

        private void AddCommonCurrency()
        {
            int addedAmount = _currencyService.AddCurrency(COMMON_CURRENCY_ID, TEST_CURRENCY_AMOUNT, "debug_input");
            int currentAmount = _currencyService.GetCurrencyAmount(COMMON_CURRENCY_ID);
            
            Debug.Log($"[CurrencyDebugInput] 공용 재화 추가: +{addedAmount}, 현재 잔액: {currentAmount}");
        }

        private void AddSpecialCurrency(string eggType, int currencyId)
        {
            int addedAmount = _currencyService.AddCurrency(currencyId, TEST_CURRENCY_AMOUNT, "debug_input");
            int currentAmount = _currencyService.GetCurrencyAmount(currencyId);
            
            Debug.Log($"[CurrencyDebugInput] {eggType} 전용 재화 추가: +{addedAmount}, 현재 잔액: {currentAmount}");
        }
    }
}