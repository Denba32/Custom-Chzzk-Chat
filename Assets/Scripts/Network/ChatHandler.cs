using Denba.Chzzk.Data;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Networks;
using UnityEngine;

namespace Denba.Chzzk.UI
{
    public class ChatHandler : MonoBehaviour
    {
        private ChzzkEventDispatcher _dispatcher;

        private void Start()
        {
            _dispatcher = ChzzkEventDispatcher.Instance;
            if (_dispatcher == null)
            {
                Debug.LogError("[ChatHandler] ChzzkEventDispatcher 없음");
                return;
            }

            _dispatcher.OnChat += HandleChat;
            _dispatcher.OnDonation += HandleDonation;
            _dispatcher.OnSubscription += HandleSubscription;
        }

        private void OnDestroy()
        {
            if (_dispatcher == null) return;

            _dispatcher.OnChat -= HandleChat;
            _dispatcher.OnDonation -= HandleDonation;
            _dispatcher.OnSubscription -= HandleSubscription;
        }

        private void HandleChat(ChzzkSocketClient.ChatEventData data)
        {
            string nickname = data.profile?.nickname;
            if (string.IsNullOrEmpty(nickname)) return;

            ViewerManager.Instance?.GetOrSpawn(nickname)?.ShowChat(data.content, data.emojis);
            ViewerLevelManager.Instance?.AddChatExp(nickname);
        }

        private void HandleDonation(ChzzkSocketClient.DonationEventData data)
        {
            string nickname = data.donatorNickname;
            if (string.IsNullOrEmpty(nickname)) return;

            int.TryParse(data.payAmount, out int amount);

            var character = ViewerManager.Instance?.GetOrSpawn(nickname);
            character?.TriggerEvent(CharacterState.Donation);
            character?.ShowChat($"💰 {amount:N0}원 후원!");

            ViewerLevelManager.Instance?.AddDonationExp(nickname, amount);
        }

        private void HandleSubscription(ChzzkSocketClient.SubscriptionEventData data)
        {
            string nickname = data.subscriberNickname;
            if (string.IsNullOrEmpty(nickname)) return;

            var character = ViewerManager.Instance?.GetOrSpawn(nickname);
            character?.TriggerEvent(CharacterState.Subscribe);
            character?.ShowChat($"🎉 {data.month}개월 구독 감사해요!");
        }
    }
}
