namespace TiffanyGame
{
    public enum InteractionKind { None, Touch, Collect, Hide, Leave, Kindle, Recharge }

    public sealed class InteractionPrompt
    {
        public object Target { get; private set; }
        public float DistanceSquared { get; private set; } = float.PositiveInfinity;
        public InteractionKind Kind { get; private set; }
        public string Text => Kind switch
        {
            InteractionKind.Touch => InterfaceTexts.TouchPrompt,
            InteractionKind.Collect => InterfaceTexts.CollectPrompt,
            InteractionKind.Hide => InterfaceTexts.HidePrompt,
            InteractionKind.Leave => InterfaceTexts.LeavePrompt,
            InteractionKind.Kindle => InterfaceTexts.KindlePrompt,
            InteractionKind.Recharge => InterfaceTexts.RechargePrompt,
            _ => string.Empty
        };

        public void Clear()
        {
            Target = null;
            Kind = InteractionKind.None;
            DistanceSquared = float.PositiveInfinity;
        }

        public void Consider(object target, float distanceSquared, InteractionKind kind, bool available = true)
        {
            if (!available || target == null || kind == InteractionKind.None
                || !(distanceSquared >= 0f && distanceSquared < DistanceSquared)) return;
            Target = target;
            Kind = kind;
            DistanceSquared = distanceSquared;
        }
    }
}
