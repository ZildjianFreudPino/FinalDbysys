using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_System.Data;
using POS_System.Models;

namespace POS_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Products
        public async Task<IActionResult> Index(string search)
        {
            var products = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                products = products.Where(p =>
                    p.Name.Contains(search) ||
                    p.Description.Contains(search) ||
                    p.Category.Name.Contains(search));
            }

            ViewData["Search"] = search;
            return View(await products.ToListAsync());
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,Stock,CategoryId")] Product product, IFormFile? ImageFile)
        {
            ModelState.Remove("ImagePath");
            ModelState.Remove("Category");

            bool isDuplicate = await _context.Products
                .AnyAsync(p => p.Name.ToLower() == product.Name.ToLower());
            if (isDuplicate)
                ModelState.AddModelError("Name", "A product with this name already exists.");

            if (ImageFile == null || ImageFile.Length == 0)
                ModelState.AddModelError("ImageFile", "Product image is required.");

            if (ModelState.IsValid)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + Path.GetExtension(ImageFile!.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ImageFile.CopyToAsync(stream);
                product.ImagePath = "/uploads/products/" + fileName;

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,Stock,CategoryId,ImagePath")] Product product, IFormFile? ImageFile)
        {
            if (id != product.Id) return NotFound();

            // ✅ Fix: Remove these from ModelState validation
            ModelState.Remove("ImagePath");
            ModelState.Remove("Category");

            bool isDuplicate = await _context.Products
                .AnyAsync(p => p.Name.ToLower() == product.Name.ToLower() && p.Id != product.Id);
            if (isDuplicate)
                ModelState.AddModelError("Name", "A product with this name already exists.");

            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(product.ImagePath))
                    {
                        var oldPath = Path.Combine(_env.WebRootPath, product.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await ImageFile.CopyToAsync(stream);
                    product.ImagePath = "/uploads/products/" + fileName;
                }
                else
                {
                    // ✅ Fix: If no new image uploaded, keep the existing ImagePath from DB
                    var existingProduct = await _context.Products.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == product.Id);
                    if (existingProduct != null)
                        product.ImagePath = existingProduct.ImagePath;
                }

                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    var imgPath = Path.Combine(_env.WebRootPath, product.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imgPath))
                        System.IO.File.Delete(imgPath);
                }
                _context.Products.Remove(product);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}