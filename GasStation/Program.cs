using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using ExcelDataReader;
using System.Text;

class Program
{
    static string connectionString = "Server=localhost;Database=GasStation;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;";

    static void Main(string[] args)
    {
        // Option 1: Insert sample records manually
        //InsertSampleRecords();

        // Option 2: Import from Excel
        ImportFromExcel();
    }

    static void InsertSampleRecords()
    {
        int customerId = InsertCustomer("John Doe", "john@example.com");
        int employeeId = InsertEmployee("Jane Smith", "Cashier");
    }

    static void ImportFromExcel()
    {
        string filePath = @"C:\Users\sande\source\repos\GasStation\GasStation\Resource\PurchaseOrdersWithCustomer.xlsx";

        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ Excel file not found at: " + filePath);
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var records = new List<OrderRecord>();

        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            var dataset = reader.AsDataSet();
            var table = dataset.Tables[0];

            for (int i = 1; i < table.Rows.Count; i++) // skip header
            {
                var row = table.Rows[i];
                records.Add(new OrderRecord
                {
                    OrderID = Convert.ToInt32(row[0]),
                    FullName = row[1].ToString(),
                    PhoneNumber = string.IsNullOrWhiteSpace(row[2]?.ToString()) ? "UNKNOWN" : row[2].ToString().Trim(),
                    Email = row[3].ToString(),
                    VehicleNumber = row[4].ToString(),
                    OrderDateTime = Convert.ToDateTime(row[5]),
                    PaymentMethod = row[6].ToString(),
                    TotalAmount = Convert.ToDecimal(row[7]),
                    Status = row[8].ToString()
                });
            }
        }

        int newCustomers = 0;
        int totalOrders = 0;

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            foreach (var record in records)
            {
                int customerId;

                // Check if customer exists
                using (var checkCmd = new SqlCommand("SELECT CustomerID FROM Customers WHERE PhoneNumber = @Phone", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Phone", record.PhoneNumber);
                    var result = checkCmd.ExecuteScalar();

                    if (result != null)
                    {
                        customerId = Convert.ToInt32(result);
                    }
                    else
                    {
                        using (var insertCustomer = new SqlCommand(
                            @"INSERT INTO Customers (FullName, PhoneNumber, Email, VehicleNumber, CreatedAt)
                              VALUES (@FullName, @Phone, @Email, @Vehicle, @CreatedAt);
                              SELECT SCOPE_IDENTITY();", conn))
                        {
                            insertCustomer.Parameters.AddWithValue("@FullName", record.FullName);
                            insertCustomer.Parameters.AddWithValue("@Phone", record.PhoneNumber);
                            insertCustomer.Parameters.AddWithValue("@Email", record.Email);
                            insertCustomer.Parameters.AddWithValue("@Vehicle", record.VehicleNumber);
                            insertCustomer.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                            customerId = Convert.ToInt32(insertCustomer.ExecuteScalar());
                            newCustomers++;
                        }
                    }
                }

                // Insert into Orders
                using (var insertOrder = new SqlCommand(
                    @"INSERT INTO Orders (CustomerID, OrderDateTime, PaymentMethod, TotalAmount, Status)
                      VALUES (@CustomerID, @OrderDateTime, @PaymentMethod, @TotalAmount, @Status)", conn))
                {
                    insertOrder.Parameters.AddWithValue("@CustomerID", customerId);
                    insertOrder.Parameters.AddWithValue("@OrderDateTime", record.OrderDateTime);
                    insertOrder.Parameters.AddWithValue("@PaymentMethod", record.PaymentMethod);
                    insertOrder.Parameters.AddWithValue("@TotalAmount", record.TotalAmount);
                    insertOrder.Parameters.AddWithValue("@Status", record.Status);

                    insertOrder.ExecuteNonQuery();
                    totalOrders++;
                    Console.WriteLine("✅ Order inserted.");
                }
            }
        }

        // Summary
        Console.WriteLine("\n📊 Import Summary:");
        Console.WriteLine($"🧾 Orders inserted: {totalOrders}");
        Console.WriteLine($"🧍 New customers inserted: {newCustomers}");
    }

    static int InsertCustomer(string name, string email)
    {
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqlCommand("INSERT INTO Customers (FullName, Email, CreatedAt) VALUES (@Name, @Email, @CreatedAt); SELECT SCOPE_IDENTITY();", conn))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }

    static int InsertEmployee(string name, string role)
    {
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqlCommand("INSERT INTO Employees (FullName, Role) VALUES (@Name, @Role); SELECT SCOPE_IDENTITY();", conn))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Role", role);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
}

// 🔽 Keep this class at the bottom of Program.cs
public class OrderRecord
{
    public int OrderID { get; set; }
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string VehicleNumber { get; set; }
    public DateTime OrderDateTime { get; set; }
    public string PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }
}