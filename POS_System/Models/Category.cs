using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS_System.Models;

public partial class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    [RegularExpression(@"^[a-zA-Z\s]+$",
        ErrorMessage = "Category name must contain letters only. No numbers or special characters allowed.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}