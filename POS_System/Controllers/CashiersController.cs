using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace POS_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CashiersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public CashiersController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: /Cashiers
        public async Task<IActionResult> Index()
        {
            var cashiers = await _userManager.GetUsersInRoleAsync("Cashier");
            return View(cashiers);
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
            return View(user);
        }

        // POST: /Cashiers/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string userName, string email)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.UserName = userName;
            user.Email = email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cashier updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(user);
        }

        // GET: /Cashiers/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
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