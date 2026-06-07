using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC.Controllers;
using MVC.ViewModels;
using Xunit;

namespace Test;

public sealed class AccountControllerTests
{
    [Fact]
    public void ChangePasswordGet_RequiresAuthenticatedUser()
    {
        var action = typeof(AccountController).GetMethod(nameof(AccountController.ChangePassword), Type.EmptyTypes);

        Assert.NotNull(action);
        Assert.Contains(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true),
            attribute => attribute is AuthorizeAttribute);
    }

    [Fact]
    public void ChangePasswordPost_RequiresAuthenticatedUserAndAntiforgeryToken()
    {
        var action = typeof(AccountController)
            .GetMethods()
            .Single(method =>
                method.Name == nameof(AccountController.ChangePassword)
                && method.GetParameters().Length == 2);

        Assert.Contains(
            action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true),
            attribute => attribute is AuthorizeAttribute);
        Assert.Contains(
            action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
            attribute => attribute is ValidateAntiForgeryTokenAttribute);
    }

    [Fact]
    public void ChangePasswordViewModel_MismatchedConfirmation_IsInvalid()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "Current@123",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "Different@123"
        };

        var results = Validate(model);

        Assert.Contains(results, result => result.ErrorMessage == "Confirm password does not match.");
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}

public sealed class AdminUsersControllerSecurityTests
{
    [Fact]
    public void ResetPasswordPost_UsesAntiforgeryToken()
    {
        var action = typeof(AdminUsersController)
            .GetMethods()
            .Single(method => method.Name == nameof(AdminUsersController.ResetPassword));

        Assert.Contains(
            action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
            attribute => attribute is ValidateAntiForgeryTokenAttribute);
    }
}
