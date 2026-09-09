using System;
using FluentAssertions;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Validation;
using Xunit;

namespace SyncAgent.Core.Tests
{
    public sealed class SyncTaskParameterReaderTests
    {
        private readonly FakeClock _clock = new FakeClock(TestData.Now);

        private SyncTaskParameterReader CreateReader() =>
            new SyncTaskParameterReader(TestData.Options(), _clock);

        [Fact]
        public void Reads_the_cutoff_the_platform_sends()
        {
            var cutoff = CreateReader().ReadModifiedSince(TestData.Task());

            cutoff.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }

        [Fact]
        public void Accepts_fractional_seconds()
        {
            // The products payload carries "2025-02-07T10:01:36.827Z", so a round trip of a
            // previously returned timestamp has to parse.
            var cutoff = CreateReader().ReadModifiedSince(
                TestData.Task(modifiedSince: "2025-02-07T10:01:36.827Z"));

            cutoff.Should().Be(new DateTimeOffset(2025, 2, 7, 10, 1, 36, 827, TimeSpan.Zero));
        }

        [Fact]
        public void Normalises_an_explicit_offset_to_UTC()
        {
            var cutoff = CreateReader().ReadModifiedSince(
                TestData.Task(modifiedSince: "2025-01-01T02:00:00+02:00"));

            cutoff.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }

        [Fact]
        public void A_missing_cutoff_is_rejected()
        {
            Action act = () => CreateReader().ReadModifiedSince(TestData.Task(modifiedSince: null));

            act.Should().Throw<SyncTaskValidationException>()
                .WithMessage("*modifiedSince*");
        }

        [Theory]
        [InlineData("not-a-date")]
        [InlineData("01/02/2025")]      // Ambiguous: TryParse would silently pick a month
        [InlineData("2025-13-01T00:00:00Z")]
        [InlineData("")]
        public void A_cutoff_that_is_not_ISO_8601_is_rejected(string value)
        {
            Action act = () => CreateReader().ReadModifiedSince(TestData.Task(modifiedSince: value));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void A_cutoff_older_than_the_configured_minimum_is_rejected()
        {
            // 0001-01-01 turns an incremental sync into a full scan of the client's database.
            Action act = () => CreateReader().ReadModifiedSince(
                TestData.Task(modifiedSince: "0001-01-01T00:00:00Z"));

            act.Should().Throw<SyncTaskValidationException>()
                .WithMessage("*minimum*");
        }

        [Fact]
        public void A_cutoff_in_the_future_is_rejected()
        {
            // It could only return nothing, and reporting a successful sync of zero records
            // would hide the real fault.
            Action act = () => CreateReader().ReadModifiedSince(
                TestData.Task(modifiedSince: "2030-01-01T00:00:00Z"));

            act.Should().Throw<SyncTaskValidationException>()
                .WithMessage("*future*");
        }

        [Fact]
        public void A_cutoff_inside_the_clock_skew_tolerance_is_accepted()
        {
            var justAhead = TestData.Now.AddMinutes(2).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

            Action act = () => CreateReader().ReadModifiedSince(TestData.Task(modifiedSince: justAhead));

            act.Should().NotThrow();
        }

        [Fact]
        public void The_parameter_name_is_matched_case_insensitively()
        {
            var task = new Core.Models.SyncTaskRequest(
                TestData.ValidTaskId,
                "GetCustomers",
                new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ModifiedSince"] = "2025-01-01T00:00:00Z",
                },
                TestData.Now);

            Action act = () => CreateReader().ReadModifiedSince(task);

            act.Should().NotThrow();
        }
    }
}
