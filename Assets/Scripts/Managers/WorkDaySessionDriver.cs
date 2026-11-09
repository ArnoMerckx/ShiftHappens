using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class WorkdaySessionDriver : MonoBehaviour
{
    [SerializeField] private DayDefinition day;

    public WorkdaySession Session { get; private set; }

    public bool HasBegunShift { get; private set; }

    private void Awake()
    {
        if (day == null)
        {
            Debug.LogError("Assign a day to the workday session driver.", this);
            enabled = false;
            return;
        }

        Session = new WorkdaySession(day.GetData());
        Session.SetPaused(true);
    }

    public bool TryBeginShift()
    {
        if (Session == null || HasBegunShift)
            return false;

        HasBegunShift = true;
        Session.SetPaused(false);
        Session.AdvanceRealSeconds(0);

        return true;
    }

    private void Update()
    {
        if (Session == null || !HasBegunShift)
            return;

        Session.AdvanceRealSeconds(UnityEngine.Time.unscaledDeltaTime);
    }
}