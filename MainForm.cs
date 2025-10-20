using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MovieHallBookingGUI
{
    public class MainForm : Form
    {
        private ComboBox cbMovies = new();
        private ComboBox cbShowTime = new();
        private FlowLayoutPanel seatPanel = new();
        private Label lblInfo = new();
        private Button btnRefresh = new();

        private int hallId = 1;
        private DateTime selectedShowTime;

        public MainForm()
        {
            Text = "Movie Seat Booking";
            Width = 900;
            Height = 600;
            InitializeComponents();
            Load += MainForm_Load!;
        }

        private void InitializeComponents()
        {
            cbMovies.DropDownStyle = ComboBoxStyle.DropDownList;
            cbMovies.Width = 250;
            cbMovies.SelectedIndexChanged += (_, _) => LoadShowTimes();

            cbShowTime.DropDownStyle = ComboBoxStyle.DropDownList;
            cbShowTime.Width = 250;
            cbShowTime.SelectedIndexChanged += (_, _) => HandleShowtimeSelection();

            btnRefresh.Text = "Refresh";
            btnRefresh.AutoSize = true;
            btnRefresh.Click += (_, _) => RenderSeats();

            lblInfo.Text = "Select a movie and showtime, then book a seat";
            lblInfo.AutoSize = true;

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8)
            };

            topPanel.Controls.Add(new Label { Text = "Movie:", AutoSize = true, Padding = new Padding(6, 8, 6, 6) });
            topPanel.Controls.Add(cbMovies);
            topPanel.Controls.Add(new Label { Text = "Showtime:", AutoSize = true, Padding = new Padding(6, 8, 6, 6) });
            topPanel.Controls.Add(cbShowTime);
            topPanel.Controls.Add(btnRefresh);
            topPanel.Controls.Add(lblInfo);

            seatPanel.Dock = DockStyle.Fill;
            seatPanel.AutoScroll = true;
            seatPanel.FlowDirection = FlowDirection.TopDown;
            seatPanel.WrapContents = false;
            seatPanel.Padding = new Padding(20);

            Controls.Add(seatPanel);
            Controls.Add(topPanel);
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            LoadMovies();
        }

        private void LoadMovies()
        {
            var dtMovies = DbHelper.Query("SELECT id, title FROM movies ORDER BY title");
            cbMovies.Items.Clear();

            foreach (DataRow row in dtMovies.Rows)
            {
                cbMovies.Items.Add(new ComboBoxItem
                {
                    Id = Convert.ToInt32(row["id"]),
                    Text = row["title"]?.ToString() ?? ""
                });
            }

            if (cbMovies.Items.Count > 0)
                cbMovies.SelectedIndex = 0;
        }

        private void LoadShowTimes()
        {
            if (cbMovies.SelectedItem is not ComboBoxItem movieItem) return;

            var dtShow = DbHelper.Query(
                $"SELECT id, show_time, hall_id FROM showtimes WHERE movie_id={movieItem.Id} ORDER BY show_time");

            cbShowTime.Items.Clear();

            foreach (DataRow row in dtShow.Rows)
            {
                string display = $"{row["hall_id"]} | {Convert.ToDateTime(row["show_time"]):yyyy-MM-dd HH:mm}";
                cbShowTime.Items.Add(display);
            }

            if (cbShowTime.Items.Count > 0)
                cbShowTime.SelectedIndex = 0;
        }

        private void HandleShowtimeSelection()
        {
            if (cbShowTime.SelectedItem == null) return;

            string[] parts = cbShowTime.SelectedItem.ToString()?.Split('|') ?? Array.Empty<string>();
            if (parts.Length < 2) return;

            if (int.TryParse(parts[0].Trim(), out int hid))
                hallId = hid;

            if (DateTime.TryParse(parts[1].Trim(), out DateTime dt))
                selectedShowTime = dt;

            RenderSeats();
        }

        private void RenderSeats()
        {
            seatPanel.Controls.Clear();

            // ✅ Match actual DB columns: total_rows / total_cols
            var dtHall = DbHelper.Query($"SELECT total_rows, total_cols FROM halls WHERE id={hallId} LIMIT 1");
            if (dtHall.Rows.Count == 0)
            {
                lblInfo.Text = "No hall configured.";
                return;
            }

            int rows = dtHall.Rows[0]["total_rows"] is DBNull ? 0 : Convert.ToInt32(dtHall.Rows[0]["total_rows"]);
            int cols = dtHall.Rows[0]["total_cols"] is DBNull ? 0 : Convert.ToInt32(dtHall.Rows[0]["total_cols"]);

            if (rows == 0 || cols == 0)
            {
                lblInfo.Text = "Hall layout not defined.";
                return;
            }

            string showTimeStr = selectedShowTime.ToString("yyyy-MM-dd HH:mm:ss");
            var dtBooked = DbHelper.Query(
                $"SELECT seat_id FROM bookings WHERE show_time='{showTimeStr}' AND status='booked'");
            var bookedSeats = dtBooked.AsEnumerable()
                                      .Select(r => Convert.ToInt32(r["seat_id"]))
                                      .ToHashSet();

            // Load all seats at once for this hall
            var dtSeats = DbHelper.Query($"SELECT id, label, row_num, col_num FROM seats WHERE hall_id={hallId}");
            if (dtSeats.Rows.Count == 0)
            {
                lblInfo.Text = "No seats found for this hall.";
                return;
            }

            for (int r = 1; r <= rows; r++)
            {
                var rowPanel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(4)
                };

                var rowSeats = dtSeats.AsEnumerable().Where(x => Convert.ToInt32(x["row_num"]) == r)
                                                     .OrderBy(x => Convert.ToInt32(x["col_num"]));

                foreach (var seat in rowSeats)
                {
                    int seatId = Convert.ToInt32(seat["id"]);
                    string label = seat["label"]?.ToString() ?? $"R{r}C{seat["col_num"]}";

                    var btn = new Button
                    {
                        Text = label,
                        Tag = seatId,
                        Width = 85,        // bigger width
                        Height = 55,       // bigger height
                        Margin = new Padding(9), // more spacing
                        Font = new Font("Segoe UI", 10, FontStyle.Bold), // clearer text
                        BackColor = bookedSeats.Contains(seatId) ? Color.LightGray : Color.LightGreen,
                        Enabled = !bookedSeats.Contains(seatId)
                    };

                    if (btn.Enabled)
                        btn.Click += SeatButton_Click!;

                    rowPanel.Controls.Add(btn);
                }

                seatPanel.Controls.Add(rowPanel);
            }
        }

        private void SeatButton_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (cbMovies.SelectedItem is not ComboBoxItem movieItem) return;
            if (btn.Tag is not int seatId) return;

            string? customer = Prompt.ShowDialog("Customer name:", "Book Seat");
            if (string.IsNullOrWhiteSpace(customer)) return;

            bool ok = DbHelper.TryBookSeat(seatId, movieItem.Id, selectedShowTime, customer);
            if (ok)
            {
                MessageBox.Show($"Seat {btn.Text} booked for {customer}", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Seat already booked by someone else.", "Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            RenderSeats();
        }

        private class ComboBoxItem
        {
            public int Id;
            public string Text = string.Empty;
            public override string ToString() => Text;
        }
    }

    public static class Prompt
    {
        public static string? ShowDialog(string text, string caption)
        {
            Form prompt = new()
            {
                Width = 360,
                Height = 160,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent
            };

            Label textLabel = new() { Left = 20, Top = 20, Text = text, AutoSize = true };
            TextBox textBox = new() { Left = 20, Top = 50, Width = 300 };
            Button confirmation = new() { Text = "OK", Left = 160, Width = 80, Top = 85, DialogResult = DialogResult.OK };

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
