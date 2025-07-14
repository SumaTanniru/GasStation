using System;
using Microsoft.Data.SqlClient;

class Program
{
    static string connectionString = "Server=localhost;Database=GasStation;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;";

    static void Main(string[] args)
    {
        int customerId = InsertCustomer("John Doe", "john@example.com");
        int employeeId = InsertEmployee("Jane Smith", "Cashier");
        int productId = InsertProduct("Diesel", "Fuel", 3.49m);

        int orderId = InsertOrder(customerId, DateTime.Now, "Cash", 34.90m, "Completed");
        InsertOrderDetail(orderId, productId, 10); // 10 units x $3.49

        Console.WriteLine("All records inserted.");
        Console.ReadKey();
    }

    static int InsertCustomer(string fullName, string email)
    {
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        // Check if customer already exists
        string checkQuery = "SELECT CustomerID FROM Customers WHERE Email = @Email";
        using SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
        checkCmd.Parameters.AddWithValue("@Email", email);

        object existingId = checkCmd.ExecuteScalar();
        if (existingId != null)
            return (int)existingId;

        // Insert new customer
        string insertQuery = @"
            INSERT INTO Customers (FullName, Email, CreatedAt)
            OUTPUT INSERTED.CustomerID
            VALUES (@FullName, @Email, @CreatedAt)";
        using SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
        insertCmd.Parameters.AddWithValue("@FullName", fullName);
        insertCmd.Parameters.AddWithValue("@Email", email);
        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
        return (int)insertCmd.ExecuteScalar();
    }

    static int InsertEmployee(string fullName, string role)
    {
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        string checkQuery = "SELECT EmployeeID FROM Employees WHERE FullName = @FullName AND Role = @Role";
        using SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
        checkCmd.Parameters.AddWithValue("@FullName", fullName);
        checkCmd.Parameters.AddWithValue("@Role", role);

        object existingId = checkCmd.ExecuteScalar();
        if (existingId != null)
            return (int)existingId;

        string insertQuery = @"
            INSERT INTO Employees (FullName, Role)
            OUTPUT INSERTED.EmployeeID
            VALUES (@FullName, @Role)";
        using SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
        insertCmd.Parameters.AddWithValue("@FullName", fullName);
        insertCmd.Parameters.AddWithValue("@Role", role);
        return (int)insertCmd.ExecuteScalar();
    }

    static int InsertProduct(string name, string type, decimal price)
    {
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        string checkQuery = "SELECT ProductID FROM Products WHERE ProductName = @Name AND PricePerUnit = @Price";
        using SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
        checkCmd.Parameters.AddWithValue("@Name", name);
        checkCmd.Parameters.AddWithValue("@Price", price);

        object existingId = checkCmd.ExecuteScalar();
        if (existingId != null)
            return (int)existingId;

        string insertQuery = @"
            INSERT INTO Products (ProductName, ProductType, PricePerUnit)
            OUTPUT INSERTED.ProductID
            VALUES (@Name, @Type, @Price)";
        using SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
        insertCmd.Parameters.AddWithValue("@Name", name);
        insertCmd.Parameters.AddWithValue("@Type", type);
        insertCmd.Parameters.AddWithValue("@Price", price);
        return (int)insertCmd.ExecuteScalar();
    }

    static int InsertOrder(int customerId, DateTime date, string payment, decimal total, string status)
    {
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        string insertQuery = @"
            INSERT INTO Orders (CustomerID, OrderDateTime, PaymentMethod, TotalAmount, Status)
            OUTPUT INSERTED.OrderID
            VALUES (@CustID, @Date, @Payment, @Total, @Status)";
        using SqlCommand cmd = new SqlCommand(insertQuery, conn);
        cmd.Parameters.AddWithValue("@CustID", customerId);
        cmd.Parameters.AddWithValue("@Date", date);
        cmd.Parameters.AddWithValue("@Payment", payment);
        cmd.Parameters.AddWithValue("@Total", total);
        cmd.Parameters.AddWithValue("@Status", status);
        return (int)cmd.ExecuteScalar();
    }

    static void InsertOrderDetail(int orderId, int productId, int quantity)
    {
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        decimal price = GetProductPrice(productId, conn);
        decimal subTotal = price * quantity;

        string insertQuery = @"
            INSERT INTO OrderDetails (OrderID, ProductID, Quantity, SubTotal)
            VALUES (@OrderID, @ProductID, @Qty, @SubTotal)";
        using SqlCommand cmd = new SqlCommand(insertQuery, conn);
        cmd.Parameters.AddWithValue("@OrderID", orderId);
        cmd.Parameters.AddWithValue("@ProductID", productId);
        cmd.Parameters.AddWithValue("@Qty", quantity);
        cmd.Parameters.AddWithValue("@SubTotal", subTotal);
        cmd.ExecuteNonQuery();
    }

    static decimal GetProductPrice(int productId, SqlConnection conn)
    {
        string query = "SELECT PricePerUnit FROM Products WHERE ProductID = @ProductID";
        using SqlCommand cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@ProductID", productId);
        return (decimal)cmd.ExecuteScalar();
    }
}