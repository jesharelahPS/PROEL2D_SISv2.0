using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace PROEL2D_SIS
{
    public partial class teacher_dashboard : Form
    {
        private string loggedInUser;

        public teacher_dashboard(string username)
        {
            InitializeComponent();
            this.loggedInUser = username;
            this.Load += Teacher_dashboard_Load;
        }

        private async void Teacher_dashboard_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async();
            LoadHtml();
        }

        private void LoadHtml()
        {
            string path = Path.Combine(Application.StartupPath, @"assets\teacher_dashboard.html");
            if (!File.Exists(path))
            {
                MessageBox.Show("Teacher HTML not found: " + path);
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
