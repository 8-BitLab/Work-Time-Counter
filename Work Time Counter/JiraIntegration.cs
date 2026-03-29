// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        JiraIntegration.cs                                           ║
// ║  PURPOSE:     JIRA CLOUD API INTEGRATION FOR TASK IMPORT                   ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Work_Time_Counter
{
    /// <summary>
    /// Data class representing a Jira issue
    /// </summary>
    public class JiraIssue
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("priority")]
        public string Priority { get; set; }

        [JsonProperty("issueType")]
        public string IssueType { get; set; }

        [JsonProperty("assignee")]
        public string Assignee { get; set; }

        public override string ToString()
        {
            return $"{Key}: {Summary}";
        }
    }

    /// <summary>
    /// Helper class for integrating with Jira Cloud REST API
    /// </summary>
    public class JiraIntegration
    {
        private const string JiraBaseUrl = "https://8bitlab-team.atlassian.net/rest/api/3";
        private const string CloudId = "e697b036-6932-4682-933e-223125c21177";
        private const string ProjectKey = "SCRUM";

        private string _email;
        private string _apiToken;
        private HttpClient _httpClient;
        private string _settingsPath;

        public JiraIntegration()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkTimeCounter",
                "jira_settings.json");

            _httpClient = new HttpClient();
            LoadSettings();
        }

        /// <summary>
        /// Gets a value indicating whether Jira is configured with email and API token
        /// </summary>
        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(_apiToken); }
        }

        /// <summary>
        /// Loads Jira settings from the settings file
        /// </summary>
        public void LoadSettings()
        {
            try
            {
//                 DebugLogger.Log($"[Jira] LoadSettings() loading from {_settingsPath}");

                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    JObject settings = JObject.Parse(json);
                    _email = settings["email"]?.ToString();
                    _apiToken = settings["apiToken"]?.ToString();
//                     DebugLogger.Log($"[Jira] Settings loaded: email={_email}");
                    UpdateHttpClientAuth();
                }
                else
                {
//                     DebugLogger.Log($"[Jira] Settings file not found at {_settingsPath}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves Jira settings to the settings file
        /// </summary>
        public void SaveSettings(string email, string apiToken)
        {
            try
            {
//                 DebugLogger.Log($"[Jira] SaveSettings() saving email={email}");

                string settingsDir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(settingsDir))
                {
//                     DebugLogger.Log($"[Jira] Creating settings directory: {settingsDir}");
                    Directory.CreateDirectory(settingsDir);
                }

                JObject settings = new JObject();
                settings["email"] = email;
                settings["apiToken"] = apiToken;

                File.WriteAllText(_settingsPath, settings.ToString());
//                 DebugLogger.Log($"[Jira] Settings saved to {_settingsPath}");

                _email = email;
                _apiToken = apiToken;
                UpdateHttpClientAuth();
//                 DebugLogger.Log("[Jira] HTTP client auth headers updated");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the Jira settings dialog
        /// </summary>
        public void ShowSettingsDialog(IWin32Window owner, bool isDarkMode = false)
        {
            using (JiraSettingsForm form = new JiraSettingsForm(_email, _apiToken, isDarkMode))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    SaveSettings(form.JiraEmail, form.JiraApiToken);
                }
            }
        }

        /// <summary>
        /// Gets all issues assigned to the current user
        /// </summary>
        public async Task<List<JiraIssue>> GetMyIssuesAsync()
        {
//             DebugLogger.Log("[Jira] GetMyIssuesAsync() called");

            if (!IsConfigured)
            {
                DebugLogger.Log("[Jira] ERROR: Jira not configured (missing email/token)");
                throw new InvalidOperationException("Jira is not configured. Please set email and API token.");
            }

            try
            {
                string jql = "assignee = currentUser() AND status != Done ORDER BY priority DESC";
//                 DebugLogger.Log($"[Jira] Executing JQL: {jql}");
                var issues = await ExecuteJqlQueryAsync(jql);
//                 DebugLogger.Log($"[Jira] Found {issues.Count} issue(s) assigned to current user");
                return issues;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR getting my issues: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets all issues in the active sprint
        /// </summary>
        public async Task<List<JiraIssue>> GetSprintIssuesAsync()
        {
//             DebugLogger.Log("[Jira] GetSprintIssuesAsync() called");

            if (!IsConfigured)
            {
                DebugLogger.Log("[Jira] ERROR: Jira not configured (missing email/token)");
                throw new InvalidOperationException("Jira is not configured. Please set email and API token.");
            }

            try
            {
                string jql = $"project = {ProjectKey} AND sprint in openSprints() ORDER BY priority DESC";
//                 DebugLogger.Log($"[Jira] Executing JQL: {jql}");
                var issues = await ExecuteJqlQueryAsync(jql);
//                 DebugLogger.Log($"[Jira] Found {issues.Count} issue(s) in active sprint(s)");
                return issues;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR getting sprint issues: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets project names for the SCRUM project
        /// </summary>
        public async Task<List<string>> GetProjectNamesAsync()
        {
//             DebugLogger.Log("[Jira] GetProjectNamesAsync() called");

            if (!IsConfigured)
            {
                DebugLogger.Log("[Jira] ERROR: Jira not configured (missing email/token)");
                throw new InvalidOperationException("Jira is not configured. Please set email and API token.");
            }

            List<string> projectNames = new List<string>();

            try
            {
                string url = $"{JiraBaseUrl}/projects/{ProjectKey}";
//                 DebugLogger.Log($"[Jira] GET {url}");
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    JObject projectObj = JObject.Parse(content);
                    string name = projectObj["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        projectNames.Add(name);
//                         DebugLogger.Log($"[Jira] Project name fetched: {name}");
                    }
                }
                else
                {
//                     DebugLogger.Log($"[Jira] API returned status {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR getting project names: {ex.Message}");
            }

            return projectNames;
        }

        /// <summary>
        /// Adds a worklog entry to an issue
        /// </summary>
        public async Task<bool> AddWorklogAsync(string issueKey, TimeSpan duration, string comment)
        {
//             DebugLogger.Log($"[Jira] AddWorklogAsync() for issue {issueKey}, duration={duration.TotalSeconds}s");

            if (!IsConfigured)
            {
                DebugLogger.Log("[Jira] ERROR: Jira not configured (missing email/token)");
                throw new InvalidOperationException("Jira is not configured. Please set email and API token.");
            }

            try
            {
                string url = $"{JiraBaseUrl}/issue/{issueKey}/worklog";

                JObject worklog = new JObject();
                worklog["timeSpentSeconds"] = (int)duration.TotalSeconds;
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    worklog["comment"] = comment;
                }

                string jsonContent = worklog.ToString();
                HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

//                 DebugLogger.Log($"[Jira] POST {url} - timeSpent={(int)duration.TotalSeconds}s, comment={(!string.IsNullOrWhiteSpace(comment) ? "yes" : "no")}");
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
//                     DebugLogger.Log($"[Jira] Worklog added successfully to {issueKey}");
                    return true;
                }
                else
                {
//                     DebugLogger.Log($"[Jira] API returned status {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR adding worklog: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Transitions an issue to a new status
        /// </summary>
        public async Task<bool> TransitionIssueAsync(string issueKey, string transitionName)
        {
//             DebugLogger.Log($"[Jira] TransitionIssueAsync() for issue {issueKey} to '{transitionName}'");

            if (!IsConfigured)
            {
                DebugLogger.Log("[Jira] ERROR: Jira not configured (missing email/token)");
                throw new InvalidOperationException("Jira is not configured. Please set email and API token.");
            }

            try
            {
                // First, get available transitions for the issue
                string getTransitionsUrl = $"{JiraBaseUrl}/issue/{issueKey}/transitions";
//                 DebugLogger.Log($"[Jira] GET {getTransitionsUrl} - fetching available transitions");
                HttpResponseMessage transitionsResponse = await _httpClient.GetAsync(getTransitionsUrl);

                if (!transitionsResponse.IsSuccessStatusCode)
                {
                    DebugLogger.Log($"[Jira] ERROR: Failed to fetch transitions, status {transitionsResponse.StatusCode}");
                    return false;
                }

                string transitionsContent = await transitionsResponse.Content.ReadAsStringAsync();
                JObject transitionsObj = JObject.Parse(transitionsContent);
                JArray transitions = transitionsObj["transitions"] as JArray;
//                 DebugLogger.Log($"[Jira] Found {transitions?.Count ?? 0} available transition(s)");

                string transitionId = null;
                foreach (var transition in transitions)
                {
                    if (transition["name"]?.ToString().Equals(transitionName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        transitionId = transition["id"]?.ToString();
//                         DebugLogger.Log($"[Jira] Found transition ID '{transitionId}' for '{transitionName}'");
                        break;
                    }
                }

                if (string.IsNullOrEmpty(transitionId))
                {
                    DebugLogger.Log($"[Jira] ERROR: Transition '{transitionName}' not found");
                    return false;
                }

                // Perform the transition
                string transitionUrl = $"{JiraBaseUrl}/issue/{issueKey}/transitions";
                JObject transitionRequest = new JObject();
                transitionRequest["transition"] = new JObject { ["id"] = transitionId };

                string jsonContent = transitionRequest.ToString();
                HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

//                 DebugLogger.Log($"[Jira] POST {transitionUrl} - executing transition {transitionId}");
                HttpResponseMessage response = await _httpClient.PostAsync(transitionUrl, content);

                if (response.IsSuccessStatusCode)
                {
//                     DebugLogger.Log($"[Jira] Transition successful for {issueKey}");
                    return true;
                }
                else
                {
                    DebugLogger.Log($"[Jira] Transition failed, status {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR transitioning issue: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes a JQL query and returns the issues
        /// </summary>
        private async Task<List<JiraIssue>> ExecuteJqlQueryAsync(string jql)
        {
            List<JiraIssue> issues = new List<JiraIssue>();

            try
            {
                string url = $"{JiraBaseUrl}/search?jql={Uri.EscapeDataString(jql)}&maxResults=100&fields=key,summary,status,priority,issuetype,assignee";

//                 DebugLogger.Log($"[Jira] ExecuteJqlQuery: {jql}");
//                 DebugLogger.Log($"[Jira] GET {JiraBaseUrl}/search");

                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
//                     DebugLogger.Log($"[Jira] API returned status 200");
                    string content = await response.Content.ReadAsStringAsync();
                    JObject responseObj = JObject.Parse(content);
                    JArray issuesArray = responseObj["issues"] as JArray;

                    if (issuesArray != null)
                    {
//                         DebugLogger.Log($"[Jira] Parsing {issuesArray.Count} issue(s)");

                        foreach (var issueToken in issuesArray)
                        {
                            JiraIssue issue = new JiraIssue
                            {
                                Key = issueToken["key"]?.ToString(),
                                Summary = issueToken["fields"]?["summary"]?.ToString(),
                                Status = issueToken["fields"]?["status"]?["name"]?.ToString(),
                                Priority = issueToken["fields"]?["priority"]?["name"]?.ToString(),
                                IssueType = issueToken["fields"]?["issuetype"]?["name"]?.ToString(),
                                Assignee = issueToken["fields"]?["assignee"]?["displayName"]?.ToString()
                            };

                            if (!string.IsNullOrEmpty(issue.Key))
                            {
                                issues.Add(issue);
//                                 DebugLogger.Log($"[Jira] Parsed issue: {issue.Key} - {issue.Summary}");
                            }
                        }
                    }
                    else
                    {
//                         DebugLogger.Log("[Jira] No issues array in response");
                    }
                }
                else
                {
//                     DebugLogger.Log($"[Jira] API returned status {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Jira] ERROR executing JQL query: {ex.Message}");
            }

//             DebugLogger.Log($"[Jira] JQL query returned {issues.Count} issue(s)");
            return issues;
        }

        /// <summary>
        /// Updates the HttpClient with Basic Auth headers
        /// </summary>
        private void UpdateHttpClientAuth()
        {
//             DebugLogger.Log("[Jira] UpdateHttpClientAuth() setting up authentication");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(_apiToken))
            {
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_email}:{_apiToken}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
//                 DebugLogger.Log("[Jira] Basic auth credentials set");
            }
            else
            {
//                 DebugLogger.Log("[Jira] WARNING: No credentials available for auth");
            }
        }
    }

    /// <summary>
    /// Settings dialog form for Jira configuration
    /// </summary>
    internal class JiraSettingsForm : Form
    {
        private TextBox _emailTextBox;
        private TextBox _apiTokenTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private Label _emailLabel;
        private Label _apiTokenLabel;
        private bool _isDarkMode;

        public string JiraEmail { get; private set; }
        public string JiraApiToken { get; private set; }

        public JiraSettingsForm(string email, string apiToken, bool isDarkMode = false)
        {
            _isDarkMode = isDarkMode;
            JiraEmail = email;
            JiraApiToken = apiToken;

            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Text = "Jira Settings";
            this.Width = 450;
            this.Height = 250;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Email Label
            _emailLabel = new Label();
            _emailLabel.Text = "Jira Email:";
            _emailLabel.Location = new System.Drawing.Point(20, 30);
            _emailLabel.Width = 150;
            _emailLabel.Height = 20;
            this.Controls.Add(_emailLabel);

            // Email TextBox
            _emailTextBox = new TextBox();
            _emailTextBox.Text = JiraEmail ?? string.Empty;
            _emailTextBox.Location = new System.Drawing.Point(170, 30);
            _emailTextBox.Width = 240;
            _emailTextBox.Height = 20;
            this.Controls.Add(_emailTextBox);

            // API Token Label
            _apiTokenLabel = new Label();
            _apiTokenLabel.Text = "API Token:";
            _apiTokenLabel.Location = new System.Drawing.Point(20, 80);
            _apiTokenLabel.Width = 150;
            _apiTokenLabel.Height = 20;
            this.Controls.Add(_apiTokenLabel);

            // API Token TextBox
            _apiTokenTextBox = new TextBox();
            _apiTokenTextBox.Text = JiraApiToken ?? string.Empty;
            _apiTokenTextBox.UseSystemPasswordChar = true;
            _apiTokenTextBox.Location = new System.Drawing.Point(170, 80);
            _apiTokenTextBox.Width = 240;
            _apiTokenTextBox.Height = 20;
            this.Controls.Add(_apiTokenTextBox);

            // Info Label
            Label infoLabel = new Label();
            infoLabel.Text = "Generate API tokens at: https://id.atlassian.com/manage/api-tokens";
            infoLabel.Location = new System.Drawing.Point(20, 120);
            infoLabel.Width = 390;
            infoLabel.Height = 40;
            infoLabel.AutoSize = true;
            this.Controls.Add(infoLabel);

            // OK Button
            _okButton = new Button();
            _okButton.Text = "OK";
            _okButton.Location = new System.Drawing.Point(250, 180);
            _okButton.Width = 80;
            _okButton.Height = 30;
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Click += (s, e) => SaveSettings();
            this.Controls.Add(_okButton);

            // Cancel Button
            _cancelButton = new Button();
            _cancelButton.Text = "Cancel";
            _cancelButton.Location = new System.Drawing.Point(340, 180);
            _cancelButton.Width = 80;
            _cancelButton.Height = 30;
            _cancelButton.DialogResult = DialogResult.Cancel;
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private void SaveSettings()
        {
            JiraEmail = _emailTextBox.Text.Trim();
            JiraApiToken = _apiTokenTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(JiraEmail) || string.IsNullOrWhiteSpace(JiraApiToken))
            {
                MessageBox.Show(this, "Please enter both email and API token.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
            }
        }

        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
                this.ForeColor = System.Drawing.Color.White;

                ApplyDarkThemeToControl(this);
            }
            else
            {
                this.BackColor = System.Drawing.SystemColors.Control;
                this.ForeColor = System.Drawing.SystemColors.ControlText;
            }
        }

        private void ApplyDarkThemeToControl(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl is TextBox tb)
                {
                    tb.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
                    tb.ForeColor = System.Drawing.Color.White;
                }
                else if (ctrl is Button btn)
                {
                    btn.BackColor = System.Drawing.Color.FromArgb(55, 55, 60);
                    btn.ForeColor = System.Drawing.Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 85);
                }
                else if (ctrl is Label lbl)
                {
                    lbl.ForeColor = System.Drawing.Color.White;
                }

                if (ctrl.HasChildren)
                    ApplyDarkThemeToControl(ctrl);
            }
        }
    }
}