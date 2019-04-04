using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace P2P_char_forms
{
    public partial class LoginForm : Form
    {
        private string _username = "";

        public string UserName
        { 
            get { return _username; }
        }

        public LoginForm()
        {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(LoginForm_FormClosing);
            btnOK.Click += new EventHandler(btnOK_Click);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            _username = tbUserName.Text.Trim();
            if (string.IsNullOrEmpty(_username))
            {
                MessageBox.Show("Please select a user name.");
                return;
            }
            this.FormClosing -= LoginForm_FormClosing;
            this.Close();
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _username = "";
        }

        private void tbUserName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnOK.PerformClick();
            }
        }
    }
}