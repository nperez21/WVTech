using Microsoft.AspNetCore.Mvc;
using MealPlanner.Services;
using MealPlanner.ViewModels;

namespace MealPlanner.Controllers;

public class RegisterController : Controller
{
    private readonly IRegistrationService _registrationService;
    private readonly IEmailService _emailService;

    public RegisterController(IRegistrationService registrationService, IEmailService emailService)
    {
        _registrationService = registrationService;
        _emailService = emailService;
    }

    [HttpGet("Register")]
    public IActionResult Register()
    {
        ViewBag.ShowEmailModal = TempData["ShowEmailModal"] as bool? ?? false;
        return View();
    }

    [HttpPost("Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _registrationService.RegisterUserAsync(model);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // Registration succeeded, send confirmation email
        var user = await _registrationService.FindUserByEmailAsync(model.Email);
        var token = await _registrationService.GenerateEmailConfirmationTokenAsync(user);

        var confirmationLink = Url.Action(
            "ConfirmEmail",
            "Register",
            new { userId = user.Id, token },
            Request.Scheme);

        var subject = "Confirm your email";
        var message = $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.";

        await _emailService.SendEmailAsync(user.Email, subject, message);

        TempData["ShowEmailModal"] = true;
        return RedirectToAction("Register");
    }

    //confirm user email when they click the link in their email
    [HttpGet("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return RedirectToAction("Index", "Home");

        var user = await _registrationService.FindUserByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        var result = await _registrationService.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            // Redirect user to profile or login page
            return RedirectToAction("Login", "Login"); // or wherever
        }

        return View("Error"); // create an error view if needed
    }


    [HttpGet("VerifyEmail")]
    public IActionResult VerifyEmail() => View();

    [HttpPost("VerifyEmail")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _registrationService.FindUserByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "No account found with that email.");
            return View(model);
        }

        var token = await _registrationService.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ChangePassword", "Register", new { email = model.Email, token = token }, Request.Scheme);

        var subject = "Reset Password";
        var message = $"Click the link to reset your password: <a href='{resetLink}'>Reset Password</a>";

        await _emailService.SendEmailAsync(model.Email, subject, message); //body instead of message?
        return RedirectToAction("EmailSent", "Register");


    }

    [HttpGet("ChangePassword")]
    public IActionResult ChangePassword(string email, string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)) return RedirectToAction("VerifyEmail", "Register");

        var model = new ChangePasswordViewModel { Email = email, Token = token };
        return View(model);
    }

    [HttpPost("ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _registrationService.FindUserByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "No account found with that email.");
            return View(model);
        }

        var resetResult = await _registrationService.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (resetResult.Succeeded)
        {
            // Redirect to login page after successful password reset
            return RedirectToAction("Login", "Login"); // Update "Account" if your login controller is different
        }

        foreach (var error in resetResult.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }


    [HttpGet("EmailSent")]
    public IActionResult EmailSent()
    {
        return View();
    }

    [HttpGet("EmailAuth")]
    public IActionResult EmailAuth()
    {
        return View();
    }

}
