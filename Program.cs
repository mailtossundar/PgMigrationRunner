using System;
using System.Threading.Tasks;
using Npgsql;

namespace PgMigrationRunner
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Connection string is read from an environment variable / Azure Pipeline
            // variable so credentials never get hard-coded or committed.
            // Set this as a pipeline variable (mark it "secret"), e.g.:
            //   PG_CONNECTION_STRING = Host=<server>.postgres.database.azure.com;
            //     Port=5432;Database=<dbname>;Username=<user>;Password=<pwd>;SSL Mode=Require;Trust Server Certificate=true
            string connectionString =
    "Host=localhost;" +
    "Port=5432;" +
    "Database=EmployeeaDB;" +
    "Username=postgres;" +
    "Password=Password@123;";

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("ERROR: Environment variable PG_CONNECTION_STRING is not set.");
                return 1;
            }

            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS employee (
                    employee_id     SERIAL PRIMARY KEY,
                    first_name      VARCHAR(100) NOT NULL,
                    last_name       VARCHAR(100) NOT NULL,
                    email           VARCHAR(150) NOT NULL UNIQUE,
                    department      VARCHAR(100),
                    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
                );";

            const string insertSql = @"
                INSERT INTO employee (first_name, last_name, email, department)
                VALUES (@firstName, @lastName, @email, @department)
                ON CONFLICT (email) DO NOTHING;";

            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                Console.WriteLine("Connected to PostgreSQL server.");

                // 1. Create table
                await using (var createCmd = new NpgsqlCommand(createTableSql, conn))
                {
                    await createCmd.ExecuteNonQueryAsync();
                    Console.WriteLine("Table 'employee' ensured (created if it did not exist).");
                }

                // 2. Insert sample values (parameterized to avoid SQL injection)
                var sampleRows = new (string FirstName, string LastName, string Email, string Department)[]
                {
                    ("Bala", "Kuppusamy", "bala.kuppusamy@example.com", "Engineering"),
                    ("Anitha", "Raman", "anitha.raman@example.com", "QA"),
                };

                foreach (var row in sampleRows)
                {
                    await using var insertCmd = new NpgsqlCommand(insertSql, conn);
                    insertCmd.Parameters.AddWithValue("firstName", row.FirstName);
                    insertCmd.Parameters.AddWithValue("lastName", row.LastName);
                    insertCmd.Parameters.AddWithValue("email", row.Email);
                    insertCmd.Parameters.AddWithValue("department", row.Department);

                    int rowsAffected = await insertCmd.ExecuteNonQueryAsync();
                    Console.WriteLine(rowsAffected > 0
                        ? $"Inserted: {row.FirstName} {row.LastName}"
                        : $"Skipped (already exists): {row.Email}");
                }

                Console.WriteLine("Migration completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Migration failed: {ex.Message}");
                return 1;
            }
        }
    }
}
