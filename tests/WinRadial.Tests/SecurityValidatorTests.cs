using FluentAssertions;
using WinRadial.Core;
using Xunit;

namespace WinRadial.Tests;

/// <summary>
/// Unit tests for SecurityValidator — path validation, action ID whitelist, color/range checks.
/// </summary>
public class SecurityValidatorTests
{
    // ─── Action ID Validation ──────────────────────────

    [Theory]
    [InlineData("lock_screen")]
    [InlineData("sleep")]
    [InlineData("toggle_dark_mode")]
    [InlineData("empty_recycle_bin")]
    [InlineData("screenshot")]
    [InlineData("app_launch")]
    [InlineData("open_folder")]
    [InlineData("separator")]
    public void ValidateActionId_KnownIds_ReturnsValid(string actionId)
    {
        var (isValid, error) = SecurityValidator.ValidateActionId(actionId);
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("hack_the_planet")]
    [InlineData("rm -rf /")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("LOCK_SCREEN")] // Should pass — case insensitive
    public void ValidateActionId_InvalidOrEdgeCases(string? actionId)
    {
        var (isValid, _) = SecurityValidator.ValidateActionId(actionId);
        // Empty/null should fail, unknown should fail, LOCK_SCREEN should pass (case insensitive)
        if (actionId?.Equals("LOCK_SCREEN", StringComparison.OrdinalIgnoreCase) == true)
            isValid.Should().BeTrue();
        else
            isValid.Should().BeFalse();
    }

    // ─── Path Validation ───────────────────────────────

    [Fact]
    public void ValidatePath_NullOrEmpty_ReturnsFalse()
    {
        SecurityValidator.ValidatePath(null).IsValid.Should().BeFalse();
        SecurityValidator.ValidatePath("").IsValid.Should().BeFalse();
        SecurityValidator.ValidatePath("   ").IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidatePath_DirectoryTraversal_Rejected()
    {
        var (isValid, error) = SecurityValidator.ValidatePath(@"C:\Windows\..\secret");
        isValid.Should().BeFalse();
        error.Should().Contain("..");
    }

    [Theory]
    [InlineData(@"C:\test|evil")]
    [InlineData(@"C:\test&evil")]
    [InlineData(@"C:\test;evil")]
    [InlineData(@"C:\test<evil")]
    [InlineData(@"C:\test>evil")]
    [InlineData("C:\\test`evil")]
    [InlineData(@"C:\test$evil")]
    public void ValidatePath_ShellMetacharacters_Rejected(string path)
    {
        var (isValid, error) = SecurityValidator.ValidatePath(path);
        isValid.Should().BeFalse();
        error.Should().Contain("metacharacter");
    }

    [Fact]
    public void ValidatePath_ExistingSystemFile_ReturnsValid()
    {
        // notepad.exe exists on every Windows install
        var (isValid, error) = SecurityValidator.ValidatePath(@"C:\Windows\System32\notepad.exe");
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void ValidatePath_NonExistentFile_ReturnsFalse()
    {
        var (isValid, error) = SecurityValidator.ValidatePath(@"C:\NonExistent\FakeApp.exe");
        isValid.Should().BeFalse();
        error.Should().Contain("does not exist");
    }

    [Fact]
    public void ValidatePath_ExistingDirectory_ReturnsValid()
    {
        var (isValid, error) = SecurityValidator.ValidatePath(@"C:\Windows\System32");
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    // ─── Executable Path Validation ────────────────────

    [Fact]
    public void ValidateExecutablePath_ValidFile_ReturnsResolvedPath()
    {
        var (isValid, resolvedPath, error) = SecurityValidator.ValidateExecutablePath(@"C:\Windows\System32\notepad.exe");
        isValid.Should().BeTrue();
        resolvedPath.Should().NotBeNull();
        resolvedPath.Should().Contain("notepad.exe");
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateExecutablePath_InvalidPath_ReturnsFalse()
    {
        var (isValid, resolvedPath, error) = SecurityValidator.ValidateExecutablePath(@"C:\fake\..\hack");
        isValid.Should().BeFalse();
        resolvedPath.Should().BeNull();
        error.Should().NotBeNull();
    }

    // ─── Color Validation ──────────────────────────────

    [Theory]
    [InlineData("#FF0000", true)]
    [InlineData("#ff0000", true)]
    [InlineData("#AABBCCDD", true)]
    [InlineData("#123456", true)]
    [InlineData("#12345678", true)]
    [InlineData("FF0000", false)]       // Missing #
    [InlineData("#GG0000", false)]      // Invalid hex
    [InlineData("#12345", false)]       // Too short
    [InlineData("#1234567890", false)]  // Too long
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidHexColor_ReturnsCorrectResult(string? color, bool expected)
    {
        SecurityValidator.IsValidHexColor(color).Should().Be(expected);
    }

    // ─── Range Validation ──────────────────────────────

    [Theory]
    [InlineData(0.5, 0.0, 1.0, true)]
    [InlineData(0.0, 0.0, 1.0, true)]
    [InlineData(1.0, 0.0, 1.0, true)]
    [InlineData(-0.1, 0.0, 1.0, false)]
    [InlineData(1.1, 0.0, 1.0, false)]
    [InlineData(60, 20, 200, true)]
    [InlineData(10, 20, 200, false)]
    public void ValidateRange_ReturnsCorrectResult(double value, double min, double max, bool expected)
    {
        var (isValid, _) = SecurityValidator.ValidateRange(value, min, max, "testField");
        isValid.Should().Be(expected);
    }
}
