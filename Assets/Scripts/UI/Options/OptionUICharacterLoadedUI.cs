using Denba.Chzzk.Scripts.Utils;
using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUICharacterLoadedUI : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Button button;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        private Subject<OptionUICharacterLoadedUI> onDeleted = new();
        private Subject<OptionUICharacterLoadedUI> onPrev = new();
        private Subject<OptionUICharacterLoadedUI> onNext = new();

        public IObservable<OptionUICharacterLoadedUI> OnDeleted => onDeleted;
        public IObservable<OptionUICharacterLoadedUI> OnPrev => onPrev;
        public IObservable<OptionUICharacterLoadedUI> OnNext => onNext;

        public Sprite Image => image.sprite;

        public void Initialize()
        {
            button?.OnClickAsObservableFirst().Subscribe(_ => onDeleted?.OnNext(this)).AddTo(this);
            prevButton?.OnClickAsObservableFirst().Subscribe(_ => onPrev?.OnNext(this)).AddTo(this);
            nextButton?.OnClickAsObservableFirst().Subscribe(_ => onNext?.OnNext(this)).AddTo(this);
        }

        public void SetImage(Sprite sprite)
        {
            image.sprite = sprite;
        }
    }
}