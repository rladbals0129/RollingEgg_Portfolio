using TMPro;
using UnityEngine;

namespace RollingEgg
{
    public class JudgmentScoreText : MonoBehaviour
    {
        [Header("## 데이터")]
        [SerializeField] private EJudgmentType _type;
        [SerializeField] private int _baseScore;

        [Header("## Text Components")]
        [SerializeField] private TMP_Text _typeText;
        [SerializeField] private TMP_Text _baseScoreText;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private TMP_Text _totalScoreText;

        public EJudgmentType Type => _type;
        public int BaseScore => _baseScore;

        public void Initialize()
        {
            _typeText.text = _type.ToString();
            _baseScoreText.text = _baseScore.ToString();
        }

        public void SetJudgmentText(int count, int score)
        {
            _countText.text = $"x {count}";
            _totalScoreText.text = $"{score}";
        }
    }
}
