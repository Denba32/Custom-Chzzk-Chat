using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUIMenuTab : MonoBehaviour
    {
        [SerializeField] private Toggle tabToggle;
        public IObservable<bool> OnToggleValueChangedAsObservable() => tabToggle.OnValueChangedAsObservable();
    }
}
