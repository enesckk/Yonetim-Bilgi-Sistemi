using PersonelYonetim.Application.Common.Security;
using PersonelYonetim.Domain.Security;

namespace PersonelYonetim.Tests;

public class LoginAttemptGateTests
{
    [Fact]
    public void Blocks_after_max_attempts_in_window()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var gate = new LoginAttemptGate(3, TimeSpan.FromMinutes(15), () => now);

        Assert.True(gate.TryRecord("10.0.0.1"));
        Assert.True(gate.TryRecord("10.0.0.1"));
        Assert.True(gate.TryRecord("10.0.0.1"));
        Assert.False(gate.TryRecord("10.0.0.1"));
        Assert.True(gate.TryRecord("10.0.0.2"));
    }

    [Fact]
    public void Allows_again_after_window()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var gate = new LoginAttemptGate(1, TimeSpan.FromMinutes(15), () => now);

        Assert.True(gate.TryRecord("1.1.1.1"));
        Assert.False(gate.TryRecord("1.1.1.1"));

        now = now.AddMinutes(16);
        Assert.True(gate.TryRecord("1.1.1.1"));
    }
}

public class WellKnownSecretsTests
{
    [Fact]
    public void Detects_development_password()
    {
        Assert.True(WellKnownSecrets.IsDevelopmentPassword("ChangeMe!123"));
        Assert.True(WellKnownSecrets.IsDevelopmentPassword(" ChangeMe!123 "));
        Assert.False(WellKnownSecrets.IsDevelopmentPassword("Other!Pass"));
        Assert.False(WellKnownSecrets.IsDevelopmentPassword(null));
    }
}
