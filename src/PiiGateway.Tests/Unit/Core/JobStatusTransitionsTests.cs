using FluentAssertions;
using PiiGateway.Core.Domain;
using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Tests.Unit.Core;

public class JobStatusTransitionsTests
{
    [Theory]
    [InlineData(JobStatus.Created, JobStatus.Processing)]
    [InlineData(JobStatus.Processing, JobStatus.ReadyReview)]
    [InlineData(JobStatus.Processing, JobStatus.Failed)]
    [InlineData(JobStatus.ReadyReview, JobStatus.InReview)]
    [InlineData(JobStatus.InReview, JobStatus.Pseudonymized)]
    [InlineData(JobStatus.InReview, JobStatus.ReadyReview)]
    [InlineData(JobStatus.Pseudonymized, JobStatus.InReview)]
    [InlineData(JobStatus.Pseudonymized, JobStatus.DePseudonymized)]
    [InlineData(JobStatus.DePseudonymized, JobStatus.DePseudonymized)]
    [InlineData(JobStatus.Created, JobStatus.Cancelled)]
    [InlineData(JobStatus.Processing, JobStatus.Cancelled)]
    public void Validate_ValidTransition_DoesNotThrow(JobStatus from, JobStatus to)
    {
        var act = () => JobStatusTransitions.Validate(from, to);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(JobStatus.Created, JobStatus.ReadyReview)]
    [InlineData(JobStatus.Created, JobStatus.DePseudonymized)]
    [InlineData(JobStatus.Processing, JobStatus.InReview)]
    [InlineData(JobStatus.ReadyReview, JobStatus.Processing)]
    [InlineData(JobStatus.DePseudonymized, JobStatus.Created)]
    public void Validate_InvalidTransition_Throws(JobStatus from, JobStatus to)
    {
        var act = () => JobStatusTransitions.Validate(from, to);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{from}'*'{to}'*");
    }

    [Theory]
    [InlineData(JobStatus.Created, JobStatus.Processing, true)]
    [InlineData(JobStatus.Created, JobStatus.ReadyReview, false)]
    [InlineData(JobStatus.Processing, JobStatus.Failed, true)]
    [InlineData(JobStatus.Created, JobStatus.Cancelled, true)]
    [InlineData(JobStatus.Processing, JobStatus.Cancelled, true)]
    [InlineData(JobStatus.ReadyReview, JobStatus.Cancelled, false)]
    public void IsValid_ReturnsExpectedResult(JobStatus from, JobStatus to, bool expected)
    {
        JobStatusTransitions.IsValid(from, to).Should().Be(expected);
    }
}
