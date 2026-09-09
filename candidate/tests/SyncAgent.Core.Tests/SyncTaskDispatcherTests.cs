using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;
using Xunit;

namespace SyncAgent.Core.Tests
{
    public sealed class SyncTaskDispatcherTests
    {
        [Fact]
        public async Task DispatchAsync_routes_to_the_handler_for_the_task_type()
        {
            var customers = new StubHandler("GetCustomers");
            var products = new StubHandler("GetProducts");
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[] { customers, products });

            await dispatcher.DispatchAsync(TestData.Task("GetProducts"), CancellationToken.None);

            products.Invocations.Should().Be(1);
            customers.Invocations.Should().Be(0);
        }

        [Fact]
        public async Task DispatchAsync_matches_the_task_type_case_insensitively()
        {
            // The platform sends PascalCase today. A casing change upstream should degrade
            // into a working sync, not into every task failing as unsupported.
            var handler = new StubHandler("GetCustomers");
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[] { handler });

            await dispatcher.DispatchAsync(TestData.Task("getcustomers"), CancellationToken.None);

            handler.Invocations.Should().Be(1);
        }

        [Fact]
        public void DispatchAsync_throws_UnknownTaskTypeException_for_an_unregistered_type()
        {
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[] { new StubHandler("GetCustomers") });

            Func<Task> act = () => dispatcher.DispatchAsync(TestData.Task("GetSomethingElse"), CancellationToken.None);

            act.Should().ThrowAsync<UnknownTaskTypeException>();
        }

        [Fact]
        public async Task DispatchAsync_does_not_echo_the_rejected_task_type()
        {
            // The message travels back to the platform, and the value came off the network.
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[] { new StubHandler("GetCustomers") });
            const string Hostile = "<script>alert(1)</script>";

            var exception = await Assert.ThrowsAsync<UnknownTaskTypeException>(
                () => dispatcher.DispatchAsync(TestData.Task(Hostile), CancellationToken.None));

            exception.Message.Should().NotContain(Hostile);
            exception.Message.Should().Be(UnknownTaskTypeException.SanitizedMessage);
        }

        [Fact]
        public void Constructor_rejects_two_handlers_for_the_same_task_type()
        {
            // Otherwise which one wins depends on container registration order, which is the
            // kind of bug that only reproduces in one environment.
            var handlers = new ISyncTaskHandler[]
            {
                new StubHandler("GetCustomers"),
                new StubHandler("GetCustomers"),
            };

            Action act = () => new SyncTaskDispatcher(handlers);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GetCustomers*");
        }

        [Fact]
        public void Constructor_rejects_a_handler_with_no_task_type()
        {
            Action act = () => new SyncTaskDispatcher(new ISyncTaskHandler[] { new StubHandler("  ") });

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SupportedTaskTypes_lists_every_registered_handler()
        {
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[]
            {
                new StubHandler("GetProducts"),
                new StubHandler("GetCustomers"),
            });

            dispatcher.SupportedTaskTypes.Should().BeEquivalentTo(new[] { "GetCustomers", "GetProducts" });
        }

        [Fact]
        public async Task DispatchAsync_passes_the_task_through_unchanged()
        {
            var handler = new StubHandler("GetCustomers", new SyncPayload(new object[] { 1, 2 }, false));
            var dispatcher = new SyncTaskDispatcher(new ISyncTaskHandler[] { handler });
            var task = TestData.Task();

            var payload = await dispatcher.DispatchAsync(task, CancellationToken.None);

            handler.LastTask.Should().BeSameAs(task);
            payload.Records.Should().HaveCount(2);
        }
    }
}
