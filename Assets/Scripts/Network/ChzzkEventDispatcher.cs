using System;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;

namespace Denba.Chzzk.Networks
{
    public class ChzzkEventDispatcher : SingletonMono<ChzzkEventDispatcher>
    {
        public event Action<ChzzkSocketClient.ChatEventData> OnChat;
        public event Action<ChzzkSocketClient.DonationEventData> OnDonation;
        public event Action<ChzzkSocketClient.SubscriptionEventData> OnSubscription;

        public override UniTask Initialize() => UniTask.CompletedTask;

        public void OnChatReceived(ChzzkSocketClient.ChatEventData data)
        {
            if (data != null) OnChat?.Invoke(data);
        }

        public void OnDonationReceived(ChzzkSocketClient.DonationEventData data)
        {
            if (data != null) OnDonation?.Invoke(data);
        }

        public void OnSubscriptionReceived(ChzzkSocketClient.SubscriptionEventData data)
        {
            if (data != null) OnSubscription?.Invoke(data);
        }
    }
}
