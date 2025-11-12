using System;
using DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class EnterMatchButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _matchNameTMP;
        [SerializeField] private TextMeshProUGUI _entryCostTMP;
        [SerializeField] private TextMeshProUGUI _prizeAmountTMP;
        
        public void Setup(MatchData matchData, Action onClickAction)
        {
            _matchNameTMP.text = matchData.MatchName;
            _entryCostTMP.text = $"Entry : {matchData.Entry}";
            _prizeAmountTMP.text = $"Prize : {matchData.Prize}";

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClickAction?.Invoke());
            
            SetIntractable(true);
        }
        
        public void SetIntractable(bool intractable)
        {
            _button.interactable = intractable;
        }
    }
}