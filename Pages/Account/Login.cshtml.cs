
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using QLess.Data;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;

namespace QLess.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly DbConnectionFactory _connectionFactory;

        public LoginModel(DbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [BindProperty]
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = "";

        [BindProperty]
        public bool RememberMe { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // 1. Validate submitted information

            if (!ModelState.IsValid)
            {
                return Page();
            }

            Email = Email.Trim();

            try
            {
                // 2. Retrieve user from database

                await using SqlConnection connection =
                    _connectionFactory.CreateConnection();

                await connection.OpenAsync();

                string query = @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        PasswordHash,
                        Role,
                        OrganizationId
                    FROM Users
                    WHERE Email = @Email;";

                await using SqlCommand command =
                    new SqlCommand(query, connection);

                command.Parameters.Add(
                    "@Email", SqlDbType.NVarChar, 150)
                    .Value = Email;

                await using SqlDataReader reader =
                    await command.ExecuteReaderAsync();

                // 3. Check whether user exists

                if (!await reader.ReadAsync())
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Invalid email or password.");

                    return Page();
                }

                // 4. Retrieve user information

                int userId = reader.GetInt32(
                    reader.GetOrdinal("UserId"));

                string fullName = reader.GetString(
                    reader.GetOrdinal("FullName"));

                string email = reader.GetString(
                    reader.GetOrdinal("Email"));

                string passwordHash = reader.GetString(
                    reader.GetOrdinal("PasswordHash"));

                string role = reader.GetString(
                    reader.GetOrdinal("Role"));

                int? organizationId = reader.IsDBNull(
                    reader.GetOrdinal("OrganizationId"))
                    ? null
                    : reader.GetInt32(
                        reader.GetOrdinal("OrganizationId"));

                await reader.CloseAsync();

                // 5. Verify password using BCrypt

                bool passwordIsValid =
                    BCrypt.Net.BCrypt.Verify(
                        Password,
                        passwordHash);

                if (!passwordIsValid)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Invalid email or password.");

                    return Page();
                }

                // 6. Create authentication claims

                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        fullName),

                    new Claim(
                        ClaimTypes.Email,
                        email),

                    new Claim(
                        ClaimTypes.Role,
                        role)
                };

                // OrganizationId is only added for organization users

                if (organizationId.HasValue)
                {
                    claims.Add(new Claim(
                        "OrganizationId",
                        organizationId.Value.ToString()));
                }

                var identity = new ClaimsIdentity(
                    claims,
                    "QLessCookie");

                var principal = new ClaimsPrincipal(identity);

                // 7. Configure authentication cookie

                var properties = new AuthenticationProperties
                {
                    IsPersistent = RememberMe,
                    AllowRefresh = true
                };

                // 8. Sign in the user

                await HttpContext.SignInAsync(
                    "QLessCookie",
                    principal,
                    properties);

                // 9. Redirect according to user role

                switch (role)
                {
                    case "CUSTOMER":
                        return RedirectToPage("/Client/Index");

                    case "EMPLOYEE":
                        return RedirectToPage("/Employee/Index");

                    case "ADMIN":
                        return RedirectToPage("/Admin/Index");

                    default:
                        await HttpContext.SignOutAsync("QLessCookie");

                        ModelState.AddModelError(
                            string.Empty,
                            "Your account has an invalid role.");

                        return Page();
                }
            }
            catch (SqlException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to connect to the database. Please try again later.");

                return Page();
            }
        }
    }
}
