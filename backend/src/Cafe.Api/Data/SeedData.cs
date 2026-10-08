using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Data;

public static class SeedData
{
    public static List<Product> CreateMenu() =>
    [
        // ---------- Coffee ----------
        P("Espresso", "A concentrated shot of our house espresso.", ProductCategory.Coffee,
            ("Single", 2.75m), ("Double", 3.50m)),
        P("Americano", "Espresso topped with hot water for a clean, bold cup.", ProductCategory.Coffee,
            ("Small", 3.25m), ("Medium", 3.75m), ("Large", 4.25m)),
        P("Flat White", "Double ristretto with silky steamed milk.", ProductCategory.Coffee,
            ("Small", 3.50m), ("Medium", 4.00m), ("Large", 4.50m)),
        P("Latte", "Smooth espresso with plenty of steamed milk.", ProductCategory.Coffee,
            ("Small", 3.75m), ("Medium", 4.25m), ("Large", 4.75m)),
        P("Cappuccino", "Equal parts espresso, steamed milk, and foam.", ProductCategory.Coffee,
            ("Small", 3.75m), ("Medium", 4.25m), ("Large", 4.75m)),
        P("Mocha", "Espresso, steamed milk, and rich dark chocolate.", ProductCategory.Coffee,
            ("Small", 4.25m), ("Medium", 4.75m), ("Large", 5.25m)),
        P("Cold Brew", "Steeped for 18 hours and served over ice.", ProductCategory.Coffee,
            ("12oz", 4.25m), ("16oz", 4.95m)),

        // ---------- Food ----------
        P("Croissant", "Flaky, buttery, and baked fresh every morning.", ProductCategory.Food,
            ("Plain", 3.25m), ("Almond", 4.00m), ("Chocolate", 3.95m)),
        P("Muffin", "Moist, golden muffin baked in-house daily.", ProductCategory.Food,
            ("Blueberry", 3.50m), ("Banana Walnut", 3.75m), ("Double Chocolate", 3.75m)),
        P("Banana Bread", "Dense, sweet, and loaded with ripe bananas.", ProductCategory.Food,
            ("Slice", 3.75m), ("Whole Loaf", 14.00m)),
        P("Avocado Toast", "Sourdough, smashed avocado, chili flakes, and lemon.", ProductCategory.Food,
            ("Regular", 8.50m), ("With Egg", 10.00m)),
        P("Breakfast Sandwich", "Toasted brioche with egg and cheddar.", ProductCategory.Food,
            ("Bacon", 7.50m), ("Sausage", 7.50m), ("Veggie", 7.00m)),

        // ---------- Merchandise ----------
        P("House Blend Beans", "Our signature medium roast, whole bean.", ProductCategory.Merchandise,
            ("250g", 12.00m), ("500g", 22.00m), ("1kg", 38.00m)),
        P("Ceramic Mug", "Hand-glazed 350ml mug.", ProductCategory.Merchandise,
            ("White", 14.00m), ("Black", 14.00m), ("Terracotta", 15.00m)),
        P("Canvas Tote Bag", "Heavy cotton tote with our logo.", ProductCategory.Merchandise,
            ("Natural", 18.00m), ("Black", 18.00m)),
    ];

    private static Product P(string name, string description, ProductCategory category,
        params (string Name, decimal Price)[] variants) => new()
    {
        Name = name,
        Description = description,
        Category = category,
        Variants = variants
            .Select((v, i) => new ProductVariant { Name = v.Name, Price = v.Price, SortOrder = i })
            .ToList()
    };
}