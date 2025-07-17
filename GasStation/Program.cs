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
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        while (true)
        {
            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("1. Import All Orders from Excel");
            Console.WriteLine("2. View Customer Records in Batches (100 per batch)");
            Console.WriteLine("3. Insert Records Into DB by Batch Number");
            Console.WriteLine("4. Exit");
            Console.Write("Enter your choice (1-4): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    ImportFromExcelFull();
                    break;

                case "2":
                    ViewCustomerBatch();
                    break;

                case "3":
                    Console.Write("Enter batch number to insert: ");
                    if (int.TryParse(Console.ReadLine(), out int batchNumber))
                    {
                        InsertBatchFromExcel(batchNumber);
                    }
                    else
                    {
                        Console.WriteLine("Invalid batch number");
                    }
                    break;

                case "4":
                    Console.WriteLine("Exiting...");
                    return;

                default:
                    Console.WriteLine("Invalid choice, try again.");
                    break;
            }
        }
    }

    // Import all records at once (full import)
    static void ImportFromExcelFull()
    {
        string filePath = @"C:\Users\sande\source\repos\GasStation\GasStation\Resource\PurchaseOrdersWithCustomer.xlsx";

        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ Excel file not found at: " + filePath);
            return;
        }

        var allRecords = ReadExcelFile(filePath);
        InsertRecordsToDb(allRecords);
    }

    // Insert only one batch of 100 records by batch number (1-based)
    static void InsertBatchFromExcel(int batchNumber)
    {
        string filePath = @"C:\Users\sande\source\repos\GasStation\GasStation\Resource\PurchaseOrdersWithCustomer.xlsx";

        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ Excel file not found at: " + filePath);
            return;
        }

        var allRecords = ReadExcelFile(filePath);

        int batchSize = 100;
        int startIndex = (batchNumber - 1) * batchSize;

        if (startIndex >= allRecords.Count)
        {
            Console.WriteLine("❌ Batch number exceeds total records.");
            return;
        }

        var batchRecords = allRecords.GetRange(startIndex, Math.Min(batchSize, allRecords.Count - startIndex));

        InsertRecordsToDb(batchRecords);

        Console.WriteLine($"\n✅ Batch {batchNumber} inserted successfully.");
        ViewCustomerBatchForBatch(batchNumber);
    }

    // Read Excel into list of OrderRecord
    static List<OrderRecord> ReadExcelFile(string filePath)
    {
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
        return records;
    }

    // Insert customers and orders to DB
    static void InsertRecordsToDb(List<OrderRecord> records)
    {
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
                }
            }
        }

        Console.WriteLine($"\n🧾 Orders inserted: {totalOrders}");
        Console.WriteLine($"🧍 New customers inserted: {newCustomers}");
    }

    // View customer records by batch number (100 per batch)
    static void ViewCustomerBatch()
    {
        int batchSize = 100;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Get total records count to calculate batches
            int totalRecords = (int)new SqlCommand("SELECT COUNT(*) FROM Customers", connection).ExecuteScalar();
            int totalBatches = (int)Math.Ceiling(totalRecords / (double)batchSize);

            Console.Write($"Enter batch number (1 to {totalBatches}): ");
            if (!int.TryParse(Console.ReadLine(), out int batchNumber) || batchNumber < 1 || batchNumber > totalBatches)
            {
                Console.WriteLine("❌ Invalid batch number.");
                return;
            }

            int offset = (batchNumber - 1) * batchSize;

            string query = @"
                SELECT * FROM Customers
                ORDER BY CustomerID
                OFFSET @Offset ROWS
                FETCH NEXT @BatchSize ROWS ONLY;
            ";

            SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Offset", offset);
            command.Parameters.AddWithValue("@BatchSize", batchSize);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                Console.WriteLine($"\n📦 Customer Records - Batch {batchNumber}:\n");

                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["CustomerID"]}, Name: {reader["FullName"]}, Phone: {reader["PhoneNumber"]}");
                }
            }
        }
    }

    // View customers of a specific batch (used after insert)
    static void ViewCustomerBatchForBatch(int batchNumber)
    {
        int batchSize = 100;
        int offset = (batchNumber - 1) * batchSize;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            string query = @"
                SELECT * FROM Customers
                ORDER BY CustomerID
                OFFSET @Offset ROWS
                FETCH NEXT @BatchSize ROWS ONLY;
            ";

            SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Offset", offset);
            command.Parameters.AddWithValue("@BatchSize", batchSize);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                Console.WriteLine($"\n📦 Customers in Batch {batchNumber}:\n");

                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["CustomerID"]}, Name: {reader["FullName"]}, Phone: {reader["PhoneNumber"]}");
                }
            }
        }
    }
}

// Model class for Excel data
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
