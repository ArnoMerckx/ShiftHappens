using System;
using System.Collections.Generic;

public sealed class SwarmsPresenter
{
    private readonly WorkdaySession session;

    public AppReadingState State { get; }

    public SwarmsPresenter(WorkdaySession session, AppReadingState state)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public List<SwarmsNoticeViewData> GetNotices()
    {
        var notices = new List<SwarmsNoticeViewData>();

        foreach (IncidentInstance incident in session.Deliveries.DeliveredIncidents)
        {
            var original = new IncidentNoticeViewData(incident);

            notices.Add(new SwarmsNoticeViewData(SwarmsNoticeKind.HrIncident, original.InstanceId, original.DisplayName, $"Report due: {original.DueTimeText}\n\n{original.BodyText}", $"Due {original.DueTimeText}"));
        }

        foreach (RequestOverviewViewData request in session.GetRequestOverview())
        {
            string timing;

            if (request.HelpWindow.HasValue)
            {
                TimeRange window = request.HelpWindow.Value;

                timing = $"Help window: {FormatMinute(window.StartMinute)}–" + $"{FormatMinute(window.EndMinute)}\n" + $"Arrange by: {FormatTime(request.SubmissionCutoff)}";
            }
            else
            {
                timing = "Arrange before shift end " + $"({FormatTime(request.SubmissionCutoff)}).";
            }

            string status = request.Status == RequestStatus.Resolved ? "Submitted" : request.Status.ToString();

            string body = $"From: {GetEmployeeName(request.Request.RequesterId)}\n" + $"{timing}\n" + $"Status: {status}\n\n" + $"{request.Request.Description}\n\n" + "View this request in Deleg8.";

            notices.Add(new SwarmsNoticeViewData(SwarmsNoticeKind.HelpRequest, request.InstanceId, request.Request.DisplayName, body, status + " · " + (request.HelpWindow.HasValue ? $"Arrange by {FormatTime(request.SubmissionCutoff)}" : "Before shift end")));
        }

        foreach (SwarmsReplyData reply in session.SwarmsReplies.DeliveredReplies)
        {
            string body = "From: " + reply.SenderName + "\n\n" + reply.BodyText;

            notices.Add(new SwarmsNoticeViewData(SwarmsNoticeKind.AuthoredReply, reply.ReplyId, reply.Subject, body, "Reply"));
        }

        return notices;
    }

    public SwarmsNoticeViewData GetSelectedNotice()
    {
        foreach (SwarmsNoticeViewData notice in GetNotices())
        {
            if (string.Equals(notice.NoticeId, State.SelectedItemId, StringComparison.Ordinal))
            {
                return notice;
            }
        }

        return null;
    }

    public bool TrySelectNotice(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        foreach (SwarmsNoticeViewData notice in GetNotices())
        {
            if (!string.Equals(notice.NoticeId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectItem(instanceId);
            return true;
        }

        return false;
    }

    private string GetEmployeeName(string employeeId)
    {
        foreach (EmployeeViewData employee in session.GetEmployeeRoster())
        {
            if (string.Equals(
                employee.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                return employee.DisplayName;
            }
        }

        return employeeId;
    }

    private static string FormatMinute(int minute)
    {
        return $"{minute / 60:00}:{minute % 60:00}";
    }

    private static string FormatTime(GameTime time)
    {
        string text = FormatMinute(time.MinuteOfDay);

        if (time.SecondOfMinute != 0 || time.MillisecondOfSecond != 0)
            text += $":{time.SecondOfMinute:00}";

        if (time.MillisecondOfSecond != 0)
            text += $".{time.MillisecondOfSecond:000}";

        return text;
    }
}