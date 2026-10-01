using Denba.Chzzk.Data;
using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.Utils
{
    public static class Extensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
            => Util.GetOrAddComponent<T>(go);

        public static string GetName(this CharacterState state)
        {
            return state switch
            {
                CharacterState.Spawn => "등장",
                CharacterState.Idle1 => "대기 모션1",
                CharacterState.Idle2 => "대기 모션2",
                CharacterState.Idle3 => "대기 모션3",
                CharacterState.Move => "이동",
                CharacterState.Donation => "도네이션",
                CharacterState.Subscribe => "구독",
                CharacterState.Drag => "드래그",
                CharacterState.Land => "착지",
                CharacterState.LevelUp => "레벨업",
                _ => string.Empty
            };
        }

        /// <summary>알파·레이캐스트·상호작용을 한 번에 켜고 끈다.</summary>
        public static void SetVisible(this CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        /// <summary>연속 클릭을 막기 위해 첫 클릭 이후 일정 시간 동안 입력을 무시한다.</summary>
        public static IObservable<Unit> OnClickAsObservableFirst(this Button button, float throttleSeconds = 0.5f)
            => button.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(throttleSeconds));

        /// <summary>간격 안에 들어온 클릭은 걸러내고 onWaitAction을 호출한다.</summary>
        public static IObservable<Unit> OnClickAsObservableWithThrottle(
            this Button button, float intervalSeconds, Action onWaitAction = null)
        {
            var interval = TimeSpan.FromSeconds(intervalSeconds);
            var lastClickTime = DateTimeOffset.MinValue;

            return button.OnClickAsObservable()
                .Where(_ =>
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now - lastClickTime >= interval)
                    {
                        lastClickTime = now;
                        return true;
                    }

                    onWaitAction?.Invoke();
                    return false;
                });
        }
    }
}
