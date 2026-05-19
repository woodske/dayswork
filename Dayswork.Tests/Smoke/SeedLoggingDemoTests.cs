using FsCheck.Xunit;

namespace Dayswork.Tests.Smoke;

public class SeedLoggingDemoTests
{
    // Remove the Skip attribute to confirm FsCheck.Xunit prints the seed (StdGen ...) and the
    // shrunk minimal failing input on failure. This is PBT-08 satisfied by default — no custom
    // seed-logging plumbing is required. To replay a known failure: [Property(Replay = "(seed1, seed2)")].
    // Math.Abs(x) < 0 is always false (absolute value is never negative); shrinks to x=0.
    [Property(Skip = "demonstrates PBT-08 seed logging — enable to see output")]
    public bool PBT08_seed_and_shrunk_input_logged_on_failure(int x)
    {
        return Math.Abs(x) < 0;
    }
}
