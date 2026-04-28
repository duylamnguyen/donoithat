using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Net.Http.Headers;

namespace ElectronicsShop.Controllers
{
	public class ChatController : Controller
	{
		private static readonly HttpClient _http = new HttpClient();
		private static readonly MemoryCache _cache = MemoryCache.Default;
		private const int WINDOW_SECONDS = 60;
		private const int MAX_PER_WINDOW = 20;

		[HttpPost]
		public async Task<JsonResult> Send(string message, string conversationId = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				return Json(new { ok = false, error = "Message is empty" });

			var hfToken = ConfigurationManager.AppSettings["chatBotAPIKey"];
			if (string.IsNullOrWhiteSpace(hfToken))
				return Json(new { ok = false, error = "Server not configured" });

			// rate limit
			var ip = Request.UserHostAddress ?? "anon";
			var rlKey = "rl:" + ip;
			var count = (_cache.Get(rlKey) as int?) ?? 0;
			if (count >= MAX_PER_WINDOW)
				return Json(new { ok = false, error = "Too many requests" });

			_cache.Set(rlKey, count + 1, DateTimeOffset.UtcNow.AddSeconds(WINDOW_SECONDS));

			try
			{
				var payload = new
				{
					model = "meta-llama/Meta-Llama-3-8B-Instruct",
					messages = new[]
					{
						new { role = "system", content = "Bạn là trợ lý AI. Trả lời ngắn gọn, rõ ràng." },
						new { role = "user", content = message }
					},
					temperature = 0.7,
					max_tokens = 512
				};

				var req = new HttpRequestMessage(
					HttpMethod.Post,
					"https://router.huggingface.co/v1/chat/completions"
				);

				req.Headers.Authorization =
					new AuthenticationHeaderValue("Bearer", hfToken);

				req.Content = new StringContent(
					JsonConvert.SerializeObject(payload),
					Encoding.UTF8,
					"application/json"
				);

				var resp = await _http.SendAsync(req);
				var text = await resp.Content.ReadAsStringAsync();

				if (!resp.IsSuccessStatusCode)
					return Json(new { ok = false, error = "LLaMA response error", details = text });

				// OpenAI-compatible response
				dynamic parsed = JsonConvert.DeserializeObject(text);
				string reply = parsed.choices[0].message.content;

				return Json(new
				{
					ok = true,
					reply,
					conversationId = conversationId ?? Guid.NewGuid().ToString()
				});
			}
			catch (Exception ex)
			{
				return Json(new { ok = false, error = ex.Message });
			}
		}
	}
}
