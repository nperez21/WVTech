using System.Threading.Tasks;
using MealPlanner.Controllers;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests
{
    [TestFixture]
    public class RegisterControllerTests
    {
        private Mock<IRegistrationService> _mockAccountService;
        private Mock<IEmailService> _mockEmailService;
        private RegisterController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockAccountService = new Mock<IRegistrationService>();
            _mockEmailService = new Mock<IEmailService>();
            _controller = new RegisterController(_mockAccountService.Object, _mockEmailService.Object);
            _controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        // ===================== REGISTER =====================
        [Test]
        public async Task Register_Post_RedirectsToEmailAuth_WhenRegistrationSucceeds()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                Name = "Test User",
                Email = "test@test.com",
                Password = "Password123!"
            };

            var user = new User
            {
                Id = "user-id",
                Email = model.Email,
                UserName = model.Email
            };

            _mockAccountService
                .Setup(s => s.RegisterUserAsync(model))
                .ReturnsAsync(IdentityResult.Success);

            _mockAccountService
                .Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync(user);

            _mockAccountService
                .Setup(s => s.GenerateEmailConfirmationTokenAsync(user))
                .ReturnsAsync("email-token");

            var controllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controllerContext.HttpContext.Request.Scheme = "https";
            _controller.ControllerContext = controllerContext;

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://fakeurl/confirmemail");

            _controller.Url = mockUrlHelper.Object;

            _mockEmailService
                .Setup(e => e.SendEmailAsync(
                    user.Email,
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Register(model);

            // Assert
            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult, Is.Not.Null);
            Assert.That(redirectResult.ActionName, Is.EqualTo("Register"));

            _mockEmailService.Verify(
                e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Test]
        public async Task Register_Post_ReturnsView_WhenRegistrationFails()
        {
            var model = new RegisterViewModel
            {
                Name = "Test User",
                Email = "fail@test.com",
                Password = "Password123!"
            };
            var errors = new IdentityError[] { new IdentityError { Description = "Email already taken" } };
            _mockAccountService.Setup(s => s.RegisterUserAsync(model))
                .ReturnsAsync(IdentityResult.Failed(errors));

            var result = await _controller.Register(model);

            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(_controller.ModelState.ContainsKey(""), Is.True);
        }

        [Test]
        public async Task Register_Post_SendsConfirmationEmail_AndRedirectsToEmailAuth()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                Name = "Test User",
                Email = "test@test.com",
                Password = "Password123!"
            };

            var user = new User
            {
                Id = "user-id",
                Email = model.Email,
                UserName = model.Email
            };

            _mockAccountService
                .Setup(s => s.RegisterUserAsync(model))
                .ReturnsAsync(IdentityResult.Success);

            _mockAccountService
                .Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync(user);

            _mockAccountService
                .Setup(s => s.GenerateEmailConfirmationTokenAsync(user))
                .ReturnsAsync("email-token");

            var controllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controllerContext.HttpContext.Request.Scheme = "https";
            _controller.ControllerContext = controllerContext;

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://fakeurl/confirmemail");
            _controller.Url = mockUrlHelper.Object;

            _mockEmailService
                .Setup(e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Register(model);

            // Assert
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect, Is.Not.Null);
            Assert.That(redirect.ActionName, Is.EqualTo("Register"));

            _mockEmailService.Verify(
                e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Test]
        public async Task ConfirmEmail_Get_RedirectsToLogin_WhenConfirmationSucceeds()
        {
            // Arrange
            var user = new User { Id = "user-id", Email = "test@test.com" };
            var token = "valid-token";

            _mockAccountService
                .Setup(s => s.FindUserByIdAsync(user.Id))
                .ReturnsAsync(user);

            _mockAccountService
                .Setup(s => s.ConfirmEmailAsync(user, token))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.ConfirmEmail(user.Id, token);

            // Assert
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect, Is.Not.Null);
            Assert.That(redirect.ActionName, Is.EqualTo("Login"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Login"));
        }

        // ===================== VERIFY EMAIL =====================
        [Test]
        public async Task VerifyEmail_Post_RedirectsToEmailSent_WhenUserFound()
        {
            var model = new VerifyEmailViewModel { Email = "example@test.com" };
            var user = new User { UserName = "example@test.com", Email = "example@test.com" };
            var token = "mock-token";

            // Mock registration service
            _mockAccountService.Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockAccountService.Setup(s => s.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync(token);

            // Setup fake HttpContext and Request.Scheme
            var controllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controllerContext.HttpContext.Request.Scheme = "https";
            _controller.ControllerContext = controllerContext;

            // Mock UrlHelper
            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://fakeurl/resetpassword");
            _controller.Url = mockUrlHelper.Object;

            // Mock email service
            _mockEmailService.Setup(e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.VerifyEmail(model);

            // Assert
            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult, Is.Not.Null);
            Assert.That(redirectResult.ActionName, Is.EqualTo("EmailSent"));
            Assert.That(redirectResult.ControllerName, Is.EqualTo("Register"));

            _mockEmailService.Verify(e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task VerifyEmail_Post_ReturnsView_WhenUserNotFound()
        {
            var model = new VerifyEmailViewModel { Email = "notfound@test.com" };
            _mockAccountService.Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync((User)null);

            var result = await _controller.VerifyEmail(model);

            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(_controller.ModelState.ContainsKey(""), Is.True);

            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ===================== CHANGE PASSWORD =====================
        [Test]
        public async Task ChangePassword_Post_RedirectsToLogin_WhenResetSucceeds()
        {
            var model = new ChangePasswordViewModel
            {
                Email = "user@test.com",
                Token = "mock-token",
                NewPassword = "NewPassword123!"
            };
            var user = new User { Email = model.Email, UserName = model.Email };

            _mockAccountService.Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockAccountService.Setup(s => s.ResetPasswordAsync(user, model.Token, model.NewPassword))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.ChangePassword(model);

            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult, Is.Not.Null);
            Assert.That(redirectResult.ActionName, Is.EqualTo("Login"));
            Assert.That(redirectResult.ControllerName, Is.EqualTo("Login"));
        }

        [Test]
        public async Task ChangePassword_Post_ReturnsView_WhenResetFails()
        {
            var model = new ChangePasswordViewModel
            {
                Email = "user@test.com",
                Token = "mock-token",
                NewPassword = "NewPassword123!"
            };
            var user = new User { Email = model.Email, UserName = model.Email };
            var errors = new IdentityError[] { new IdentityError { Description = "Password invalid" } };

            _mockAccountService.Setup(s => s.FindUserByEmailAsync(model.Email))
                .ReturnsAsync(user);
            _mockAccountService.Setup(s => s.ResetPasswordAsync(user, model.Token, model.NewPassword))
                .ReturnsAsync(IdentityResult.Failed(errors));

            var result = await _controller.ChangePassword(model);

            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(_controller.ModelState.ContainsKey(""), Is.True);
        }
    }
}