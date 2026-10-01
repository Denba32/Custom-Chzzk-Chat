using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUICharacterButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private TMP_Text characterNameText;

        private Subject<OptionUICharacterButton> onSelect = new();
        private Subject<OptionUICharacterButton> onClickDelete = new();
        public IObservable<OptionUICharacterButton> OnClickAsObservable() => onSelect;
        public IObservable<OptionUICharacterButton> OnDeleteAsObservable() => onClickDelete;
        public string CharacterName => characterNameText.text;
        public void Initialize()
        {
            _button?.OnClickAsObservable().Subscribe(_ => onSelect?.OnNext(this)).AddTo(this);
            _deleteButton?.OnClickAsObservable().Subscribe(_ => onClickDelete?.OnNext(this)).AddTo(this);
        }

        public void SetCharacterName(string characterName) => characterNameText.text = characterName;
    }
}