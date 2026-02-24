using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Util
{
    public static class KeyCodeUtil
    {
        // KeyCode와 표시 문자열을 매핑하는 사전 (한 번만 초기화됨)
        private static readonly Dictionary<KeyCode, string> _keyDisplayMap = new Dictionary<KeyCode, string>
        {
            // 방향키
            { KeyCode.UpArrow, "↑" },
            { KeyCode.DownArrow, "↓" },
            { KeyCode.LeftArrow, "←" },
            { KeyCode.RightArrow, "→" },

            // 필요한 다른 키패드 키 추가
        };

        public static string GetDisplayString(KeyCode keyCode)
        {
            // 맵에 정의된 특수키인 경우 해당 문자열 반환
            if (_keyDisplayMap.TryGetValue(keyCode, out string displayString))
            {
                return displayString;
            }

            return keyCode.ToString();
        }

        public static KeyCode[] BuildAllowedKeys()
        {
            var list = new List<KeyCode>();

            // 알파벳
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++) list.Add(k);
            // 숫자(상단)
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) list.Add(k);
            // 기능키
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++) list.Add(k);
            // 스페이스/시프트/컨트롤/알트/탭/백스페이스/엔터
            list.AddRange(new[]
            {
                KeyCode.Space, KeyCode.LeftShift, KeyCode.RightShift,
                KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt,
                KeyCode.Tab, KeyCode.Backspace, KeyCode.Return
            });
            // 방향키
            list.AddRange(new[]
            {
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow
            });

            return list.ToArray();
        }
    }
}
