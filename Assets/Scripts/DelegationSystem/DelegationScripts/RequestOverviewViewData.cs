using System;

public sealed class RequestOverviewViewData
{
    public string InstanceId { get; }
    public RequestData Request { get; }
    public GameTime DeliveryAt { get; }
    public TimeRange? HelpWindow { get; }
    public GameTime SubmissionCutoff { get; }
    public RequestStatus Status { get; }
    public string HelperId { get; }
    public string Explanation { get; }

    public RequestOverviewViewData(RequestInstance instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        InstanceId = instance.InstanceId;
        Request = instance.Request;
        DeliveryAt = instance.DeliveryAt;
        HelpWindow = instance.HelpWindow;
        SubmissionCutoff = instance.CutoffAt ?? instance.ExpiresAt;
        Status = instance.Status;
        HelperId = instance.Decision?.HelperId ?? string.Empty;
        Explanation = instance.Decision?.Explanation ?? string.Empty;
    }
}