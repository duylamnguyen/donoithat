using ElectronicsShop.Models;
using ElectronicsShop.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace ElectronicsShop.Controllers
{

    public class ProductController : Customer_BaseController
    {
		private readonly string connectionString = ConnectionStrings.DefaultConnection;

		// GET: Product by ID
		public Product GetProductByID(int id)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                // Query to get product details
                string productQuery = @"
                    SELECT
                        *
                    FROM
                        Products p
                    WHERE
                        p.product_id = @ProductId";

                // Query to get product images
                string imagesQuery = @"
                    SELECT
                        *
                    FROM
                        Product_Images pi
                    WHERE
                        pi.product_id = @ProductId";

                // Query to get product images
                string categoryQuery = @"
                    SELECT
                        *
                    FROM
                        Categories category
                    WHERE
                        category.category_id = @CategoryID";

                SqlCommand cmd = new SqlCommand(productQuery, con);
                cmd.Parameters.AddWithValue("@ProductId", id);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                Product product = null;
                if (reader.Read())
                {
                    product = new Product
                    {
                        product_id = (int)reader["product_id"],
                        product_name = (string)reader["product_name"],
                        description = (string)reader["description"],
                        price = reader["price"] as decimal?,
                        discount_price = reader["discount_price"] as decimal?,
                        brand = reader["brand"] as string,
                        stock = reader["stock"] as int?,
                        is_new = reader["is_new"] as bool?,
                        material = reader["material"] as string,
                        size = reader["size"] as string,
                        status = reader["status"] as string,
                        color = reader["color"] as string,
                        warranty = reader["warranty"] as int?,
						category_id = (int)reader["category_id"],
                        Category = new Category(),
                        Product_Images = new List<Product_Images>()
                    };
                }

                reader.Close(); // Close the reader before executing the next command

                // Fetching product images
                if (product != null)
                {
                    SqlCommand imgCmd = new SqlCommand(imagesQuery, con);
                    imgCmd.Parameters.AddWithValue("@ProductId", id);
                    SqlDataReader imgReader = imgCmd.ExecuteReader();

                    while (imgReader.Read())
                    {
                        product.Product_Images.Add(new Product_Images
                        {
                            image_id = (int)imgReader["image_id"],
                            image_url = (string)imgReader["image_url"],
                            product_id = (int)imgReader["product_id"]
                        });
                    }
                    imgReader.Close();
                }


                // Fetching product images
                if (product != null)
                {
                    SqlCommand categoryCmd = new SqlCommand(categoryQuery, con);
                    categoryCmd.Parameters.AddWithValue("@CategoryID", product.category_id);
                    SqlDataReader categoryReader = categoryCmd.ExecuteReader();

                    while (categoryReader.Read())
                    {
                        product.Category = new Category
                        {
                            category_id = (int)categoryReader["category_id"],
                            category_name = (string)categoryReader["category_name"]
                        };
                    }
                    categoryReader.Close();
                }

                return product;
            }
        }


        // GET: Categories
        public ActionResult Categories()
        {
            List<Category> categories = new CategoriesController().GetCategories();
            return View(categories);
        }

        // GET: Products by Category
        public ActionResult ProductsByCategory(int categoryId, int page = 1, int pageSize = 10)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        Products.product_id
                    FROM
                        Products
                    WHERE
                        category_id = @CategoryId
                    ORDER BY
                        product_name OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                List<Product> products = new List<Product>();
                while (reader.Read())
                {
                    Product product = GetProductByID((int)reader["product_id"]);
                    products.Add(product);
                }
                ViewBag.CategoryId = categoryId;
                ViewBag.CurrentPage = page;
                return View(products);
            }
        }


        private List<Product> GetRelatedProducts(int currentProductId)
        {
            var relatedProducts = new List<Product>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        TOP 10 p.product_id
                    FROM
                        Products p
                    WHERE
                        p.category_id = (
                            SELECT
                                category_id
                            FROM
                                Products
                            WHERE
                                product_id = @CurrentProductId
                        )
                        AND p.product_id != @CurrentProductId
                    ORDER BY
                        NEWID();
                ";


                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CurrentProductId", currentProductId);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = GetProductByID((int)reader["product_id"]);
                            relatedProducts.Add(product);
                        }
                    }
                }
            }

            return relatedProducts;
        }


        // GET: Product Details
        public ActionResult Details(int id)
        {
            Product product = GetProductByID(id);
            var relatedProducts = GetRelatedProducts(id);

            ViewBag.RelatedProducts = relatedProducts;
            return View(product);
        }


        // GET: Search Products
        public ActionResult Search(string query, decimal? minPrice, decimal? maxPrice, int[] selectedCategories = null, string[] selectedBrands = null, int page = 1, int pageSize = 6)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                // SQL Query with conditional checks and pagination
                string sqlQuery = @"
                    SELECT
                        product_id
                    FROM
                        Products
                    WHERE
                        (
                            @Query IS NULL
                            OR product_name LIKE '%' + @Query + '%'
                            OR description LIKE '%' + @Query + '%'
                            OR brand LIKE '%' + @Query + '%'
                        )
                        AND (
                            @MinPrice IS NULL
                            OR price >= @MinPrice
                        )
                        AND (
                            @MaxPrice IS NULL
                            OR price <= @MaxPrice
                        )";


                // Adding category filter
                if (selectedCategories != null && selectedCategories.Length > 0)
                {
                    sqlQuery += " AND category_id IN (" + string.Join(",", selectedCategories) + ")";
                }

                // Adding brand filter
                if (selectedBrands != null && selectedBrands.Length > 0)
                {
                    sqlQuery += " AND brand IN ('" + string.Join("','", selectedBrands) + "')";
                }

                sqlQuery += @" 
                    ORDER BY
                        product_name OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";


                SqlCommand cmd = new SqlCommand(sqlQuery, con);

                // Add parameters with null checks
                cmd.Parameters.AddWithValue("@Query", string.IsNullOrEmpty(query) ? (object)DBNull.Value : query);
                cmd.Parameters.AddWithValue("@MinPrice", minPrice.HasValue ? (object)minPrice.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@MaxPrice", maxPrice.HasValue ? (object)maxPrice.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                // Execute the query and process results
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                List<Product> products = new List<Product>();
                while (reader.Read())
                {
                    products.Add(GetProductByID((int)reader["product_id"]));
                }
                reader.Close();

                // Pass relevant data to the view
                ViewBag.Query = query;
                ViewBag.MinPrice = minPrice;
                ViewBag.MaxPrice = maxPrice;
                ViewBag.CurrentPage = page;

                ViewBag.SelectedCategories = selectedCategories ?? new int[0];
                ViewBag.SelectedBrands = selectedBrands ?? new string[0];
                ViewBag.Brands = GetBrands();

                ViewBag.BestSellingProducts = new HomeController().GetBestSellingProducts();
                return View(products);
            }
        }

        public List<string> GetBrands()
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        DISTINCT brand
                    FROM
                        Products
                    WHERE
                        brand IS NOT NULL;";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                List<string> brands = new List<string>();
                while (reader.Read())
                {
                    brands.Add((string)reader["brand"]);
                }
                return brands;
            }
        }


    }

}