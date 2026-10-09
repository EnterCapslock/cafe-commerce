using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Data;

public static class SeedData
{
    public static List<Product> CreateMenu() =>
    [
        // ---------- Coffee ----------
        P("Espresso", "A concentrated shot of our house espresso.", ProductCategory.Coffee,
            ("Single", 90m), ("Double", 120m)),
        P("Americano", "Espresso topped with hot water for a clean, bold cup.", ProductCategory.Coffee,
            ("Small", 110m), ("Medium", 125m), ("Large", 140m)),
        P("Flat White", "Double ristretto with silky steamed milk.", ProductCategory.Coffee,
            ("Small", 130m), ("Medium", 150m), ("Large", 170m)),
        P("Latte", "Smooth espresso with plenty of steamed milk.", ProductCategory.Coffee,
            ("Small", 130m), ("Medium", 150m), ("Large", 170m)),
        P("Cappuccino", "Equal parts espresso, steamed milk, and foam.", ProductCategory.Coffee,
            ("Small", 130m), ("Medium", 150m), ("Large", 170m)),
        P("Mocha", "Espresso, steamed milk, and rich dark chocolate.", ProductCategory.Coffee,
            ("Small", 145m), ("Medium", 165m), ("Large", 185m)),
        P("Cold Brew", "Steeped for 18 hours and served over ice.", ProductCategory.Coffee,
            ("12oz", 140m), ("16oz", 165m)),

        // ---------- Food ----------
        P("Croissant", "Flaky, buttery, and baked fresh every morning.", ProductCategory.Food,
            ("Plain", 95m), ("Almond", 125m), ("Chocolate", 120m)),
        P("Muffin", "Moist, golden muffin baked in-house daily.", ProductCategory.Food,
            ("Blueberry", 110m), ("Banana Walnut", 115m), ("Double Chocolate", 115m)),
        P("Banana Bread", "Dense, sweet, and loaded with ripe bananas.", ProductCategory.Food,
            ("Slice", 105m), ("Whole Loaf", 480m)),
        P("Avocado Toast", "Sourdough, smashed avocado, chili flakes, and calamansi.", ProductCategory.Food,
            ("Regular", 245m), ("With Egg", 295m)),
        P("Breakfast Sandwich", "Toasted brioche with egg and cheddar.", ProductCategory.Food,
            ("Bacon", 220m), ("Sausage", 220m), ("Veggie", 195m)),

        // ---------- Merchandise ----------
        P("House Blend Beans", "Our signature medium roast, whole bean.", ProductCategory.Merchandise,
            ("250g", 450m), ("500g", 850m), ("1kg", 1600m)),
        P("Ceramic Mug", "Hand-glazed 350ml mug.", ProductCategory.Merchandise,
            ("White", 450m), ("Black", 450m), ("Terracotta", 480m)),
        P("Canvas Tote Bag", "Heavy cotton tote with our logo.", ProductCategory.Merchandise,
            ("Natural", 350m), ("Black", 350m)),
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