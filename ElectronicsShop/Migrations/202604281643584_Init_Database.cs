namespace ElectronicsShop.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Init_Database : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Cart",
                c => new
                    {
                        cart_id = c.Int(nullable: false, identity: true),
                        user_id = c.Int(nullable: false),
                        product_id = c.Int(nullable: false),
                        quantity = c.Int(),
                    })
                .PrimaryKey(t => t.cart_id)
                .ForeignKey("dbo.Products", t => t.product_id)
                .ForeignKey("dbo.Users", t => t.user_id)
                .Index(t => t.user_id)
                .Index(t => t.product_id);
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        product_id = c.Int(nullable: false, identity: true),
                        category_id = c.Int(nullable: false),
                        product_name = c.String(nullable: false, maxLength: 100),
                        description = c.String(maxLength: 2000),
                        price = c.Decimal(precision: 10, scale: 2),
                        discount_price = c.Decimal(precision: 10, scale: 2),
                        stock = c.Int(),
                        brand = c.String(maxLength: 255),
                        is_new = c.Boolean(),
                        material = c.String(),
                        size = c.String(),
                        status = c.String(),
                        color = c.String(),
                        warranty = c.Int(),
                    })
                .PrimaryKey(t => t.product_id)
                .ForeignKey("dbo.Categories", t => t.category_id)
                .Index(t => t.category_id);
            
            CreateTable(
                "dbo.Categories",
                c => new
                    {
                        category_id = c.Int(nullable: false, identity: true),
                        category_name = c.String(nullable: false, maxLength: 100),
                    })
                .PrimaryKey(t => t.category_id);
            
            CreateTable(
                "dbo.Order_Items",
                c => new
                    {
                        order_item_id = c.Int(nullable: false, identity: true),
                        order_id = c.Int(nullable: false),
                        product_id = c.Int(nullable: false),
                        quantity = c.Int(),
                        price = c.Decimal(precision: 10, scale: 2),
                    })
                .PrimaryKey(t => t.order_item_id)
                .ForeignKey("dbo.Orders", t => t.order_id)
                .ForeignKey("dbo.Products", t => t.product_id)
                .Index(t => t.order_id)
                .Index(t => t.product_id);
            
            CreateTable(
                "dbo.Orders",
                c => new
                    {
                        order_id = c.Int(nullable: false, identity: true),
                        shipment_id = c.Int(nullable: false),
                        user_id = c.Int(nullable: false),
                        order_date = c.DateTime(),
                        total_amount = c.Decimal(precision: 10, scale: 2),
                        status = c.String(maxLength: 50),
                        payment_method = c.String(maxLength: 50),
                        order_note = c.String(maxLength: 10, fixedLength: true, unicode: false),
                    })
                .PrimaryKey(t => t.order_id)
                .ForeignKey("dbo.Shipments", t => t.shipment_id)
                .ForeignKey("dbo.Users", t => t.user_id)
                .Index(t => t.shipment_id)
                .Index(t => t.user_id);
            
            CreateTable(
                "dbo.Shipments",
                c => new
                    {
                        shipment_id = c.Int(nullable: false, identity: true),
                        user_id = c.Int(nullable: false),
                        recipient_first_name = c.String(),
                        recipient_last_name = c.String(),
                        recipient_phone = c.String(),
                        shipment_address = c.String(maxLength: 255),
                        shipment_city = c.String(maxLength: 50),
                        shipment_country = c.String(maxLength: 50),
                        shipment_zip_code = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.shipment_id)
                .ForeignKey("dbo.Users", t => t.user_id)
                .Index(t => t.user_id);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        user_id = c.Int(nullable: false, identity: true),
                        first_name = c.String(nullable: false, maxLength: 50),
                        last_name = c.String(nullable: false, maxLength: 50),
                        email = c.String(nullable: false, maxLength: 100, unicode: false),
                        password = c.String(nullable: false, maxLength: 100, unicode: false),
                        phone = c.String(maxLength: 20, unicode: false),
                        role = c.String(maxLength: 50),
                    })
                .PrimaryKey(t => t.user_id);
            
            CreateTable(
                "dbo.Wishlist",
                c => new
                    {
                        wishlist_id = c.Int(nullable: false, identity: true),
                        user_id = c.Int(nullable: false),
                        product_id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.wishlist_id)
                .ForeignKey("dbo.Users", t => t.user_id)
                .ForeignKey("dbo.Products", t => t.product_id)
                .Index(t => t.user_id)
                .Index(t => t.product_id);
            
            CreateTable(
                "dbo.Product_Images",
                c => new
                    {
                        image_id = c.Int(nullable: false, identity: true),
                        image_url = c.String(nullable: false, maxLength: 255, unicode: false),
                        product_id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.image_id)
                .ForeignKey("dbo.Products", t => t.product_id)
                .Index(t => t.product_id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Wishlist", "product_id", "dbo.Products");
            DropForeignKey("dbo.Product_Images", "product_id", "dbo.Products");
            DropForeignKey("dbo.Order_Items", "product_id", "dbo.Products");
            DropForeignKey("dbo.Wishlist", "user_id", "dbo.Users");
            DropForeignKey("dbo.Shipments", "user_id", "dbo.Users");
            DropForeignKey("dbo.Orders", "user_id", "dbo.Users");
            DropForeignKey("dbo.Cart", "user_id", "dbo.Users");
            DropForeignKey("dbo.Orders", "shipment_id", "dbo.Shipments");
            DropForeignKey("dbo.Order_Items", "order_id", "dbo.Orders");
            DropForeignKey("dbo.Products", "category_id", "dbo.Categories");
            DropForeignKey("dbo.Cart", "product_id", "dbo.Products");
            DropIndex("dbo.Product_Images", new[] { "product_id" });
            DropIndex("dbo.Wishlist", new[] { "product_id" });
            DropIndex("dbo.Wishlist", new[] { "user_id" });
            DropIndex("dbo.Shipments", new[] { "user_id" });
            DropIndex("dbo.Orders", new[] { "user_id" });
            DropIndex("dbo.Orders", new[] { "shipment_id" });
            DropIndex("dbo.Order_Items", new[] { "product_id" });
            DropIndex("dbo.Order_Items", new[] { "order_id" });
            DropIndex("dbo.Products", new[] { "category_id" });
            DropIndex("dbo.Cart", new[] { "product_id" });
            DropIndex("dbo.Cart", new[] { "user_id" });
            DropTable("dbo.Product_Images");
            DropTable("dbo.Wishlist");
            DropTable("dbo.Users");
            DropTable("dbo.Shipments");
            DropTable("dbo.Orders");
            DropTable("dbo.Order_Items");
            DropTable("dbo.Categories");
            DropTable("dbo.Products");
            DropTable("dbo.Cart");
        }
    }
}
