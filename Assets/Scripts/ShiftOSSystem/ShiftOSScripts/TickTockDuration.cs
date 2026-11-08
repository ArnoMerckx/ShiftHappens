using System;
using System.Globalization;

public static class TickTockDuration
{
    public static bool TryParseDuration(string minutesText, string secondsText, out int durationSeconds, out string error)
    {
        durationSeconds = 0;
        error = string.Empty;

        if (!int.TryParse(minutesText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int minutes))
        {
            error = "Enter minutes as a non-negative whole number.";
            return false;
        }

        if (!int.TryParse(secondsText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds > 59)
        {
            error = "Enter seconds as a whole number from 0 to 59.";
            return false;
        }

        long total = (long)minutes * 60 + seconds;

        if (total == 0)
        {
            error = "Enter a duration greater than zero.";
            return false;
        }

        if (total > int.MaxValue)
        {
            error = "That duration is too large.";
            return false;
        }

        durationSeconds = (int)total;
        return true;
    }

    public static string FormatRemaining(long milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));

        long seconds = milliseconds / GameTime.MillisecondsPerSecond;

        if (milliseconds % GameTime.MillisecondsPerSecond != 0)
            seconds++;

        long minutes = seconds / 60;
        long secondsPart = seconds % 60;

        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + secondsPart.ToString("00", CultureInfo.InvariantCulture);
    }
}