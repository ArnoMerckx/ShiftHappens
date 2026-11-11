using System;

public sealed class DayRequestPlan
{
    public string InstanceId { get; }
    public RequestData Request { get; }
    public RequestProfileData Profile { get; }
    public GameTime DeliveryAt { get; }
    public TimeRange? HelpWindow { get; }
    public GameTime? CutoffAt { get; }

    public bool IsFixedWindow => HelpWindow.HasValue;

    public DayRequestPlan(string instanceId, RequestData request, RequestProfileData profile, GameTime deliveryAt, TimeRange? helpWindow = null, GameTime? cutoffAt = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Request instance ID is required.", nameof(instanceId));

        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        if (request.Category != profile.RequiredCapability)
            throw new ArgumentException("The request category must match its profile.", nameof(profile));

        if (helpWindow.HasValue)
        {
            TimeRange window = helpWindow.Value;

            if (window.StartMinute >= window.EndMinute || window.DayIndex != deliveryAt.DayIndex)
            {
                throw new ArgumentException("The help window must be nonempty and on the delivery day.",nameof(helpWindow));
            }

            var windowStart = new GameTime(window.DayIndex, window.StartMinute);

            if (!cutoffAt.HasValue)
                cutoffAt = windowStart;

            GameTime cutoff = cutoffAt.Value;

            if (cutoff.DayIndex != deliveryAt.DayIndex || cutoff.TotalMilliseconds < deliveryAt.TotalMilliseconds || cutoff.TotalMilliseconds > windowStart.TotalMilliseconds)
            {
                throw new ArgumentException("The cutoff must be between delivery and window start.", nameof(cutoffAt));
            }
        }
        else if (cutoffAt.HasValue)
        {
            throw new ArgumentException("End-of-shift requests cannot have a separate cutoff.", nameof(cutoffAt));
        }

        InstanceId = instanceId;
        Request = request;
        Profile = profile;
        DeliveryAt = deliveryAt;
        HelpWindow = helpWindow;
        CutoffAt = cutoffAt;
    }
}