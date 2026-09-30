using Shouldly;
using SyncAgent.Core.Validation;

namespace SyncAgent.Tests.Validation;

public sealed class LogSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_values_are_marked(string? value)
        => LogSanitizer.Sanitize(value).ShouldBe("(empty)");

    [Fact]
    public void Normal_values_are_unchanged()
        => LogSanitizer.Sanitize("01JQFG8N3XRTV5KHW2YP4M7B6C").ShouldBe("01JQFG8N3XRTV5KHW2YP4M7B6C");

    [Fact]
    public void Control_characters_are_removed_to_prevent_log_forging()
    {
        var sanitized = LogSanitizer.Sanitize("abc\r\n[ERR] fake entry\t");

        sanitized.ShouldBe("abc[ERR] fake entry");
        sanitized.ShouldNotContain("\n");
        sanitized.ShouldNotContain("\r");
    }

    [Fact]
    public void Long_values_are_truncated()
    {
        var sanitized = LogSanitizer.Sanitize(new string('A', 1000));

        sanitized.Length.ShouldBe(65); // 64 chars + ellipsis
        sanitized.ShouldEndWith("…");
    }
}
