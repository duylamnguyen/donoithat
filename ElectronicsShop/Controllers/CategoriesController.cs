using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Mvc;
using ElectronicsShop.Models;
using System.Configuration;

namespace ElectronicsShop.Controllers
{
    public class CategoriesController : Customer_BaseController
    {
        // Database connection string
        //private string connectionString = "Data Source=.\\SQLEXPRESS;Initial Catalog=thuongmaidientudb;Integrated Security=True;TrustServerCertificate=True";
        string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        // GET: Categories
        public ActionResult Index()
        {
            return View();
        }

        public List<Category> GetCategories()
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        category_id,
                        category_name
                    FROM
                        Categories";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                List<Category> categories = new List<Category>();
                while (reader.Read())
                {
                    categories.Add(new Category
                    {
                        category_id = (int)reader["category_id"],
                        category_name = (string)reader["category_name"]
                    });
                }
                return categories;
            }
        }
    }
}