using System;
using System.Data;
using MySql.Data.MySqlClient;

namespace MovieHallBookingGUI
{
    public static class DbHelper
    {
        //  Update with your XAMPP MySQL credentials
        private static readonly string connStr =
            "Server=localhost;Database=movie_booking;Uid=root;Pwd=;SslMode=none;";

        // Execute SELECT queries 
        public static DataTable Query(string sql)
        {
            using var conn = new MySqlConnection(connStr);
            using var cmd = new MySqlCommand(sql, conn);
            using var adapter = new MySqlDataAdapter(cmd);
            var dt = new DataTable();

            try
            {
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Query failed: " + ex.Message);
            }

            return dt;
        }

        // Execute INSERT, UPDATE, DELETE 
        public static int Execute(string sql)
        {
            using var conn = new MySqlConnection(connStr);
            using var cmd = new MySqlCommand(sql, conn);
            try
            {
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database command failed: " + ex.Message);
                return 0;
            }
        }

        // Get hall layout (rows & cols)
        public static (int Rows, int Cols)? GetHallLayout(int hallId)
        {
            using var conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();

                string query = "SELECT total_rows, total_cols FROM halls WHERE id=@id LIMIT 1;";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", hallId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    // ✅ Safe null checks with correct column names
                    int rows = reader["total_rows"] is DBNull ? 0 : Convert.ToInt32(reader["total_rows"]);
                    int cols = reader["total_cols"] is DBNull ? 0 : Convert.ToInt32(reader["total_cols"]);

                    return (rows, cols);
                }

                return null; // not found
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetHallLayout failed: " + ex.Message);
                return null;
            }
        }

        // Try to book a seat for a movie & showtime
        public static bool TryBookSeat(int seatId, int? movieId, DateTime showTime, string customerName)
        {
            using var conn = new MySqlConnection(connStr);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // Check if the seat is already booked
                using var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM bookings WHERE seat_id=@seat AND show_time=@st AND status='booked';",
                    conn, trans);

                checkCmd.Parameters.AddWithValue("@seat", seatId);
                checkCmd.Parameters.AddWithValue("@st", showTime);

                object? result = checkCmd.ExecuteScalar();
                long existing = (result == null || result is DBNull) ? 0 : Convert.ToInt64(result);

                if (existing > 0)
                {
                    trans.Rollback();
                    return false; // seat  booked
                }

                // Insert new booking
                using var insertCmd = new MySqlCommand(
                    @"INSERT INTO bookings (seat_id, movie_id, show_time, customer_name, status) 
                      VALUES (@seat,@mid,@st,@cust,'booked');",
                    conn, trans);

                insertCmd.Parameters.AddWithValue("@seat", seatId);
                insertCmd.Parameters.AddWithValue("@mid", (object?)movieId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@st", showTime);
                insertCmd.Parameters.AddWithValue("@cust", customerName ?? string.Empty);

                insertCmd.ExecuteNonQuery();
                trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Booking failed: " + ex.Message);
                try { trans.Rollback(); } catch { }
                return false;
            }
        }
    }
}
