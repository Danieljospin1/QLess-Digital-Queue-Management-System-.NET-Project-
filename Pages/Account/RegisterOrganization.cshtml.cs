using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using QLess.Data;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace QLess.Pages.Account
{
    public class RegisterOrganizationModel : PageModel
    {
        private readonly DbConnectionFactory _connectionFactory;

        public RegisterOrganizationModel(DbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [BindProperty]
        [Required(ErrorMessage = "Organization name is required.")]
        public string OrganizationName { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Organization email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid organization email.")]
        public string OrganizationEmail { get; set; } = "";

        [BindProperty]
        public string? OrganizationPhone { get; set; }

        [BindProperty]
        public string? OrganizationAddress { get; set; }


        

        [BindProperty]
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Administrator email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid administrator email.")]
        public string AdminEmail { get; set; } = "";

        [BindProperty]
        public string? AdminPhone { get; set; }

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

            // Remove unnecessary spaces from text fields.

            OrganizationName = OrganizationName.Trim();
            OrganizationEmail = OrganizationEmail.Trim();
            FullName = FullName.Trim();
            AdminEmail = AdminEmail.Trim();

            OrganizationPhone = OrganizationPhone?.Trim();
            OrganizationAddress = OrganizationAddress?.Trim();
            AdminPhone = AdminPhone?.Trim();


            // 2. Hash administrator password using BCrypt

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(Password);


            try
            {
                // 3. Create and open database connection

                await using SqlConnection connection =
                    _connectionFactory.CreateConnection();

                await connection.OpenAsync();


                // 4. Begin SQL transaction

                using SqlTransaction transaction = connection.BeginTransaction();


                try
                {
                    // 5. Insert organization and retrieve its generated ID

                    string organizationQuery = @"
                        INSERT INTO Organizations
                            (OrganizationName, Email, PhoneNumber, Address)
                        OUTPUT INSERTED.OrganizationId
                        VALUES
                            (@OrganizationName, @Email, @PhoneNumber, @Address);";

                    int organizationId;

                    await using (SqlCommand command = new SqlCommand(
                        organizationQuery, connection, transaction))
                    {
                        command.Parameters.Add(
                            "@OrganizationName", SqlDbType.NVarChar, 150)
                            .Value = OrganizationName;

                        command.Parameters.Add(
                            "@Email", SqlDbType.NVarChar, 150)
                            .Value = OrganizationEmail;

                        command.Parameters.Add(
                            "@PhoneNumber", SqlDbType.NVarChar, 20)
                            .Value = (object?)OrganizationPhone ?? DBNull.Value;

                        command.Parameters.Add(
                            "@Address", SqlDbType.NVarChar, 250)
                            .Value = (object?)OrganizationAddress ?? DBNull.Value;

                        organizationId = Convert.ToInt32(
                            await command.ExecuteScalarAsync());
                    }


                    // 6. Insert administrator using the generated OrganizationId

                    string userQuery = @"
                        INSERT INTO Users
                            (FullName, Email, PhoneNumber,
                             PasswordHash, Role, OrganizationId)
                        VALUES
                            (@FullName, @Email, @PhoneNumber,
                             @PasswordHash, 'ADMIN', @OrganizationId);";

                    await using (SqlCommand command = new SqlCommand(
                        userQuery, connection, transaction))
                    {
                        command.Parameters.Add(
                            "@FullName", SqlDbType.NVarChar, 150)
                            .Value = FullName;

                        command.Parameters.Add(
                            "@Email", SqlDbType.NVarChar, 150)
                            .Value = AdminEmail;

                        command.Parameters.Add(
                            "@PhoneNumber", SqlDbType.NVarChar, 20)
                            .Value = (object?)AdminPhone ?? DBNull.Value;

                        command.Parameters.Add(
                            "@PasswordHash", SqlDbType.NVarChar, 255)
                            .Value = passwordHash;

                        command.Parameters.Add(
                            "@OrganizationId", SqlDbType.Int)
                            .Value = organizationId;

                        await command.ExecuteNonQueryAsync();
                    }


                    // 7. Both insertions succeeded. Commit transaction.

                    await transaction.CommitAsync();

                    SuccessMessage =
                        "Your organization and administrator account have been registered successfully.";

                    // Clear password fields after successful registration.

                    Password = "";
                    ConfirmPassword = "";

                    return Page();
                }
                catch
                {
                    // If either insertion fails, undo the transaction.

                    await transaction.RollbackAsync();

                    throw;
                }
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // SQL Server duplicate key / unique constraint violation.

                ModelState.AddModelError(
                    string.Empty,
                    "An account or organization with one of these email addresses already exists.");

                return Page();
            }
        }

    }
}
