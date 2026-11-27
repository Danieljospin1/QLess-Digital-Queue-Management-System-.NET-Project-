using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using QLess.Data;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace QLess.Pages.Account
{
    public class CustomerRegisterModel : PageModel
    {
        private readonly DbConnectionFactory _connectionFactory;

        public CustomerRegisterModel(DbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [BindProperty]
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [BindProperty]
        public string? PhoneNumber { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must contain at least 8 characters.")]
        public string Password { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

        public string? SuccessMessage { get; set; }
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

            FullName = FullName.Trim();
            Email = Email.Trim();
            PhoneNumber = PhoneNumber?.Trim();


            // 2. Hash the password using BCrypt

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(Password);


            try
            {
                // 3. Connect to SQL Server

                await using SqlConnection connection =
                    _connectionFactory.CreateConnection();

                await connection.OpenAsync();


                // 4. Insert customer

                string query = @"
                    INSERT INTO Users
                        (FullName, Email, PhoneNumber,
                         PasswordHash, Role, OrganizationId)
                    VALUES
                        (@FullName, @Email, @PhoneNumber,
                         @PasswordHash, 'CUSTOMER', NULL);";

                await using SqlCommand command =
                    new SqlCommand(query, connection);

                command.Parameters.Add(
                    "@FullName", SqlDbType.NVarChar, 150)
                    .Value = FullName;

                command.Parameters.Add(
                    "@Email", SqlDbType.NVarChar, 150)
                    .Value = Email;

                command.Parameters.Add(
                    "@PhoneNumber", SqlDbType.NVarChar, 20)
                    .Value = (object?)PhoneNumber ?? DBNull.Value;

                command.Parameters.Add(
                    "@PasswordHash", SqlDbType.NVarChar, 255)
                    .Value = passwordHash;

                await command.ExecuteNonQueryAsync();


                // 5. Registration successful

                SuccessMessage =
                    "Your customer account has been created successfully.";

                // Clear password fields

                Password = "";
                ConfirmPassword = "";
                FullName = "";
                Email = "";
                PhoneNumber = "";

                return Page();
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "An account with this email address already exists.");

                return Page();
            }
        }
    }
}
