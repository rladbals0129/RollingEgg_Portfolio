using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RollingEgg.Util
{
    public static class PathColorUtil
    {
        /// <summary>
        /// 챕터별 색상 스킴 정보
        /// </summary>
        private class ChapterColorScheme
        {
            public EColorType MainColor { get; set; }
            public List<EColorType> SubColors { get; set; }
        }

        private static readonly Dictionary<EColorType, Color> _pathColorMap = new Dictionary<EColorType, Color>
        {
            { EColorType.None, Color.gray },

            { EColorType.Yellow, Color.yellow },
            { EColorType.Blue, Color.blue },
            { EColorType.Red, Color.red },
            { EColorType.White, Color.white },
            { EColorType.Black, Color.black },

            { EColorType.Orange, new Color(1f, 0.5f, 0f) },
            { EColorType.Green, Color.green },
            { EColorType.Purple, new Color(0.6f, 0.2f, 0.8f) },
            { EColorType.Pink, new Color(1f, 0.6f, 0.8f) },
            { EColorType.Brown, new Color(0.6f, 0.4f, 0.2f) },
            { EColorType.Cyan, Color.cyan },
            { EColorType.Magenta, Color.magenta },
            { EColorType.Lime, new Color(0.75f, 1f, 0f) },
        };

        private static readonly List<EColorType> _randomColorCandidates =
            System.Enum.GetValues(typeof(EColorType))
            .Cast<EColorType>()
            .Where(color => color != EColorType.None)
            .ToList();

        /// <summary>
        /// 챕터별 메인 색상과 서브 색상 매핑
        /// </summary>
        private static readonly Dictionary<int, ChapterColorScheme> _chapterColorSchemes = new Dictionary<int, ChapterColorScheme>
        {
            // Chapter 1 = Blue
            {
                1, new ChapterColorScheme
                {
                    MainColor = EColorType.Blue,
                    SubColors = new List<EColorType> { EColorType.Orange, EColorType.Green, EColorType.Purple, EColorType.Pink, EColorType.Brown, EColorType.Cyan, EColorType.Magenta, EColorType.Lime }
                }
            },
            // Chapter 2 = Red
            {
                2, new ChapterColorScheme
                {
                    MainColor = EColorType.Red,
                    SubColors = new List<EColorType> { EColorType.Orange, EColorType.Green, EColorType.Purple, EColorType.Pink, EColorType.Brown, EColorType.Cyan, EColorType.Magenta, EColorType.Lime }
                }
            },
            // Chapter 3 = White
            {
                3, new ChapterColorScheme
                {
                    MainColor = EColorType.White,
                    SubColors = new List<EColorType> { EColorType.Orange, EColorType.Green, EColorType.Purple, EColorType.Pink, EColorType.Brown, EColorType.Cyan, EColorType.Magenta, EColorType.Lime }
                }
            },
            // Chapter 4 = Black
            {
                4, new ChapterColorScheme
                {
                    MainColor = EColorType.Black,
                    SubColors = new List<EColorType> { EColorType.Orange, EColorType.Green, EColorType.Purple, EColorType.Pink, EColorType.Brown, EColorType.Cyan, EColorType.Magenta, EColorType.Lime }
                }
            },
            // Chapter 5 = Yellow
            {
                5, new ChapterColorScheme
                {
                    MainColor = EColorType.Yellow,
                    SubColors = new List<EColorType> { EColorType.Orange, EColorType.Green, EColorType.Purple, EColorType.Pink, EColorType.Brown, EColorType.Cyan, EColorType.Magenta, EColorType.Lime }
                }
            },
        };

        /// <summary>
        /// eggId(챕터 ID)에 해당하는 메인 색상을 반환합니다.
        /// </summary>
        public static EColorType GetMainColorByEggId(int eggId)
        {
            if (_chapterColorSchemes.TryGetValue(eggId, out ChapterColorScheme scheme))
            {
                return scheme.MainColor;
            }

            Debug.LogWarning($"[PathColorUtil] eggId {eggId}에 해당하는 색상 스킴을 찾을 수 없습니다. 기본값 Blue를 반환합니다.");
            return EColorType.Blue; // 기본값
        }

        /// <summary>
        /// eggId(챕터 ID)에 해당하는 서브 색상 리스트를 반환합니다.
        /// </summary>
        public static List<EColorType> GetSubColorsByEggId(int eggId)
        {
            if (_chapterColorSchemes.TryGetValue(eggId, out ChapterColorScheme scheme))
            {
                return new List<EColorType>(scheme.SubColors);
            }

            Debug.LogWarning($"[PathColorUtil] eggId {eggId}에 해당하는 색상 스킴을 찾을 수 없습니다. 빈 리스트를 반환합니다.");
            return new List<EColorType>();
        }

        /// <summary>
        /// eggId(챕터 ID)와 count를 전달받아 해당하는 서브 색상을 랜덤하게 count 개수만큼 반환합니다.
        /// </summary>
        public static List<EColorType> GetRandomSubColorsByEggId(int eggId, int count)
        {
            if (count <= 0)
            {
                Debug.LogWarning($"[PathColorUtil] count는 1 이상이어야 합니다. (현재: {count})");
                return new List<EColorType>();
            }

            if (!_chapterColorSchemes.TryGetValue(eggId, out ChapterColorScheme scheme))
            {
                Debug.LogWarning($"[PathColorUtil] eggId {eggId}에 해당하는 색상 스킴을 찾을 수 없습니다.");
                return new List<EColorType>();
            }

            if (scheme.SubColors == null || scheme.SubColors.Count == 0)
            {
                Debug.LogWarning($"[PathColorUtil] eggId {eggId}의 서브 색상이 없습니다.");
                return new List<EColorType>();
            }

            List<EColorType> result = new List<EColorType>();
            List<EColorType> availableSubColors = new List<EColorType>(scheme.SubColors);

            // 요청한 개수가 사용 가능한 서브 색상 개수보다 많으면 모든 서브 색상 반환
            int takeCount = Mathf.Min(count, availableSubColors.Count);

            // 중복 없이 랜덤하게 선택
            for (int i = 0; i < takeCount; i++)
            {
                if (availableSubColors.Count == 0)
                    break;

                int randomIndex = Random.Range(0, availableSubColors.Count);
                EColorType selectedColor = availableSubColors[randomIndex];
                result.Add(selectedColor);

                // 선택한 색상을 리스트에서 제거하여 중복 방지
                availableSubColors.RemoveAt(randomIndex);
            }

            return result;
        }

        public static Color GetColorFromPathColor(EColorType pathColor)
        {
            return _pathColorMap.TryGetValue(pathColor, out var color) ? color : Color.gray;
        }

        public static EColorType GetRandomPathColor()
        {
            int randomIndex = UnityEngine.Random.Range(0, _randomColorCandidates.Count);
            return _randomColorCandidates[randomIndex];
        }

        public static Color GetColorFromKeyType(EColorKeyType pathType)
        {
            return pathType switch
            {
                EColorKeyType.S => Color.red,
                EColorKeyType.D => Color.green,
                EColorKeyType.F => Color.blue,
                EColorKeyType.J => Color.yellow,
                EColorKeyType.K => Color.magenta,
                EColorKeyType.L => Color.cyan,
                EColorKeyType.None => Color.gray,
                _ => Color.gray
            };
        }

        public static EColorType GetPathColorFromPathType(EPathType pathType)
        {
            return pathType switch
            {
                EPathType.L1 => EColorType.Red,
                EPathType.L2 => EColorType.Green,
                EPathType.L3 => EColorType.Blue,
                EPathType.L4 => EColorType.Yellow,
                EPathType.L5 => EColorType.Magenta,
                EPathType.None => EColorType.None,
                _ => EColorType.None
            };
        }

    }
}
