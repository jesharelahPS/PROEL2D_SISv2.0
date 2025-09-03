using System;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace PROEL2D_SIS
{
    public partial class Login : Form
    {
        string connectionString = @"Data Source=LAB3-PC14\LAB2PC45;Initial Catalog=SIS;Integrated Security=True;";

        private int failedAttempts = 0;
        private DateTime? lockUntil = null;
        private const string SALT = "7";

        private const int FAILED_ATTEMPT_RESET_MINUTES = 30;

        public Login()
        {
            InitializeComponent();
        }

        private async void Login_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async();

            var settings = webView21.CoreWebView2.Settings;
            settings.IsPasswordAutosaveEnabled = false;

            LoadLoginPage();

        }

        private void LoadLoginPage()
        {
            string loginPath = Path.Combine(Application.StartupPath, @"assets\login.html");
            if (!File.Exists(loginPath))
            {
                MessageBox.Show("Login HTML not found: " + loginPath);
                return;
            }

            webView21.Source = new Uri("file:///" + loginPath.Replace("\\", "/"));
            webView21.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            ClearInputs();
        }

        private void ClearInputs()
        {
            webView21.CoreWebView2.PostWebMessageAsString("{\"type\":\"clear\"}");
        }

        private void ExecuteSqlScript()
        {
            string scriptPath = Path.Combine(Application.StartupPath, "assets", "script.sql");
            if (!File.Exists(scriptPath)) return;

            string script = File.ReadAllText(scriptPath);
            string[] batches = script.Split(new string[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                try
                {
                    con.Open();
                    foreach (string batch in batches)
                    {
                        if (!string.IsNullOrWhiteSpace(batch))
                        {
                            SqlCommand cmd = new SqlCommand(batch, con);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to execute SQL script: " + ex.Message);
                }
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            dynamic json = JsonConvert.DeserializeObject(e.TryGetWebMessageAsString());
            string type = json.type;

            if (type == "login")
            {
                string username = json.username;
                string password = json.password;
                AttemptLogin(username, password);
            }
            else if (type == "forgot")
            {
                LoadForgotPasswordPage();
            }
            else if (type == "clear")
            {
                ClearInputs();
            }
        }

        private void LoadForgotPasswordPage()
        {
            string forgotPath = Path.Combine(Application.StartupPath, @"assets\forgotPassword.html");
            if (!File.Exists(forgotPath))
            {
                MessageBox.Show("Forgot password HTML not found: " + forgotPath);
                return;
            }

            webView21.Source = new Uri("file:///" + forgotPath.Replace("\\", "/"));
            webView21.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        private void AttemptLogin(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill in both username and password.");
                return;
            }

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                try
                {
                    con.Open();
                    string query = @"SELECT user_id, username, password_hash, role_id, failed_attempts, lock_until, salt
                             FROM user_login
                             WHERE username = @username";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@username", username);

                    SqlDataReader r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        int userId = r.GetInt32(0);
                        string storedHash = r.GetString(2);
                        int roleId = r.GetInt32(3);
                        int dbFailedAttempts = r.GetInt32(4);
                        object lockUntilObj = r["lock_until"];
                        DateTime? dbLockUntil = lockUntilObj == DBNull.Value ? (DateTime?)null : (DateTime)lockUntilObj;
                        string userSalt = r.GetString(r.GetOrdinal("salt"));

                        r.Close();

                        if (dbLockUntil.HasValue && DateTime.Now < dbLockUntil.Value)
                        {
                            TimeSpan remaining = dbLockUntil.Value - DateTime.Now;
                            MessageBox.Show($"Account is locked. Try again in {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}");
                            return;
                        }

                        string hashedPassword = HashPassword(password, userSalt);

                        if (hashedPassword == storedHash)
                        {
                            
                            ResetLoginAttempts(userId, con);
                            ClearInputs();
                            this.Hide();
                            OpenDashboard(roleId, username);
                        }
                        else
                        {
                      
                            HandleFailedAttemptDB(userId, dbFailedAttempts, con);
                        }
                    }
                    else
                    {
                        r.Close();
                        MessageBox.Show("Invalid username.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Login failed: " + ex.Message);
                }
            }
        }


        private void HandleFailedAttemptDB(int userId, int currentAttempts, SqlConnection con)
        {
            string getLockQuery = @"SELECT lock_until FROM user_login WHERE user_id = @userId";
            DateTime? dbLockUntil = null;
            using (SqlCommand lockCmd = new SqlCommand(getLockQuery, con))
            {
                lockCmd.Parameters.AddWithValue("@userId", userId);
                object lockObj = lockCmd.ExecuteScalar();
                if (lockObj != DBNull.Value) dbLockUntil = (DateTime)lockObj;
            }

            if (dbLockUntil.HasValue && DateTime.Now < dbLockUntil.Value)
            {
                TimeSpan remaining = dbLockUntil.Value - DateTime.Now;
                MessageBox.Show($"Account is locked. Try again in {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}");
                return;
            }

            int newAttempts = currentAttempts + 1;
            DateTime now = DateTime.Now;

            if (newAttempts >= 3)
            {
                DateTime lockUntilTime = now.AddHours(24);
                string updateQuery = @"UPDATE user_login
                               SET failed_attempts = @attempts,
                                   lock_until = @lockUntil,
                                   last_failed_at = @lastFailed
                               WHERE user_id = @userId";

                using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                {
                    cmd.Parameters.AddWithValue("@attempts", newAttempts);
                    cmd.Parameters.AddWithValue("@lockUntil", lockUntilTime);
                    cmd.Parameters.AddWithValue("@lastFailed", now);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Too many failed attempts. Account locked for 24 hours.");
            }
            else
            {
                string updateQuery = @"UPDATE user_login
                               SET failed_attempts = @attempts,
                                   last_failed_at = @lastFailed
                               WHERE user_id = @userId";

                using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                {
                    cmd.Parameters.AddWithValue("@attempts", newAttempts);
                    cmd.Parameters.AddWithValue("@lastFailed", now);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();
                }

                int remaining = 3 - newAttempts;
                MessageBox.Show($"❌ Incorrect login. {remaining} attempt(s) remaining.");
            }
        }





        private void ResetLoginAttempts(int userId, SqlConnection con)
        {
            string query = @"UPDATE user_login
                     SET failed_attempts = 0,
                         lock_until = NULL
                     WHERE user_id = @userId";

            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.ExecuteNonQuery();
            }
        }

        private string GenerateRandomSalt(int length = 10)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            StringBuilder sb = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];

                while (sb.Length < length)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    sb.Append(validChars[(int)(num % (uint)validChars.Length)]);
                }
            }
            return sb.ToString();
        }

        private string HashPassword(string password, string salt)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string salted = password + salt;
                byte[] bytes = Encoding.UTF8.GetBytes(salted);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }



        private void OpenDashboard(int role_id, string username)
        {
            Form dashboard = role_id switch
            {
                1 => new admin_dashboard(username),
                2 => new teacher_dashboard(username),
                3 => new student_dashboard(username),
                _ => null
            };

            if (dashboard != null)
            {
                dashboard.FormClosed += (s, args) => this.Show();
                dashboard.StartPosition = FormStartPosition.CenterScreen;
                dashboard.Show();
                dashboard.BringToFront();
                dashboard.Activate();
            }
            else
            {
                MessageBox.Show("Unrecognized role.");
                this.Show();
            }
        }


    }
}
