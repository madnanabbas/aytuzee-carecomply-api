using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientCaregiverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNormalizesAssignmentAndStartsActive()
    {
        var value = Create();
        Assert.Equal(ClientCaregiverStatus.Active, value.Status);
        Assert.Equal("PCA, CFSS", value.Services);
    }

    [Fact]
    public void EndPreservesStatusWhenHistoricalDetailsAreCorrected()
    {
        var value = Create(); value.End(new DateOnly(2026, 9, 4), Now.AddMinutes(1));
        Assert.Equal(ClientCaregiverStatus.Ended, value.Status);
        value.Update(ClientCaregiverType.Backup, false, "CFSS",
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), "Corrected", Now.AddMinutes(2));
        Assert.Equal(ClientCaregiverStatus.Ended, value.Status);
        Assert.Equal(ClientCaregiverType.Backup, value.Type);
    }

    [Fact]
    public void RejectsEndDateBeforeStartDate() => Assert.Throws<ArgumentException>(() => ClientCaregiver.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ClientCaregiverType.Primary, true, null,
        new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 9), null, Guid.NewGuid(), Now));

    [Fact]
    public void ScheduledFutureEndDateDoesNotImmediatelyEndAssignment()
    {
        var value = ClientCaregiver.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ClientCaregiverType.Primary, true, "PCA", new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30), null, Guid.NewGuid(), Now);

        Assert.Equal(ClientCaregiverStatus.Active, value.Status);
    }

    [Fact]
    public void ExplicitEndRejectsFutureDate()
    {
        var value = Create();

        Assert.Throws<ArgumentException>(() => value.End(new DateOnly(2026, 9, 30), Now));
    }

    private static ClientCaregiver Create() => ClientCaregiver.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        ClientCaregiverType.Primary, true, " PCA, CFSS ", new DateOnly(2026, 9, 1), null, " Notes ", Guid.NewGuid(), Now);
}
