#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using POS_System.Models;

namespace POS_System.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AspNetUser> _signInManager;
        private readonly UserManager<AspNetUser> _userManager;
        private readonly IUserStore<AspNetUser> _userStore;
        private readonly IUserEmailStore<AspNetUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RegisterModel(
            UserManager<AspNetUser> userManager,
            IUserStore<AspNetUser> userStore,
            SignInManager<AspNetUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IWebHostEnvironment env,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _env = env;
            _roleManager = roleManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            public string UserName { get; set; }

            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Profile Image")]
            public IFormFile ProfileImage { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Check duplicate username
                var existingUserName = await _userManager.FindByNameAsync(Input.UserName);
                if (existingUserName != null)
                {
                    ModelState.AddModelError(string.Empty, "Username is already taken.");
                    return Page();
                }

                // Check duplicate email
                var existingEmail = await _userManager.FindByEmailAsync(Input.Email);
                if (existingEmail != null)
                {
                    ModelState.AddModelError(string.Empty, "Email is already registered.");
                    return Page();
                }

                // Create AspNetUser and set FullName directly
                var user = new AspNetUser
                {
                    FullName = Input.FullName?.Trim()
                };

                await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // Handle image upload before CreateAsync so we can clean up on failure
                string tempImagePath = null;
                string tempFileName = null;

                if (Input.ProfileImage != null && Input.ProfileImage.Length > 0)
                {
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(Input.ProfileImage.ContentType.ToLower()))
                    {
                        ModelState.AddModelError(string.Empty, "Only image files (jpg, png, gif, webp) are allowed.");
                        return Page();
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "cashiers");
                    Directory.CreateDirectory(uploadsFolder);

                    tempFileName = $"temp_{Guid.NewGuid()}{Path.GetExtension(Input.ProfileImage.FileName)}";
                    tempImagePath = Path.Combine(uploadsFolder, tempFileName);

                    using (var stream = new FileStream(tempImagePath, FileMode.Create))
                    {
                        await Input.ProfileImage.CopyToAsync(stream);
                    }

                    // Set temp path on user so it gets saved in CreateAsync
                    user.ProfileImage = $"/uploads/cashiers/{tempFileName}";
                }

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created successfully. ID: {UserId}", user.Id);

                    // Rename temp image to use actual user ID
                    if (tempImagePath != null && System.IO.File.Exists(tempImagePath))
                    {
                        try
                        {
                            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "cashiers");
                            var newFileName = $"{user.Id}{Path.GetExtension(Input.ProfileImage.FileName)}";
                            var newFilePath = Path.Combine(uploadsFolder, newFileName);

                            System.IO.File.Move(tempImagePath, newFilePath);

                            user.ProfileImage = $"/uploads/cashiers/{newFileName}";
                            await _userManager.UpdateAsync(user);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to rename image for user {UserId}", user.Id);
                        }
                    }

                    // Auto-confirm email
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _userManager.ConfirmEmailAsync(user, token);

                    // Ensure Cashier role exists then assign
                    if (!await _roleManager.RoleExistsAsync("Cashier"))
                        await _roleManager.CreateAsync(new IdentityRole("Cashier"));

                    await _userManager.AddToRoleAsync(user, "Cashier");

                    TempData["Success"] = "Cashier registered successfully.";
                    return RedirectToAction("Index", "Cashiers");
                }

                // Clean up temp image if user creation failed
                if (tempImagePath != null && System.IO.File.Exists(tempImagePath))
                    System.IO.File.Delete(tempImagePath);

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private AspNetUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AspNetUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(AspNetUser)}'. " +
                    $"Ensure that '{nameof(AspNetUser)}' is not an abstract class and has a parameterless constructor.");
            }
        }

        private IUserEmailStore<AspNetUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("The default UI requires a user store with email support.");

            return (IUserEmailStore<AspNetUser>)_userStore;
        }
    }
}