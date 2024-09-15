using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace bankidbackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public LoginController(IConfiguration config)
        {
            _config = config;
            _httpClient = new HttpClient();
        }

        // Endpoint to initiate the login process
        [HttpPost("start-login")]
        public async Task<IActionResult> StartLogin()
        {
            var domain = _config["GrandIdSettings:Domain"];
            var apiKey = _config["GrandIdSettings:ApiKey"];
            var authenticateServiceKey = _config["GrandIdSettings:AuthenticateServiceKey"];
            var callbackUrl =  "http://localhost:5173/callback";
            //var callbackUrl = Url.Action("Callback", "Login", null, Request.Scheme);

            var url = $"{domain}/json1.1/FederatedLogin";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("apiKey", apiKey),
                new KeyValuePair<string, string>("authenticateServiceKey", authenticateServiceKey),
                new KeyValuePair<string, string>("callbackUrl", callbackUrl)
            });

            var response = await _httpClient.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);

            if (responseObject["errorObject"] != null)
            {
                return BadRequest(responseObject["errorObject"]["message"]?.ToString());
            }

            var sessionId = responseObject["sessionId"]?.ToString();
            var redirectUrl = responseObject["redirectUrl"]?.ToString();

            if (string.IsNullOrEmpty(redirectUrl))
            {
                return BadRequest("Failed to initiate login.");
            }

            // Return the redirect URL to the client
            return Ok(new { RedirectUrl = redirectUrl, SessionId = sessionId });
        }

        // Callback endpoint to handle the response from the authentication provider
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string grandidsession)
        {
            if (string.IsNullOrEmpty(grandidsession))
            {
                return BadRequest("Missing expected parameter grandidsession");
            }

            var domain = _config["GrandIdSettings:Domain"];
            var apiKey = _config["GrandIdSettings:ApiKey"];
            var authenticateServiceKey = _config["GrandIdSettings:AuthenticateServiceKey"];

            var url = $"{domain}/json1.1/GetSession?apiKey={apiKey}&authenticateServiceKey={authenticateServiceKey}&sessionId={grandidsession}";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var responseObject = JObject.Parse(response);
                var userAttributes = responseObject["userAttributes"]?.ToObject<Dictionary<string, string>>();
                // Debugging output
                Console.WriteLine("Response from GetSession API:");
                Console.WriteLine(response);

                // Ensure that responseObject has the correct format
                var result = new
                {
                    sessionId = responseObject["sessionId"]?.ToString(),
                    username = responseObject["username"]?.ToString(),
                    userAttributes = userAttributes
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine("Error fetching session details:");
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }

    }
}

