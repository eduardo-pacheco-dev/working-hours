namespace WorkingHours.Core.Models;

/// <summary>The three phases of a Pomodoro cycle.</summary>
public enum RoundType
{
    Work,
    ShortBreak,
    LongBreak
}

public static class RoundTypeExtensions
{
    /// <summary>Stable identifier also used by the theme files and the WebSocket API.</summary>
    public static string ToId(this RoundType round) => round switch
    {
        RoundType.Work => "work",
        RoundType.ShortBreak => "short-break",
        RoundType.LongBreak => "long-break",
        _ => "work"
    };
}
