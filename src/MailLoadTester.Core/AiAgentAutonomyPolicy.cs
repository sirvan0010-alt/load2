namespace MailLoadTester.Core;

public sealed record AiAgentAutonomyPolicy(
    int MaxIterations,
    TimeSpan TimeBudget,
    bool RequireAuthorizationForRealTarget = true,
    bool RequireIndependentVerification = true,
    bool AllowAutomaticMerge = false,
    bool AllowAutomaticRelease = false)
{
    public void Validate(AiAgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (MaxIterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(MaxIterations));

        if (TimeBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TimeBudget));

        if (task.MaxIterations > MaxIterations)
            throw new InvalidOperationException(
                $"Task MaxIterations ({task.MaxIterations}) exceeds autonomy policy limit ({MaxIterations}).");

        if (task.TimeBudget > TimeBudget)
            throw new InvalidOperationException(
                $"Task TimeBudget ({task.TimeBudget}) exceeds autonomy policy limit ({TimeBudget}).");

        if (RequireAuthorizationForRealTarget && task.RealTargetRequired && !task.Authorized)
            throw new InvalidOperationException("Real-target autonomous execution requires explicit authorization.");

        if (RequireIndependentVerification is false)
            throw new InvalidOperationException("Independent verification cannot be disabled by autonomy policy.");

        if (AllowAutomaticMerge)
            throw new InvalidOperationException("Automatic merge is disabled by policy.");

        if (AllowAutomaticRelease)
            throw new InvalidOperationException("Automatic release is disabled by policy.");
    }
}
