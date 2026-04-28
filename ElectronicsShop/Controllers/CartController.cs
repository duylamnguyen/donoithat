using ElectronicsShop.Helpers;
using ElectronicsShop.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace ElectronicsShop.Controllers
{
	public class CartController : Customer_BaseController
	{
		private readonly string connectionString = ConnectionStrings.DefaultConnection;

		// GET: Cart
		public ActionResult Index()
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}

			int userId = (int)Session["UserId"];

			List<Cart> carts = GetCartItemsByUserId(userId);
			return View(carts);
		}

		// POST: Add to Cart (server form)
		[HttpPost]
		public ActionResult AddToCart(int productId, int quantity)
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}

			int userId = (int)Session["UserId"];

			using (SqlConnection con = new SqlConnection(connectionString))
			{
				con.Open();

				if (!IsStockAvailable(con, productId, quantity))
				{
					TempData["Error"] = "Vượt quá số lượng trong kho.";
					return RedirectToAction("Index");
				}

				int? currentCartQuantity = GetCurrentCartQuantity(con, userId, productId);

				if (currentCartQuantity.HasValue)
				{
					int newQuantity = currentCartQuantity.Value + quantity;
					if (newQuantity > GetProductStock(con, productId))
					{
						TempData["Error"] = "Vượt quá số lượng trong kho.";
						return RedirectToAction("Index");
					}

					UpdateCart(con, userId, productId, newQuantity);
				}
				else
				{
					InsertIntoCart(con, userId, productId, quantity);
				}
			}

			return RedirectToAction("Index", "Home");
		}

		// POST: Add to Cart (AJAX-compatible)
		[HttpPost]
		public ActionResult Add(int productId, int quantity = 1)
		{
			if (Session["UserId"] == null)
			{
				if (Request.IsAjaxRequest())
				{
					return Json(new { success = false, redirectUrl = Url.Action("Login", "Account") });
				}
				return RedirectToAction("Login", "Account");
			}

			int userId = (int)Session["UserId"];
			bool stockOk = true;
			using (SqlConnection con = new SqlConnection(connectionString))
			{
				con.Open();

				if (!IsStockAvailable(con, productId, quantity))
				{
					stockOk = false;
				}
				else
				{
					int? currentCartQuantity = GetCurrentCartQuantity(con, userId, productId);

					if (currentCartQuantity.HasValue)
					{
						int newQuantity = currentCartQuantity.Value + quantity;
						if (newQuantity > GetProductStock(con, productId))
						{
							stockOk = false;
						}
						else
						{
							UpdateCart(con, userId, productId, newQuantity);
						}
					}
					else
					{
						InsertIntoCart(con, userId, productId, quantity);
					}
				}
			}

			if (Request.IsAjaxRequest())
			{
				if (!stockOk)
				{
					return Json(new { success = false, message = "Vượt quá số lượng trong kho." });
				}
				var cartsNow = GetCartItemsByUserId(userId);
				var html = RenderPartialViewToString("_CartItems", cartsNow);
				int quantityCount = (int)cartsNow.Sum(c => c.quantity);
				int rowCount = cartsNow.Count;
				decimal subtotal = (decimal)cartsNow.Sum(c => (c.quantity * (c.Product?.discount_price ?? c.Product?.price ?? 0M)));
				return Json(new { success = true, quantityCount = quantityCount, rowCount = rowCount, cartSubtotal = subtotal, html = html });
			}

			return RedirectToAction("Index", "Home");
		}

		private bool IsStockAvailable(SqlConnection con, int productId, int requestedQuantity)
		{
			int currentStock = GetProductStock(con, productId);
			return currentStock >= requestedQuantity;
		}

		private int GetProductStock(SqlConnection con, int productId)
		{
			string query = @"
                SELECT
                    stock
                FROM
                    Products
                WHERE
                    product_id = @ProductId";
			using (SqlCommand cmd = new SqlCommand(query, con))
			{
				cmd.Parameters.AddWithValue("@ProductId", productId);
				return (int)cmd.ExecuteScalar();
			}
		}

		private int? GetCurrentCartQuantity(SqlConnection con, int userId, int productId)
		{
			string query = @"
                SELECT
                    quantity
                FROM
                    Cart
                WHERE
                    user_id = @UserId
                    AND product_id = @ProductId";

			using (SqlCommand cmd = new SqlCommand(query, con))
			{
				cmd.Parameters.AddWithValue("@UserId", userId);
				cmd.Parameters.AddWithValue("@ProductId", productId);

				object result = cmd.ExecuteScalar();
				return result != null ? (int?)result : null;
			}
		}

		private void UpdateCart(SqlConnection con, int userId, int productId, int quantity)
		{
			string query = @"
                UPDATE
                    Cart
                SET
                    quantity = @Quantity
                WHERE
                    user_id = @UserId
                    AND product_id = @ProductId";

			using (SqlCommand cmd = new SqlCommand(query, con))
			{
				cmd.Parameters.AddWithValue("@Quantity", quantity);
				cmd.Parameters.AddWithValue("@UserId", userId);
				cmd.Parameters.AddWithValue("@ProductId", productId);

				cmd.ExecuteNonQuery();
			}
		}

		private void InsertIntoCart(SqlConnection con, int userId, int productId, int quantity)
		{
			string query = @"
                INSERT INTO
                    Cart (user_id, product_id, quantity)
                VALUES
                    (@UserId, @ProductId, @Quantity)";

			using (SqlCommand cmd = new SqlCommand(query, con))
			{
				cmd.Parameters.AddWithValue("@UserId", userId);
				cmd.Parameters.AddWithValue("@ProductId", productId);
				cmd.Parameters.AddWithValue("@Quantity", quantity);

				cmd.ExecuteNonQuery();
			}
		}

		// POST: Remove from Cart (supports AJAX)
		[HttpPost]
		public ActionResult Remove(int cartId)
		{
			if (Session["UserId"] == null)
			{
				if (Request.IsAjaxRequest())
				{
					return Json(new { success = false, redirectUrl = Url.Action("Login", "Account") });
				}
				return RedirectToAction("Login", "Account");
			}

			int removedProductId = 0;
			int userId = (int)Session["UserId"];

			using (SqlConnection con = new SqlConnection(connectionString))
			{
				con.Open();

				// attempt to read product_id for UI update
				string selectQuery = @"
                    SELECT product_id, user_id
                    FROM Cart
                    WHERE cart_id = @CartId";
				using (SqlCommand cmdSel = new SqlCommand(selectQuery, con))
				{
					cmdSel.Parameters.AddWithValue("@CartId", cartId);
					using (var rdr = cmdSel.ExecuteReader())
					{
						if (rdr.Read())
						{
							removedProductId = rdr["product_id"] == DBNull.Value ? 0 : (int)rdr["product_id"];
							// ensure the cart row belongs to current user; if not, prevent delete below
							userId = rdr["user_id"] == DBNull.Value ? userId : (int)rdr["user_id"];
						}
					}
				}

				// Delete
				string query = @"
                    DELETE FROM
                        Cart
                    WHERE
                        cart_id = @CartId";
				using (SqlCommand cmd = new SqlCommand(query, con))
				{
					cmd.Parameters.AddWithValue("@CartId", cartId);
					cmd.ExecuteNonQuery();
				}
			}

			if (Request.IsAjaxRequest())
			{
				var cartsNow = GetCartItemsByUserId((int)Session["UserId"]);
				var html = RenderPartialViewToString("_CartItems", cartsNow);
				int quantityCount = (int)cartsNow.Sum(c => c.quantity);
				int rowCount = cartsNow.Count;
				decimal subtotal = (decimal)cartsNow.Sum(c => (c.quantity * (c.Product?.discount_price ?? c.Product?.price ?? 0M)));
				return Json(new { success = true, quantityCount = quantityCount, rowCount = rowCount, cartSubtotal = subtotal, html = html, removedProductId = removedProductId });
			}

			return RedirectToAction("Index", "Home");
		}

		public List<Cart> GetCartItemsByUserId(int userId)
		{
			List<Cart> carts = new List<Cart>();

			using (SqlConnection con = new SqlConnection(connectionString))
			{
				string query = @"
                    SELECT
                        *
                    FROM
                        Cart c
                        JOIN Products p ON c.product_id = p.product_id
                    WHERE
                        c.user_id = @UserId";

				SqlCommand cmd = new SqlCommand(query, con);
				cmd.Parameters.AddWithValue("@UserId", userId);

				con.Open();
				SqlDataReader reader = cmd.ExecuteReader();

				while (reader.Read())
				{
					carts.Add(new Cart()
					{
						cart_id = (int)reader["cart_id"],
						user_id = (int)reader["user_id"],
						product_id = (int)reader["product_id"],
						quantity = (int)reader["quantity"],
						Product = new ProductController().GetProductByID((int)reader["product_id"])
					});
				}
				reader.Close();
			}
			return carts;
		}

		public ActionResult Purchase()
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}

			int userId = (int)Session["UserId"];
			User user = new AccountController().GetUserById(userId);

			if (user != null)
			{
				user.Shipments = new ShipmentsController().GetShipmentsByUserId(userId);
				user.Carts = GetCartItemsByUserId(userId);
				if (user.Shipments == null || !user.Shipments.Any())
				{
					user.Shipments = new List<Shipment>
					{
						new Shipment
						{
							shipment_address = "",
							shipment_city = "",
							shipment_country = "",
							shipment_zip_code = ""
						}
					};
				}
			}

			return View(user);
		}

		// POST: Purchase
		[HttpPost]
		public ActionResult Purchase(string recipient_first_name, string recipient_last_name, string recipient_phone, string address, string city, string country, string zipCode, string paymentMethod)
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}
			int userId = (int)Session["UserId"];
			using (SqlConnection con = new SqlConnection(connectionString))
			{
				string queryShipment = @"
                    INSERT INTO
                        Shipments (
                            user_id,
                            recipient_first_name,
                            recipient_last_name,
                            recipient_phone,
                            shipment_address,
                            shipment_city,
                            shipment_country,
                            shipment_zip_code
                        ) OUTPUT INSERTED.shipment_id
                    VALUES
                        (@UserId, @RecipientFirstName, @RecipientLastName, @RecipientPhone, @Address, @City, @Country, @ZipCode)";

				SqlCommand cmdShipment = new SqlCommand(queryShipment, con);
				cmdShipment.Parameters.AddWithValue("@UserId", userId);
				cmdShipment.Parameters.AddWithValue("@RecipientFirstName", recipient_first_name);
				cmdShipment.Parameters.AddWithValue("@RecipientLastName", recipient_last_name);
				cmdShipment.Parameters.AddWithValue("@RecipientPhone", recipient_phone);
				cmdShipment.Parameters.AddWithValue("@Address", address);
				cmdShipment.Parameters.AddWithValue("@City", city);
				cmdShipment.Parameters.AddWithValue("@Country", country);
				cmdShipment.Parameters.AddWithValue("@ZipCode", zipCode);
				con.Open();
				int shipmentId = (int)cmdShipment.ExecuteScalar();

				// totalAmount lấy từ giỏ hàng hiện thời
				decimal totalAmount = Convert.ToDecimal(CalculateTotalAmount(userId, con));

				// Nếu paymentMethod là VNPAY, tạo order với trạng thái PendingPayment,
				// chèn order_items nhưng KHÔNG trừ kho, KHÔNG xóa cart -> sẽ xử lý sau khi VNPAY trả về.
				string initialStatus = paymentMethod != null && paymentMethod.Equals("vnpay", StringComparison.OrdinalIgnoreCase)
					? "PendingPayment"
					: "Processing";

				string queryOrder = @"
                    INSERT INTO
                        Orders (
                            shipment_id,
                            user_id,
                            order_date,
                            total_amount,
                            status,
                            payment_method
                        ) OUTPUT INSERTED.order_id
                    VALUES
                        (
                            @ShipmentId,
                            @UserId,
                            @OrderDate,
                            @TotalAmount,
                            @Status,
                            @PaymentMethod
                        )";

				SqlCommand cmdOrder = new SqlCommand(queryOrder, con);
				cmdOrder.Parameters.AddWithValue("@ShipmentId", shipmentId);
				cmdOrder.Parameters.AddWithValue("@UserId", userId);
				cmdOrder.Parameters.AddWithValue("@OrderDate", DateTime.Now);
				cmdOrder.Parameters.AddWithValue("@TotalAmount", totalAmount);
				cmdOrder.Parameters.AddWithValue("@Status", initialStatus);
				cmdOrder.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
				int orderId = (int)cmdOrder.ExecuteScalar();

				string queryOrderItems = @"
                    INSERT INTO
                        Order_Items (order_id, product_id, quantity, price)
                    SELECT
                        @OrderId,
                        c.product_id,
                        c.quantity,
                        p.price
                    FROM
                        Cart c
                        INNER JOIN Products p ON c.product_id = p.product_id
                    WHERE
                        c.user_id = @UserId";

				SqlCommand cmdOrderItems = new SqlCommand(queryOrderItems, con);
				cmdOrderItems.Parameters.AddWithValue("@OrderId", orderId);
				cmdOrderItems.Parameters.AddWithValue("@UserId", userId);
				cmdOrderItems.ExecuteNonQuery();

				if (paymentMethod != null && paymentMethod.Equals("vnpay", StringComparison.OrdinalIgnoreCase))
				{
					// Redirect to VNPay with the created order id and amount
					string vnpUrl = Url.Action("VNPay", "Cart", new { totalAmount = totalAmount, orderId = orderId }, protocol: Request.Url.Scheme);
					return Redirect(vnpUrl);
				}

				// Với phương thức thanh toán khi nhận hàng (cod)
				string queryUpdateStock = @"
                    UPDATE
                        Products
                    SET
                        stock = stock - c.quantity
                    FROM
                        Products p
                        INNER JOIN Cart c ON p.product_id = c.product_id
                    WHERE
                        c.user_id = @UserId";

				SqlCommand cmdUpdateStock = new SqlCommand(queryUpdateStock, con);
				cmdUpdateStock.Parameters.AddWithValue("@UserId", userId);
				cmdUpdateStock.ExecuteNonQuery();

				string queryClearCart = @"
                    DELETE FROM
                        Cart
                    WHERE
                        user_id = @UserId";

				SqlCommand cmdClearCart = new SqlCommand(queryClearCart, con);
				cmdClearCart.Parameters.AddWithValue("@UserId", userId);
				cmdClearCart.ExecuteNonQuery();
			}
			return RedirectToAction("OrderHistory");
		}

		// GET: Order History
		public ActionResult OrderHistory()
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}
			int userId = (int)Session["UserId"];
			using (SqlConnection con = new SqlConnection(connectionString))
			{
				string query = @"
                    SELECT
                        o.order_id,
                        o.order_date,
                        o.total_amount,
                        o.status,
                        s.shipment_address,
                        (
                            SELECT
                                COUNT(*)
                            FROM
                                Order_Items oi
                            WHERE
                                oi.order_id = o.order_id
                        ) AS NumberOfProducts
                    FROM
                        Orders o
                        JOIN Shipments s ON o.shipment_id = s.shipment_id
                    WHERE
                        o.user_id = @UserId
                    ORDER BY
                        o.order_date DESC";

				SqlCommand cmd = new SqlCommand(query, con);
				cmd.Parameters.AddWithValue("@UserId", userId);
				con.Open();
				SqlDataReader reader = cmd.ExecuteReader();
				List<Order> orders = new List<Order>();
				Dictionary<int, string> shipmentAddresses = new Dictionary<int, string>();
				Dictionary<int, int> productCounts = new Dictionary<int, int>();
				while (reader.Read())
				{
					int orderId = (int)reader["order_id"];
					orders.Add(new Order
					{
						order_id = orderId,
						order_date = (DateTime)reader["order_date"],
						total_amount = (decimal)reader["total_amount"],
						status = (string)reader["status"]
					});
					shipmentAddresses[orderId] = (string)reader["shipment_address"];
					productCounts[orderId] = (int)reader["NumberOfProducts"];
				}
				ViewBag.ShipmentAddresses = shipmentAddresses;
				ViewBag.ProductCounts = productCounts;
				return View(orders);
			}
		}

		// GET: Order Details
		public ActionResult OrderDetails(int id)
		{
			if (Session["UserId"] == null)
			{
				return RedirectToAction("Login", "Account");
			}
			using (SqlConnection con = new SqlConnection(connectionString))
			{
				string query = @"
                    SELECT
                        oi.order_item_id,
                        oi.quantity,
                        oi.price,
                        oi.product_id,
						o.user_id,
						o.shipment_id,
						o.status
                    FROM
                        Order_Items oi
                        JOIN Products p ON oi.product_id = p.product_id
						JOIN Orders o ON oi.order_id = o.order_id
                    WHERE
                        oi.order_id = @OrderId";

				SqlCommand cmd = new SqlCommand(query, con);
				cmd.Parameters.AddWithValue("@OrderId", id);
				con.Open();
				SqlDataReader reader = cmd.ExecuteReader();
				List<Order_Items> orderDetails = new List<Order_Items>();
				while (reader.Read())
					orderDetails.Add(new Order_Items
					{
						order_item_id = (int)reader["order_item_id"],
						quantity = (int)reader["quantity"],
						price = (decimal)reader["price"],
						Order = new Order
						{
							status = (string)reader["status"],
							User = new AccountController().GetUserById((int)reader["user_id"]),
							Shipment = new ShipmentsController().GetShipmentById((int)reader["shipment_id"])
						},
						Product = new ProductController().GetProductByID((int)reader["product_id"])
					});
				return View(orderDetails);
			}
		}

		private decimal CalculateTotalAmount(int userId, SqlConnection con)
		{
			string query = @"
                SELECT
                    SUM(c.quantity * p.price)
                FROM
                    Cart c
                    JOIN Products p ON c.product_id = p.product_id
                WHERE
                    c.user_id = @UserId";

			SqlCommand cmd = new SqlCommand(query, con);
			cmd.Parameters.AddWithValue("@UserId", userId);
			return (decimal)cmd.ExecuteScalar();
		}

		public ActionResult VNPay(decimal totalAmount, int orderId)
		{
			string vnp_Url = ConfigurationManager.AppSettings["VNPAY:BaseUrl"];
			string vnp_TmnCode = ConfigurationManager.AppSettings["VNPAY:TmnCode"];
			string vnp_HashSecret = ConfigurationManager.AppSettings["VNPAY:HashSecret"];
			string vnp_ReturnUrl = ConfigurationManager.AppSettings["VNPAY:CallbackUrl"];

			if (string.IsNullOrWhiteSpace(vnp_Url) ||
				string.IsNullOrWhiteSpace(vnp_TmnCode) ||
				string.IsNullOrWhiteSpace(vnp_HashSecret) ||
				string.IsNullOrWhiteSpace(vnp_ReturnUrl))
			{
				return new HttpStatusCodeResult(400, "VNPAY configuration missing");
			}

			var vnPay = new VnPayLibrary();
			DateTime now = DateTime.Now;
			string txnRef = orderId.ToString();
			string orderInfo = $"Thanh toán đơn hàng #{orderId}";
			string locale = "vn";
			string ipAddr = Utils.GetIpAddress();
			if (string.IsNullOrEmpty(ipAddr))
			{
				ipAddr = Request.UserHostAddress;
			}

			// VNPAY yêu cầu amount là số nguyên (đơn vị: đồng * 100)
			long amountInt = Convert.ToInt64(Math.Round(totalAmount * 100M));

			vnPay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
			vnPay.AddRequestData("vnp_Command", "pay");
			vnPay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
			vnPay.AddRequestData("vnp_Amount", amountInt.ToString());
			vnPay.AddRequestData("vnp_CurrCode", "VND");
			vnPay.AddRequestData("vnp_TxnRef", txnRef);
			vnPay.AddRequestData("vnp_OrderInfo", orderInfo);
			vnPay.AddRequestData("vnp_OrderType", "other");
			vnPay.AddRequestData("vnp_Locale", locale);
			vnPay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);
			vnPay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
			vnPay.AddRequestData("vnp_IpAddr", ipAddr);

			string paymentUrl = vnPay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
			return Redirect(paymentUrl);
		}

		[HttpGet]
		public ActionResult ConfirmPayment(string orderId)
		{
			string vnp_HashSecret = ConfigurationManager.AppSettings["VNPAY:HashSecret"];
			if (string.IsNullOrWhiteSpace(vnp_HashSecret))
			{
				TempData["VnPayResult"] = "VNPAY configuration missing.";
				return RedirectToAction("OrderHistory");
			}

			// Thu thập các tham số vnp_* từ QueryString vào VnPayLibrary
			var vnPay = new VnPayLibrary();
			if (Request.QueryString != null)
			{
				foreach (string key in Request.QueryString.AllKeys)
				{
					if (string.IsNullOrEmpty(key)) continue;
					// chỉ thêm các key bắt đầu bằng "vnp_" để tránh đưa vào những param khác
					if (key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
					{
						vnPay.AddResponseData(key, Request.QueryString[key]);
					}
				}
			}

			// Lấy chữ ký do VNPAY gửi và validate
			string inputHash = Request.QueryString["vnp_SecureHash"];
			bool validSignature;
			try
			{
				validSignature = vnPay.ValidateSignature(inputHash, vnp_HashSecret);
			}
			catch (Exception ex)
			{
				TempData["VnPayResult"] = "Lỗi xác thực chữ ký: " + ex.Message;
				return RedirectToAction("OrderHistory");
			}

			if (!validSignature)
			{
				TempData["VnPayResult"] = "Chữ ký không hợp lệ từ VNPAY.";
				return RedirectToAction("OrderHistory");
			}

			// Lấy các thông tin cần thiết từ response
			string txnRef = vnPay.GetResponseData("vnp_TxnRef");
			string responseCode = vnPay.GetResponseData("vnp_ResponseCode");
			if (string.IsNullOrEmpty(responseCode))
			{
				// một số sandbox/trường hợp trả về tên khác
				responseCode = vnPay.GetResponseData("vnp_TransactionStatus");
			}
			string vnpAmount = vnPay.GetResponseData("vnp_Amount");
			string vnpBankTranNo = vnPay.GetResponseData("vnp_BankTranNo");
			string vnpTransactionNo = vnPay.GetResponseData("vnp_TransactionNo");
			decimal amountFromVnPay = 0m;
			if (!string.IsNullOrEmpty(vnpAmount) && long.TryParse(vnpAmount, out long amtLong))
			{
				// VNPAY gửi amount = VND * 100
				amountFromVnPay = Convert.ToDecimal(amtLong) / 100M;
			}

			// Xác định order id: ưu tiên vnp_TxnRef, fallback tham số orderId
			int parsedOrderId = 0;
			if (!string.IsNullOrEmpty(txnRef) && int.TryParse(txnRef, out int tmpId))
			{
				parsedOrderId = tmpId;
			}
			if (parsedOrderId == 0 && !string.IsNullOrEmpty(orderId))
			{
				int.TryParse(orderId, out parsedOrderId);
			}

			if (parsedOrderId == 0)
			{
				TempData["VnPayResult"] = "Đơn hàng bị thiếu hoặc không hợp lệ";
				return RedirectToAction("OrderHistory");
			}

			int orderUserId = 0;
			decimal dbTotal = 0m;

			// Kiểm tra order và cập nhật trạng thái
			using (SqlConnection con = new SqlConnection(connectionString))
			{
				con.Open();

				// Lấy total_amount và user_id của order
				const string qryGet = @"SELECT total_amount, user_id FROM Orders WHERE order_id = @OrderId";
				using (SqlCommand cmd = new SqlCommand(qryGet, con))
				{
					cmd.Parameters.AddWithValue("@OrderId", parsedOrderId);
					using (SqlDataReader rdr = cmd.ExecuteReader())
					{
						if (rdr.Read())
						{
							dbTotal = rdr["total_amount"] == DBNull.Value ? 0m : (decimal)rdr["total_amount"];
							orderUserId = rdr["user_id"] == DBNull.Value ? 0 : (int)rdr["user_id"];
						}
						else
						{
							TempData["VnPayResult"] = "Đơn hàng không tồn tại";
							return RedirectToAction("OrderHistory");
						}
					}
				}

				// Nếu có amount trả về, so sánh với DB để tránh gian lận
				if (amountFromVnPay > 0m && Math.Abs(amountFromVnPay - dbTotal) > 0.01m)
				{
					const string qryMismatch = @"
						UPDATE Orders
						SET status = @Status, payment_method = @PaymentMethod
						WHERE order_id = @OrderId";
					using (SqlCommand cmd = new SqlCommand(qryMismatch, con))
					{
						cmd.Parameters.AddWithValue("@Status", "PaymentAmountMismatch");
						cmd.Parameters.AddWithValue("@PaymentMethod", "vnpay");
						cmd.Parameters.AddWithValue("@OrderId", parsedOrderId);
						cmd.ExecuteNonQuery();
					}
					TempData["VnPayResult"] = "Số tiền không khớp giữa đơn hàng và VNPAY";
					// Redirect đến OrderDetails nếu user đăng nhập là chủ order, ngược lại OrderHistory
					if (Session["UserId"] != null && (int)Session["UserId"] == orderUserId)
					{
						return RedirectToAction("OrderDetails", new { id = parsedOrderId });
					}
					return RedirectToAction("OrderHistory");
				}

				// Xử lý theo response code: "00" thường là thành công
				if (responseCode == "00")
				{
					const string qryUpdatePaid = @"
						UPDATE Orders
						SET status = @Status, payment_method = @PaymentMethod
						WHERE order_id = @OrderId";
					using (SqlCommand cmd = new SqlCommand(qryUpdatePaid, con))
					{
						cmd.Parameters.AddWithValue("@Status", "Processing");
						cmd.Parameters.AddWithValue("@PaymentMethod", "vnpay");
						cmd.Parameters.AddWithValue("@OrderId", parsedOrderId);
						cmd.ExecuteNonQuery();
					}

					// Sau khi đánh dấu Paid, trừ kho và xóa cart cho user tương ứng
					const string qryUpdateStock = @"
						UPDATE Products
						SET stock = stock - oi.quantity
						FROM Products p
						INNER JOIN Order_Items oi ON p.product_id = oi.product_id
						WHERE oi.order_id = @OrderId";

					using (SqlCommand cmd = new SqlCommand(qryUpdateStock, con))
					{
						cmd.Parameters.AddWithValue("@OrderId", parsedOrderId);
						cmd.ExecuteNonQuery();
					}

					const string qryClearCart = @"
						DELETE FROM Cart
						WHERE user_id = @UserId";
					using (SqlCommand cmd = new SqlCommand(qryClearCart, con))
					{
						cmd.Parameters.AddWithValue("@UserId", orderUserId);
						cmd.ExecuteNonQuery();
					}
					TempData["VnPayResult"] = "Thanh toán thành công";
				}
				else
				{
					const string qryUpdateFail = @"
						UPDATE Orders
						SET status = @Status, payment_method = @PaymentMethod
						WHERE order_id = @OrderId";
					using (SqlCommand cmd = new SqlCommand(qryUpdateFail, con))
					{
						cmd.Parameters.AddWithValue("@Status", "Failed");
						cmd.Parameters.AddWithValue("@PaymentMethod", "vnpay");
						cmd.Parameters.AddWithValue("@OrderId", parsedOrderId);
						cmd.ExecuteNonQuery();
					}
					TempData["VnPayResult"] = "Thanh toán thất bại hoặc bị hủy";
				}
			}

			// Redirect hợp lý
			if (Session["UserId"] != null && (int)Session["UserId"] == orderUserId)
			{
				return RedirectToAction("OrderDetails", new { id = parsedOrderId });
			}
			return RedirectToAction("OrderHistory");
		}

		private string RenderPartialViewToString(string viewName, object model)
		{
			ViewData.Model = model;
			using (var sw = new StringWriter())
			{
				var viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
				var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);
				viewResult.View.Render(viewContext, sw);
				viewResult.ViewEngine.ReleaseView(ControllerContext, viewResult.View);
				return sw.GetStringBuilder().ToString();
			}
		}
	}
}