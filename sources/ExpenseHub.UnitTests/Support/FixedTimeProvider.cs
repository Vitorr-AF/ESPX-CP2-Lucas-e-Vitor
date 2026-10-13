using System;

namespace ExpenseHub.UnitTests.Support;

/// <summary>Clock that returns a fixed instant so tests do not depend on the machine time.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan amount)
    {
        _now = _now.Add(amount);
    }
}
