using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class WorkdaySessionDriver : MonoBehaviour
{
    [SerializeField] private DayDefinition day;

    public WorkdaySession Session { get; private set; }

    private void Awake()
    {
        if (day == null)
        {
            Debug.LogError("Assign a day to the workday session driver.", this);
            enabled = false;
            return;
        }

        Session = new WorkdaySession(day.GetData());
    }

    private void Start()
    {
        Session.AdvanceRealSeconds(0);
    }

    private void Update()
    {
        Session.AdvanceRealSeconds(UnityEngine.Time.unscaledDeltaTime);
    }
}