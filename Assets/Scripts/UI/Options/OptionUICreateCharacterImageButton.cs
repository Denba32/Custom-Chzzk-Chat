using Denba.Chzzk.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUICreateCharacterImageButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        public IObservable<Unit> OnClickAsObservable => button.OnClickAsObservableFirst();
    }
}