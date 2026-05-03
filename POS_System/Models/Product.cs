using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace POS_System.Models;

public partial class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 999999.99, ErrorMessage = "Price must be greater than 0.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Stock is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Product image is required.")]
    public string? ImagePath { get; set; }

    [ValidateNever] 
    public virtual Category Category { get; set; } = null!;
}