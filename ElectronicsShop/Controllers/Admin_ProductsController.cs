using System.Data;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.IO;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    public class Admin_ProductsController : Controller
    {
        private Db_ElectronicsShop db = new Db_ElectronicsShop();

        // GET: Products
        public ActionResult Index()
        {
            if ((string)Session["role"] != "admin")
            {
                return RedirectToAction("Index", "Home");
            }
            var products = db.Products.Include(p => p.Category);
            return View(products.ToList());
        }

        // GET: Products/Details/5
        public ActionResult Details(int? id)
        {
            if ((string)Session["role"] != "admin")
            {
                return RedirectToAction("Index", "Home");
            }
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // GET: Products/Create
        public ActionResult Create()
        {
            if ((string)Session["role"] != "admin")
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.category_id = new SelectList(db.Categories, "category_id", "category_name");
            return View();
        }

        // POST: Products/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "product_id,category_id,product_name,description,price,discount_price,stock,brand,is_new,material,size,status,color,warranty")] Product product)
        {
            if (ModelState.IsValid)
            {
                if ((string)Session["role"] != "admin")
                {
                    return RedirectToAction("Index", "Home");
                }

                // Save product first to obtain product_id
                db.Products.Add(product);
                db.SaveChanges();

                // Handle uploaded files (any number)
                var files = Request.Files;
                var addedAny = false;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0)
                    {
                        var fileName = Path.GetFileName(file.FileName);
                        var path = Server.MapPath("~/Images/" + fileName);
                        file.SaveAs(path);

                        Product_Images pi = new Product_Images
                        {
                            product_id = product.product_id,
                            image_url = fileName
                        };
                        db.Product_Images.Add(pi);
                        addedAny = true;
                    }
                }

                // If no valid uploads, add one default image
                if (!addedAny)
                {
                    Product_Images pi = new Product_Images
                    {
                        product_id = product.product_id,
                        image_url = "noimage.png"
                    };
                    db.Product_Images.Add(pi);
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.category_id = new SelectList(db.Categories, "category_id", "category_name", product.category_id);
            return View(product);
        }

        // GET: Products/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            ViewBag.category_id = new SelectList(db.Categories, "category_id", "category_name", product.category_id);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "product_id,category_id,product_name,description,price,discount_price,stock,brand,is_new,material,size,status,color,warranty")] Product product)
        {
            if (ModelState.IsValid)
            {
                // Update product fields
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();

                // Handle uploaded files: add each uploaded file as a new Product_Images entry
                var files = Request.Files;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0)
                    {
                        var fileName = Path.GetFileName(file.FileName);
                        var path = Server.MapPath("~/Images/" + fileName);
                        file.SaveAs(path);

                        Product_Images pi = new Product_Images
                        {
                            product_id = product.product_id,
                            image_url = fileName
                        };
                        db.Product_Images.Add(pi);
                    }
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.category_id = new SelectList(db.Categories, "category_id", "category_name", product.category_id);
            return View(product);
        }

        // New: delete single image (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteImage(int id)
        {
            var pi = db.Product_Images.Find(id);
            if (pi == null)
            {
                return Json(new { success = false, message = "Không tìm thấy ảnh." });
            }

            // Remove DB record
            db.Product_Images.Remove(pi);
            db.SaveChanges();

            // Try delete file from disk if not default
            try
            {
                if (!string.IsNullOrEmpty(pi.image_url) && pi.image_url.ToLower() != "noimage.png")
                {
                    var fullPath = Server.MapPath("~/Images/" + pi.image_url);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
            }
            catch
            {
                // ignore file delete errors
            }

            return Json(new { success = true });
        }

        // GET: Products/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Product product = db.Products.Find(id);
            db.Products.Remove(product);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        DataUtil data = new DataUtil();
        public ActionResult SellingProduct()
        {
            var selling = data.GetSellingProduct();
            return View(selling);
        }
        public ActionResult UnsoldProduct()
        {
            var Unsold = data.GetUnsoldProduct();
            return View(Unsold);
        }
    }
}
