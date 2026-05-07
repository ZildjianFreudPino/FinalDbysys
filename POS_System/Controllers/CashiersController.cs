using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_System.Data;
using POS_System.Models;
using POS_System.Models.ViewModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace POS_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CashiersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CashiersController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _context = context;
            _env = env;
        }

        // GET: /Cashiers
        public async Task<IActionResult> Index(string search)
        {
            var users = await _context.Database
    .SqlQueryRaw<CashierViewModel>(
        @"SELECT u.Id, 
                 ISNULL(u.FullName, '') AS FullName, 
                 ISNULL(u.UserName, '') AS UserName, 
                 ISNULL(u.Email, '') AS Email, 
                 ISNULL(u.ProfileImage, '/images/default-avatar.png') AS ProfileImage 
          FROM AspNetUsers u
          INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
          INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
          WHERE r.Name = 'Cashier'")
    .ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u =>
                    (u.FullName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.UserName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            ViewData["Search"] = search;
            return View(users);
        }

        // GET: /Cashiers/Create
        public IActionResult Create()
        {
            return RedirectToPage("/Account/Register", new { area = "Identity" });
        }

        // GET: /Cashiers/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var data = await _context.Database
                .SqlQueryRaw<CashierInfo>(
                    "SELECT Id, FullName, ProfileImage FROM AspNetUsers WHERE Id = {0}", id)
                .ToListAsync();

            ViewBag.FullName = data.FirstOrDefault()?.FullName ?? "";
            ViewBag.ProfileImage = data.FirstOrDefault()?.ProfileImage ?? "/images/default-avatar.png";

            return View(user);
        }

        // POST: /Cashiers/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            string userName,
            string email,
            string fullName,
            string newPassword,
            string confirmPassword,
            IFormFile ProfileImage)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Check duplicate username (exclude current user)
            var existingUserName = await _userManager.FindByNameAsync(userName);
            if (existingUserName != null && existingUserName.Id != id)
            {
                TempData["Error"] = "Username is already taken.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Check duplicate email (exclude current user)
            var existingEmail = await _userManager.FindByEmailAsync(email);
            if (existingEmail != null && existingEmail.Id != id)
            {
                TempData["Error"] = "Email is already registered.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Password change
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    TempData["Error"] = "Passwords do not match.";
                    return RedirectToAction(nameof(Edit), new { id });
                }

                var passToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passResult = await _userManager.ResetPasswordAsync(user, passToken, newPassword);
                if (!passResult.Succeeded)
                {
                    TempData["Error"] = string.Join(", ", passResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Edit), new { id });
                }
            }

            // Update username and email
            user.UserName = userName;
            user.Email = email;
            user.NormalizedUserName = userName.ToUpper();
            user.NormalizedEmail = email.ToUpper();
            await _userManager.UpdateAsync(user);

            // Handle image upload
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "cashiers");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{id}{Path.GetExtension(ProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                string profileImagePath = $"/uploads/cashiers/{fileName}";

                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE AspNetUsers SET FullName = {0}, ProfileImage = {1} WHERE Id = {2}",
                    fullName ?? string.Empty,
                    profileImagePath,
                    id);
            }
            else
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE AspNetUsers SET FullName = {0} WHERE Id = {1}",
                    fullName ?? string.Empty,
                    id);
            }

            TempData["Success"] = "Cashier updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Cashiers/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var data = await _context.Database
                .SqlQueryRaw<CashierInfo>(
                    "SELECT Id, FullName, ProfileImage FROM AspNetUsers WHERE Id = {0}", id)
                .ToListAsync();

            ViewBag.FullName = data.FirstOrDefault()?.FullName ?? "N/A";
            ViewBag.ProfileImage = data.FirstOrDefault()?.ProfileImage ?? "/images/default-avatar.png";

            return View(user);
        }

        // POST: /Cashiers/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            TempData["Success"] = "Cashier deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}