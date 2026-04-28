using ElectronicsShop.Models;
using ElectronicsShop.Helpers;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Mvc;
using System;

namespace ElectronicsShop.Controllers
{
    public class HomeController : Customer_BaseController
    {
		private readonly string connectionString = ConnectionStrings.DefaultConnection;

		private ProductController productController = new ProductController();

        public ActionResult Index()
        {
            var bestSellingProducts = GetBestSellingProducts();
            var newProducts = GetNewProducts();
            var categories = GetAllCategories();

			ViewBag.BestSellingProducts = bestSellingProducts;
            ViewBag.NewProducts = newProducts;
            ViewBag.Categories = categories;
			return View();
        }

        private List<Category> GetAllCategories()
        {
            var categories = new List<Category>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                                    SELECT
                                        category_id,
                                        category_name
                                    FROM
                                        Categories";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Category category = new Category
                            {
                                category_id = (int)reader["category_id"],
                                category_name = (string)reader["category_name"]
                            };
                            categories.Add(category);
                        }
                    }
                }
            }
            return categories;
		}

		public List<Product> GetBestSellingProducts()
        {
            var bestSellingProducts = new List<Product>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string  query = @"
                                SELECT
                                    TOP 10 Products.product_id,
                                    SUM(Order_Items.quantity) AS TotalSold
                                FROM
                                    Products
                                JOIN 
                                    Order_Items ON Products.product_id = Order_Items.product_id
                                GROUP BY
                                    Products.product_id
                                ORDER BY
                                    TotalSold DESC;";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = productController.GetProductByID((int)reader["product_id"]);
                            bestSellingProducts.Add(product);
                        }
                    }
                }
            }

            return bestSellingProducts;
        }

        private List<Product> GetNewProducts()
        {
            var newProducts = new List<Product>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        TOP 10 Products.product_id
                    FROM
                        Products
                    WHERE
                        is_new = 1
                    ORDER BY
                        product_id DESC
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = productController.GetProductByID((int)reader["product_id"]);
                            newProducts.Add(product);
                        }
                    }
                }
            }

            return newProducts;
        }

        // Lấy danh sách wishlist cho user tương tự GetCartItemsByUserId ở CartController
        public List<Wishlist> GetWishListItemsByUserId(int userId)
        {
            var wishlists = new List<Wishlist>();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        w.*
                    FROM
                        Wishlist w
                        JOIN Products p ON w.product_id = p.product_id
                    WHERE
                        w.user_id = @UserId";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    con.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wishlists.Add(new Wishlist
                            {
                                wishlist_id = (int)reader["wishlist_id"],
                                user_id = (int)reader["user_id"],
                                product_id = (int)reader["product_id"],
                                Product = productController.GetProductByID((int)reader["product_id"])
                            });
                        }
                    }
                }
            }
            return wishlists;
        }
    }
}