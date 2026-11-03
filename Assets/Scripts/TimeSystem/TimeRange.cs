using System;
    public readonly struct TimeRange
    {
        public int DayIndex { get; }
        public int StartMinute { get; }
        public int EndMinute { get; }

        public TimeRange(int dayIndex, int startMinute, int endMinute)
        {
            if (dayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(dayIndex));

            if (startMinute < 0 || startMinute >= 1440)
                throw new ArgumentOutOfRangeException(nameof(startMinute));

            if (endMinute < 0 || endMinute > 1440)
                throw new ArgumentOutOfRangeException(nameof(endMinute));

            if (endMinute <= startMinute)
                throw new ArgumentException("End must be later than start.", nameof(endMinute));

            DayIndex = dayIndex;
            StartMinute = startMinute;
            EndMinute = endMinute;
        }

        public bool Contains(int dayIndex, int minuteOfDay)
        {
            return StartMinute < EndMinute && DayIndex == dayIndex && StartMinute <= minuteOfDay && minuteOfDay < EndMinute;
        }

        public bool Overlaps(TimeRange other)
        {
            return StartMinute < EndMinute && other.StartMinute < other.EndMinute && DayIndex == other.DayIndex && StartMinute < other.EndMinute && other.StartMinute < EndMinute;
        }
    }