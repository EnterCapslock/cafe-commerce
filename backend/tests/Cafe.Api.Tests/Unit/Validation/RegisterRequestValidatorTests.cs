using Cafe.Api.Models.Dtos;
using Cafe.Api.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Cafe.Api.Tests.Unit.Validation;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest With(string? name = null, string? email = null, string? password = null) =>
        new(name ?? "Juan Dela Cruz", email ?? "juan@example.com", password ?? "Passw0rd!x");

    [Fact]
    public void A_complete_registration_is_valid()
    {
        _validator.TestValidate(With()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Aa1!aaaa")]                    // exactly 8 characters
    [InlineData("Correct-Horse-Battery-9")]
    public void Passwords_meeting_every_rule_are_accepted(string password)
    {
        _validator.TestValidate(With(password: password)).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("alllowercase1!", "Password must contain at least one uppercase letter.")]
    [InlineData("ALLUPPERCASE1!", "Password must contain at least one lowercase letter.")]
    [InlineData("NoDigitsHere!!", "Password must contain at least one digit.")]
    [InlineData("NoSpecial12345", "Password must contain at least one special character.")]
    public void A_missing_character_class_gets_its_own_message(string password, string message)
    {
        _validator.TestValidate(With(password: password))
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(message);
    }

    [Fact]
    public void A_password_under_eight_characters_is_rejected()
    {
        _validator.TestValidate(With(password: "Sh0rt!A")).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData(72, true)]    // BCrypt ignores anything past 72 bytes, so that's the limit
    [InlineData(73, false)]
    public void A_password_can_be_at_most_72_characters(int length, bool valid)
    {
        var password = "Aa1!" + new string('x', length - 4);
        var result = _validator.TestValidate(With(password: password));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Password);
        else result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void A_valid_email_is_required(string email)
    {
        _validator.TestValidate(With(email: email)).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void A_name_is_required()
    {
        _validator.TestValidate(With(name: "")).ShouldHaveValidationErrorFor(x => x.Name);
    }
}