using System.Collections.Generic;
using DTOs;
using TMPro;
using UnityEngine;

namespace UI
{
    //This is a practical project so it doesn't need perfect and clean structure for client
    //Main focus is to learn backend and communicating with it in client
    public class MainMenuManager : MonoBehaviour
    {
        private static MainMenuManager _instance;

        public static MainMenuManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MainMenuManager>();
                }
                return _instance;
            }
        }
        
        [Header("References"), Space]
        [SerializeField] private TextMeshProUGUI _statusTMP;
        
        [Header("Enter match UI"), Space]
        [SerializeField] private EnterMatchButton _enterMatchButton;
        [SerializeField] private Transform _matchContainer;
        private Dictionary<int ,EnterMatchButton> _enterMatchButtons = new();

        public void SetStatusText(string text)
        {
            _statusTMP.text = text;
        }

        public void SetupMatchUI(List<MatchData> matchData)
        {
            foreach (var pair in _enterMatchButtons)
            {
                Destroy(pair.Value.gameObject);
                _enterMatchButtons.Remove(pair.Key);
            }
            
            foreach (var data in matchData)
            {
                var button = Instantiate(_enterMatchButton, _matchContainer);
                button.Setup(data,() => EnterMatchAction(data));
            }
        }

        private void EnterMatchAction(MatchData matchData)
        {
            //TODO : Handle match making logic here
        }
    }
}