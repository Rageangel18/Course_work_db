using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Npgsql;

namespace ETLProcess
{
    class Program
    {
        static void Main(string[] args)
        {
            string oltpConnectionString = "Host=localhost;Username=postgres;Password=123;Database=Course_Work";
            string olapConnectionString = "Host=localhost;Username=postgres;Password=123;Database=Course_Work_DWH";

            // Step 1: Extract data from OLTP
            var customers = ExtractCustomers(oltpConnectionString);
            var products = ExtractProducts(oltpConnectionString);
            var sales = ExtractSales(oltpConnectionString);
            var orders = ExtractOrders(oltpConnectionString);

            // Step 2: Transform data (if needed)
            // Example: Add a transformation function if needed

            // Step 3: Load data into OLAP
            LoadCustomers(olapConnectionString, customers);
            LoadProducts(olapConnectionString, products);
            LoadSales(olapConnectionString, sales);
            LoadOrders(olapConnectionString, orders);

            Console.WriteLine("ETL process completed successfully.");
        }

        static List<Customer> ExtractCustomers(string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                var query = @"
            SELECT u.id AS Customer_ID, u.username AS Name, u.email AS Email, MIN(o.delivery_address) AS Address
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            GROUP BY u.id, u.username, u.email";
                return connection.Query<Customer>(query).AsList();
            }
        }

        static List<Product> ExtractProducts(string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                var query = @"
                    SELECT p.id AS Product_ID, 
                           p.name AS Name, 
                           s.name AS Subcategory, 
                           c.name AS Category, 
                           p.price AS Price, 
                           p.availability_status AS AvailabilityStatus
                    FROM products p
                    JOIN subcategories s ON p.subcategory_id = s.id
                    JOIN categories c ON s.category_id = c.id";
                return connection.Query<Product>(query).AsList();
            }
        }

        static List<Sale> ExtractSales(string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
            SELECT 
                oi.id AS Sales_ID, 
                oi.product_id AS Product_ID, 
                o.user_id AS Customer_ID, 
                o.order_date AS CreatedAt, 
                oi.quantity AS Quantity_Sold, 
                oi.price * oi.quantity AS Total_Sales 
            FROM 
                order_items oi
            JOIN 
                orders o ON oi.order_id = o.id";

                return connection.Query<Sale>(query).AsList();
            }
        }

        static List<Order> ExtractOrders(string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT 
                        o.id AS Order_ID, 
                        oi.product_id AS Product_ID, 
                        o.user_id AS Customer_ID, 
                        o.order_date AS CreatedAt, 
                        oi.quantity AS Order_Quantity, 
                        oi.price * oi.quantity AS Order_Total 
                    FROM 
                        orders o
                    JOIN 
                        order_items oi ON o.id = oi.order_id";
                return connection.Query<Order>(query).AsList();
            }
        }

            static void LoadCustomers(string connectionString, List<Customer> customers)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                foreach (var customer in customers)
                {
                    // Check if customer already exists in OLAP
                    var existingCustomer = connection.QuerySingleOrDefault<Customer>("SELECT * FROM Dim_Customer WHERE Customer_ID = @Customer_ID", new { customer.Customer_ID });
                    if (existingCustomer == null)
                    {
                        connection.Execute("INSERT INTO Dim_Customer (Customer_ID, Name, Email, StartDate, EndDate, IsCurrent) VALUES (@Customer_ID, @Name, @Email, @StartDate, @EndDate, @IsCurrent)", new { customer.Customer_ID, customer.Name, customer.Email, StartDate = DateTime.Now, EndDate = (DateTime?)null, IsCurrent = true });
                    }
                }
            }
        }

        static void LoadProducts(string connectionString, List<Product> products)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                foreach (var product in products)
                {
                    // Check if product already exists in OLAP
                    var existingProduct = connection.QuerySingleOrDefault<Product>("SELECT * FROM Dim_Product WHERE Product_ID = @Product_ID", new { product.Product_ID });
                    if (existingProduct == null)
                    {
                        connection.Execute("INSERT INTO Dim_Product (Product_ID, Name, Subcategory, Category, Price) VALUES (@Product_ID, @Name, @Subcategory, @Category, @Price)", product);
                    }
                }
            }
        }

        static void LoadSales(string connectionString, List<Sale> sales)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                foreach (var sale in sales)
                {
                    // Check if sale already exists in OLAP
                    var existingSale = connection.QuerySingleOrDefault<Sale>("SELECT * FROM Fact_Sales WHERE Sales_ID = @Sales_ID", new { sale.Sales_ID });
                    if (existingSale == null)
                    {
                        connection.Execute("INSERT INTO Fact_Sales (Sales_ID, Product_ID, Customer_ID, CreatedAt, Quantity_Sold, Total_Sales) VALUES (@Sales_ID, @Product_ID, @Customer_ID, @CreatedAt, @Quantity_Sold, @Total_Sales)", sale);
                    }
                }
            }
        }

        static void LoadOrders(string connectionString, List<Order> orders)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                foreach (var order in orders)
                {
                    var existingOrder = connection.QuerySingleOrDefault<Order>("SELECT * FROM Fact_Orders WHERE Order_ID = @Order_ID", new { order.Order_ID });
                    if (existingOrder == null)
                    {
                        connection.Execute("INSERT INTO Fact_Orders (Order_ID, Product_ID, Customer_ID, CreatedAt, Order_Quantity, Order_Total) VALUES (@Order_ID, @Product_ID, @Customer_ID, @CreatedAt, @Order_Quantity, @Order_Total)", new { order.Order_ID, order.Product_ID, order.Customer_ID, CreatedAt = order.CreatedAt, order.Order_Quantity, order.Order_Total });
                    }
                }
            }
        }

        public class Customer
        {
            public int Customer_ID { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Address { get; set; }
        }

        public class Product
        {
            public int Product_ID { get; set; }
            public string Name { get; set; }
            public string Subcategory { get; set; }
            public string Category { get; set; }
            public decimal Price { get; set; }
            public bool AvailabilityStatus { get; set; }
        }

        public class Sale
        {
            public int Sales_ID { get; set; }
            public int Product_ID { get; set; }
            public int Customer_ID { get; set; }
            public DateTime CreatedAt { get; set; }
            public int Quantity_Sold { get; set; }
            public decimal Total_Sales { get; set; }
        }
        public class Order
        {
            public int Order_ID { get; set; }
            public int Product_ID { get; set; }
            public int Customer_ID { get; set; }
            public DateTime CreatedAt { get; set; }
            public int Order_Quantity { get; set; }
            public decimal Order_Total { get; set; }
        }
    }
}