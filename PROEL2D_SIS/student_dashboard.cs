using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace PROEL2D_SIS
{
    public partial class student_dashboard : Form
    {
        private string loggedInUser;

        public student_dashboard(string username)
        {
            InitializeComponent();
            this.loggedInUser = username;
            this.Load += Student_dashboard_Load;
        }

        private async void Student_dashboard_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async();
            LoadHtml();
        }

        private void LoadHtml()
        {
            string path = Path.Combine(Application.StartupPath, @"assets\student_dashboard.html");
            if (!File.Exists(path))
            {
                MessageBox.Show("Student HTML not found: " + path);
                return;
            }

            webView21.Source = new Uri("file:///" + path.Replace("\\", "/"));

            webView21.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                webView21.CoreWebView2.PostWebMessageAsString(
                    $"{{ \"type\": \"setUser\", \"username\": \"{loggedInUser}\" }}"
                );
            };
        }
    }
}
