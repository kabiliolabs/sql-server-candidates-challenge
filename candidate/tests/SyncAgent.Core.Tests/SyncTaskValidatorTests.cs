using System;
using FluentAssertions;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;
using Xunit;

namespace SyncAgent.Core.Tests
{
    public sealed class SyncTaskValidatorTests
    {
        private readonly FakeClock _clock = new FakeClock(TestData.Now);

        private SyncTaskValidator CreateValidator() => new SyncTaskValidator(TestData.Options(), _clock);

        [Fact]
        public void A_well_formed_task_passes()
        {
            Action act = () => CreateValidator().EnsureValid(TestData.Task());

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void A_blank_task_id_is_rejected(string taskId)
        {
            Action act = () => CreateValidator().EnsureValid(TestData.Task(taskId: taskId));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Theory]
        [InlineData("too-short")]
        [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6")]      // 25 characters
        [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6CC")]    // 27 characters
        [InlineData("01IQFG8N3XRTV5KHW2YP4M7B6C")]     // 'I' is not in the Crockford alphabet
        [InlineData("01UQFG8N3XRTV5KHW2YP4M7B6C")]     // 'U' is not in the Crockford alphabet
        [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6-")]
        public void A_task_id_that_is_not_a_ULID_is_rejected(string taskId)
        {
            Action act = () => CreateValidator().EnsureValid(TestData.Task(taskId: taskId));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void A_lowercase_ULID_is_accepted()
        {
            // The contract does not promise a casing, and rejecting a valid identifier over
            // one is a worse failure than accepting it.
            Action act = () => CreateValidator().EnsureValid(
                TestData.Task(taskId: TestData.ValidTaskId.ToLowerInvariant()));

            act.Should().NotThrow();
        }

        [Fact]
        public void A_missing_task_type_is_rejected()
        {
            Action act = () => CreateValidator().EnsureValid(TestData.Task(taskType: ""));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void An_over_long_task_type_is_rejected()
        {
            Action act = () => CreateValidator().EnsureValid(
                TestData.Task(taskType: new string('A', 65)));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void An_over_long_parameter_value_is_rejected()
        {
            Action act = () => CreateValidator().EnsureValid(
                TestData.Task(modifiedSince: new string('9', 257)));

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void A_creation_timestamp_beyond_the_clock_skew_tolerance_is_rejected()
        {
            // Every incremental cutoff the platform sends comes from the same clock, so a
            // disagreement this large is worth surfacing rather than working around.
            var task = TestData.Task(createdAt: TestData.Now.AddMinutes(6));

            Action act = () => CreateValidator().EnsureValid(task);

            act.Should().Throw<SyncTaskValidationException>();
        }

        [Fact]
        public void A_creation_timestamp_inside_the_clock_skew_tolerance_is_accepted()
        {
            var task = TestData.Task(createdAt: TestData.Now.AddMinutes(4));

            Action act = () => CreateValidator().EnsureValid(task);

            act.Should().NotThrow();
        }
    }
}
