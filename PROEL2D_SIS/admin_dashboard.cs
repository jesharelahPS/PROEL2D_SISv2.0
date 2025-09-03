using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace PROEL2D_SIS
{
    public partial class admin_dashboard : Form
    {
        private string loggedInUser;
        private string connectionString = @"Data Source=LAB3-PC14\LAB2PC45;Initial Catalog=SIS;Integrated Security=True;";

        public admin_dashboard(string username)
        {
            InitializeComponent();
            this.loggedInUser = username;
            this.Load += Admin_dashboard_Load;
        }

        private async void Admin_dashboard_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async();
            webView21.CoreWebView2.WebMessageReceived += WebView21_WebMessageReceived;
            webView21.CoreWebView2.NavigationCompleted += WebView21_NavigationCompleted;

            LoadDashboardPage();
        }


        private void LoadDashboardPage()
        {
            string path = Path.Combine(Application.StartupPath, @"assets\admin_dashboard.html");
            if (!File.Exists(path))
            {
                MessageBox.Show("Admin HTML not found: " + path);
                return;
            }
            webView21.Source = new Uri("file:///" + path.Replace("\\", "/"));
        }



        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            string filename = Path.GetFileName(webView21.Source.LocalPath).ToLower();

            // Always send the logged in user first
            webView21.CoreWebView2.PostWebMessageAsString(
                $"{{\"type\":\"setUser\",\"username\":\"{loggedInUser}\"}}"
            );

            if (filename == "admin_dashboard.html")
            {
                var stats = await GetDashboardStats();
                webView21.CoreWebView2.PostWebMessageAsString(
                    JsonConvert.SerializeObject(new { type = "dashboardStats", data = stats })
                );
            }
            else if (filename == "admin_dashboard.html")
            {
                var students = await GetAllStudents();
                webView21.CoreWebView2.PostWebMessageAsString(
                    JsonConvert.SerializeObject(new { type = "studentData", data = students })
                );
            }
        }


        private async System.Threading.Tasks.Task<DashboardStats> GetDashboardStats()
        {
            var stats = new DashboardStats();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Student", conn))
                        stats.TotalStudents = (int)await cmd.ExecuteScalarAsync();

                    using (SqlCommand cmd = new SqlCommand(@"
                                SELECT 
                                    COALESCE(SUM(CASE WHEN status = 'Active' THEN 1 ELSE 0 END), 0) AS ActiveCount,
                                    COALESCE(SUM(CASE WHEN status <> 'Active' THEN 1 ELSE 0 END), 0) AS InactiveCount
                                FROM Student", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            stats.ActiveStudents = reader.GetInt32(0);
                            stats.InactiveStudents = reader.GetInt32(1);
                        }
                    }

                    // --- Total Teachers ---
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Teacher", conn))
                        stats.TotalTeachers = (int)await cmd.ExecuteScalarAsync();

                    // --- Total Courses ---
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Course", conn))
                        stats.TotalCourses = (int)await cmd.ExecuteScalarAsync();

                    // --- Courses per Teacher ---
                    using (SqlCommand cmd = new SqlCommand(@"
                            SELECT (t.first_name + ' ' + t.last_name) AS TeacherName, COUNT(c.course_id)
                            FROM Teacher t
                            LEFT JOIN Course c ON t.teacher_id = c.teacher_id
                            GROUP BY t.first_name, t.last_name", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            stats.TeacherCourses[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error fetching stats: " + ex.Message);
            }

            return stats;
        }

        public class DashboardStats
        {
            public int TotalStudents { get; set; }
            public int ActiveStudents { get; set; }
            public int InactiveStudents { get; set; }
            public int TotalTeachers { get; set; }
            public int TotalCourses { get; set; }
            public Dictionary<string, int> TeacherCourses { get; set; } = new Dictionary<string, int>();
        }


        private void WebView21_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonConvert.DeserializeObject<dynamic>(e.TryGetWebMessageAsString());
                string type = msg.type;

                if (type == "logout")
                {
                    this.Hide();
                    var loginForm = Application.OpenForms["Login"];

                    if (loginForm != null)
                    {
                        loginForm.Show();
                        loginForm.BringToFront();
                        loginForm.WindowState = FormWindowState.Maximized;
                        loginForm.Activate();
                    }
                    else
                    {
                        var newLogin = new Login();
                        newLogin.Show();
                        newLogin.BringToFront();
                        newLogin.Activate();
                    }

                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error handling message: " + ex.Message);
            }
        }










        private async Task<List<Student>> GetAllStudents()
        {
            var students = new List<Student>();

            string connectionString = @"Data Source=LAB3-PC14\LAB2PC45;Initial Catalog=SIS;Integrated Security=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"SELECT student_id, first_name, last_name, date_of_birth, gender, email, phone, address, enrollment_date, status FROM Student";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        students.Add(new Student
                        {
                            StudentId = reader.GetInt32(0),
                            FirstName = reader.GetString(1),
                            LastName = reader.GetString(2),
                            DateOfBirth = reader.GetDateTime(3),
                            Gender = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Email = reader.GetString(5),
                            Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Address = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            EnrollmentDate = reader.GetDateTime(8),
                            Status = reader.IsDBNull(9) ? "Active" : reader.GetString(9)
                        });
                    }
                }
            }

            return students;
        }
        public class Student
        {
            public int StudentId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime DateOfBirth { get; set; }
            public string Gender { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public DateTime EnrollmentDate { get; set; }
            public string Status { get; set; }
        }


    }
}
