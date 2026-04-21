using EduConnect.Api.Common.Auth;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace EduConnect.Api.Tests;

public class PasswordPolicyTests
{
    private readonly PasswordPolicyValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("short1")]            // 6 chars — below MinLength (8)
    [InlineData("abcdefg1")]          // 8 chars, letter + digit → OK (sanity check below)
    [InlineData("12345678")]          // 8 digits, no letter
    [InlineData("abcdefgh")]          // 8 letters, no digit
    public void Rejects_or_accepts_correctly(string password)
    {
        var result = _validator.Validate(password);
        var expectedValid = password == "abcdefg1";
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Accepts_minimum_length_with_letter_and_digit()
    {
        _validator.Validate("password1").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_seven_character_password()
    {
        var result = _validator.Validate("abcdef1");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("at least 8 characters"));
    }

    [Fact]
    public void Rejects_password_longer_than_max()
    {
        var tooLong = new string('a', PasswordPolicy.MaxLength) + "1";
        var result = _validator.Validate(tooLong);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains($"{PasswordPolicy.MaxLength} characters or fewer"));
    }

    [Fact]
    public void Rejects_empty_with_required_message()
    {
        var result = _validator.Validate("");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void IsLegacyPassword_is_true_when_null()
    {
        PasswordPolicy.IsLegacyPassword(null).Should().BeTrue();
    }

    [Fact]
    public void IsLegacyPassword_is_true_when_before_cutoff()
    {
        var before = PasswordPolicy.PolicyEnforcedAt.AddDays(-1);
        PasswordPolicy.IsLegacyPassword(before).Should().BeTrue();
    }

    [Fact]
    public void IsLegacyPassword_is_false_when_at_or_after_cutoff()
    {
        PasswordPolicy.IsLegacyPassword(PasswordPolicy.PolicyEnforcedAt).Should().BeFalse();
        PasswordPolicy.IsLegacyPassword(PasswordPolicy.PolicyEnforcedAt.AddHours(1)).Should().BeFalse();
    }
}
