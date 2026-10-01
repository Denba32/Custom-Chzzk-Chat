using Denba.Chzzk.Data;
using Denba.Chzzk.Scripts.Utils;
using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class StateTabItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemText;
        [SerializeField] private Button button;

        private CharacterState state;
        public CharacterState State => state;

        private Subject<StateTabItem> onSelect = new();
        public IObservable<StateTabItem> OnSelect => onSelect;
        public void Initialize(CharacterState state)
        {
            this.state = state;
            itemText.text = state.GetName();
            button.OnClickAsObservable().Subscribe(_ => onSelect?.OnNext(this)).AddTo(this);
        }
    }
}