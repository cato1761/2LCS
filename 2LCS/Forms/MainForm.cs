﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Linq.Dynamic;
using System.Diagnostics;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using LCS.JsonObjects;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net;
using System.Text.RegularExpressions;

namespace LCS.Forms
{
    public partial class MainForm : Form
    {
        private HttpClientHelper _httpClientHelper;
        private LcsProject _selectedProject;
        private bool _cheSortAscending = true;
        private bool _saasSortAscending = true;
        private CookieContainer _cookies;
        private static string _lcsUrl = "https://lcs.dynamics.com";
        private static string _lcsUpdateUrl = "https://update.lcs.dynamics.com";

        private List<ProjectInstance> Instances;
        private List<LcsProject> Projects;
        private List<CustomLink> Links;

        private List<CloudHostedInstance> _cheInstancesList;
        private List<CloudHostedInstance> _saasInstancesList;
        private readonly BindingSource _cheInstancesSource = new BindingSource();
        private readonly BindingSource _saasInstancesSource = new BindingSource();

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetGetCookieEx(string url, string cookieName, StringBuilder cookieData, ref int size, Int32 dwFlags, IntPtr lpReserved);
        private const int InternetCookieHttponly = 0x2000;
        private FormWindowState _previousState;
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, [Out] out RECT lpRect);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IntersectRect([Out] out RECT lprcDst, [In] ref RECT lprcSrc1, [In] ref RECT lprcSrc2);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const int GW_HWNDPREV = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            SetInitialGridSettings();

            Instances = JsonConvert.DeserializeObject<List<ProjectInstance>>(Properties.Settings.Default.instances) ??
                        new List<ProjectInstance>();
            Projects = JsonConvert.DeserializeObject<List<LcsProject>>(Properties.Settings.Default.projects) ?? new List<LcsProject>();

            if (!string.IsNullOrEmpty(Properties.Settings.Default.cookie))
            {
                _cookies = new CookieContainer();
                _cookies.SetCookies(new Uri(_lcsUrl), Properties.Settings.Default.cookie);
                _cookies.SetCookies(new Uri (_lcsUpdateUrl), Properties.Settings.Default.cookie);
                _httpClientHelper = new HttpClientHelper(_cookies)
                {
                    LcsUrl = _lcsUrl,
                    LcsUpdateUrl = _lcsUpdateUrl
                };

                changeProjectMenuItem.Enabled = true;
                cheInstanceContextMenu.Enabled = true;
                saasInstanceContextMenu.Enabled = true;
                loginToLcsMenuItem.Enabled = false;
                logoutToolStripMenuItem.Enabled = true;
                
                _selectedProject = GetLcsProjectFromCookie();
                if (_selectedProject != null)
                {
                    SetLcsProjectText();
                    refreshMenuItem.Enabled = true;
                    _httpClientHelper.ChangeLcsProjectId(_selectedProject.Id.ToString());
                    var projectInstance = Instances.FirstOrDefault(x => x.LcsProjectId.Equals(_selectedProject.Id));
                    if (projectInstance != null)
                    {
                        if(projectInstance.CheInstances != null)
                        {
                            _cheInstancesSource.DataSource = _cheInstancesList = projectInstance.CheInstances;
                        }
                        if(projectInstance.SaasInstances != null)
                        {
                            _saasInstancesSource.DataSource = _saasInstancesList = projectInstance.SaasInstances;
                        }
                    }
                }
            }
            CreateCustomLinksMenuItems();
        }

        private void SetLcsProjectText()
        {
            projectDescriptionLabel.Text = $"LCS Project ID: {_selectedProject.Id} : {_selectedProject.Name} : {_selectedProject.OrganizationName}";
        }

        private LcsProject GetLcsProjectFromCookie()
        {
            var cookies = _cookies.GetCookies(new Uri(_lcsUrl));
            foreach (Cookie cookie in cookies)
            {
                if(cookie.Name == "lcspid")
                {
                    return Projects.FirstOrDefault(x => x.Id.Equals(int.Parse(cookie.Value)));
                }
            }
            return null;
        }

        private void SetInitialGridSettings()
        {
            cheDataGridView.AutoGenerateColumns = false;
            saasDataGridView.AutoGenerateColumns = false;
            //Perf fix for grids rendering
            if (!SystemInformation.TerminalServerSession)
            {
                var dgvType = cheDataGridView.GetType();
                var pi = dgvType.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (pi != null)
                {
                    pi.SetValue(cheDataGridView, true, null);
                    dgvType = saasDataGridView.GetType();
                }

                pi = dgvType.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(saasDataGridView, true, null);
            }
            cheDataGridView.DataSource = _cheInstancesSource;
            saasDataGridView.DataSource = _saasInstancesSource;
        }

        private void CheDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (cheDataGridView.DataSource == null || _cheInstancesList == null) return;
            _cheInstancesSource.DataSource = _cheSortAscending ? _cheInstancesList.OrderBy(cheDataGridView.Columns[e.ColumnIndex].DataPropertyName).ToList() : _cheInstancesList.OrderBy(cheDataGridView.Columns[e.ColumnIndex].DataPropertyName).Reverse().ToList();
            _cheSortAscending = !_cheSortAscending;
            cheDataGridView.ClearSelection();
        }

        private void SaasDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (saasDataGridView.DataSource == null || _saasInstancesList == null) return;
            _saasInstancesSource.DataSource = _saasSortAscending ? _saasInstancesList.OrderBy(saasDataGridView.Columns[e.ColumnIndex].DataPropertyName).ToList() : _saasInstancesList.OrderBy(saasDataGridView.Columns[e.ColumnIndex].DataPropertyName).Reverse().ToList();
            _saasSortAscending = !_saasSortAscending;
            saasDataGridView.ClearSelection();
        }

        private void RefreshMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon.BalloonTipText = $"Fetching list of environments for project {_selectedProject.Name} from LCS. Please wait...";
            notifyIcon.BalloonTipTitle = "Fetching environments";

            notifyIcon.ShowBalloonTip(2000); //This setting might be overruled by the OS

            if (tabControl.SelectedTab == tabControl.TabPages["cheTabPage"])
            {
                RefreshChe();
                RefreshSaas();
            }
            else if (tabControl.SelectedTab == tabControl.TabPages["saasTabPage"])
            {
                RefreshSaas();
                RefreshChe();
            }
        }

        private void RefreshSaas(bool reloadFromLcs = true)
        {
            Cursor = Cursors.WaitCursor;
            if (reloadFromLcs)
            {
                _saasInstancesList = _httpClientHelper.GetSaasInstances();

                if (_saasInstancesList != null)
                {
                    _saasInstancesSource.DataSource = _saasInstancesList;
                    if (Instances.Exists(x => x.LcsProjectId == _selectedProject.Id))
                    {
                        Instances.Where(x => x.LcsProjectId == _selectedProject.Id)
                            .Select(x => { x.SaasInstances = _saasInstancesList; return x; })
                                .ToList();
                    }

                    Properties.Settings.Default.instances = JsonConvert.SerializeObject(Instances, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                var projectInstance = Instances?.FirstOrDefault(x => x.LcsProjectId.Equals(_selectedProject.Id));
                if (projectInstance != null)
                    _saasInstancesSource.DataSource = _saasInstancesList = projectInstance.SaasInstances;
            }
            _saasInstancesSource.ResetBindings(false);
            Cursor = Cursors.Default;
        }

        private void RefreshChe(bool reloadFromLcs = true)
        {
            Cursor = Cursors.WaitCursor;
            if (reloadFromLcs)
            {
                _cheInstancesList = _httpClientHelper.GetCheInstances();
                if (_cheInstancesList != null)
                {
                    _cheInstancesSource.DataSource = _cheInstancesList;
                    if (Instances.Exists(x => x.LcsProjectId == _selectedProject.Id))
                    {
                        Instances.Where(x => x.LcsProjectId == _selectedProject.Id)
                            .Select(x => { x.CheInstances = _cheInstancesList; return x; })
                                .ToList();
                    }

                    Properties.Settings.Default.instances = JsonConvert.SerializeObject(Instances, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                    Properties.Settings.Default.Save();
                }                
            }
            else
            {
                var projectInstance = Instances?.FirstOrDefault(x => x.LcsProjectId.Equals(_selectedProject.Id));
                if (projectInstance != null)
                    _cheInstancesSource.DataSource = _cheInstancesList = projectInstance.CheInstances;
            }
            _cheInstancesSource.ResetBindings(false);
            Cursor = Cursors.Default;
        }

        private void CheExportRDCManConnectionsMenuItem_Click(object sender, EventArgs e)
        {
            if (_cheInstancesList == null) return;
            Cursor = Cursors.WaitCursor;
            var sb = new StringBuilder();
            sb.Append(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<RDCMan schemaVersion=""1"">
    <version>2.2</version>
    <file>
        <properties>
            <name>CHE instances exported with LCS!</name>
            <expanded>True</expanded>
            <comment />
            <logonCredentials inherit=""FromParent"" />
            <connectionSettings inherit=""FromParent"" />
            <gatewaySettings inherit=""FromParent"" />
            <remoteDesktop inherit=""FromParent"" />
            <localResources inherit=""FromParent"" />
            <securitySettings inherit=""FromParent"" />
            <displaySettings inherit=""FromParent"" />
        </properties>");

            foreach (var instance in _cheInstancesList)
            {
                var rdpList = _httpClientHelper.GetRdpConnectionDetails(instance);
                foreach (var rdpEntry in rdpList)
                {
                    sb.Append(
                        $@"
        <server>
            <name>{rdpEntry.Address}:{rdpEntry.Port}</name>
            <displayName>{instance.InstanceId}-{rdpEntry.Machine}</displayName>
            <comment />
            <logonCredentials inherit=""None"">
                <userName>{rdpEntry.Username}</userName>
                <domain>{rdpEntry.Domain}</domain>
                <password storeAsClearText=""True"">{rdpEntry.Password}</password>
            </logonCredentials>
            <connectionSettings inherit=""FromParent"" />
            <gatewaySettings inherit=""FromParent"" />
            <remoteDesktop inherit=""FromParent"" />
            <localResources inherit=""FromParent"" />
            <securitySettings inherit=""FromParent"" />
            <displaySettings inherit=""FromParent"" />
        </server>");
                }
            }
            sb.Append(
                @"
    </file>
</RDCMan>");
            var savefile = new SaveFileDialog
            {
                FileName = "CHE-Exported.rdg",
                Filter = "RDCMan file (*.rdg)|*.rdg|All files (*.*)|*.*",
                DefaultExt = "rdg",
                AddExtension = true
            };
            if (savefile.ShowDialog() == DialogResult.OK)
            {
                using (StreamWriter sw = new StreamWriter(savefile.FileName))
                    sw.Write(sb);
            }
            Cursor = Cursors.Default;
        }

        private void SaasExportRDCManConnectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_saasInstancesList == null) return;
            Cursor = Cursors.WaitCursor;
            var sb = new StringBuilder();
            sb.Append(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<RDCMan schemaVersion=""1"">
    <version>2.2</version>
    <file>
        <properties>
            <name>SAAS instances exported with LCS!</name>
            <expanded>True</expanded>
            <comment />
            <logonCredentials inherit=""FromParent"" />
            <connectionSettings inherit=""FromParent"" />
            <gatewaySettings inherit=""FromParent"" />
            <remoteDesktop inherit=""FromParent"" />
            <localResources inherit=""FromParent"" />
            <securitySettings inherit=""FromParent"" />
            <displaySettings inherit=""FromParent"" />
        </properties>");

            foreach (var saasInstance in _saasInstancesList)
            {
                var instance = _httpClientHelper.GetSaasDeploymentDetail(saasInstance.EnvironmentId);
                sb.Append(
                    $@"
        <group>
            <properties>
                <name>{instance.InstanceId}</name>
                <expanded>True</expanded>
                <comment />
                <logonCredentials inherit=""FromParent"" />
                <connectionSettings inherit=""FromParent"" />
                <gatewaySettings inherit=""FromParent"" />
                <remoteDesktop inherit=""FromParent"" />
                <localResources inherit=""FromParent"" />
                <securitySettings inherit=""FromParent"" />
                <displaySettings inherit=""FromParent"" />
            </properties>");

                var rdpList = _httpClientHelper.GetRdpConnectionDetails(instance);
                foreach (var rdpEntry in rdpList)
                {
                    sb.Append(
                        $@"
            <server>
                <name>{rdpEntry.Address}:{rdpEntry.Port}</name>
                <displayName>{rdpEntry.Machine}</displayName>
                <comment />
                <logonCredentials inherit=""None"">
                    <userName>{rdpEntry.Username}</userName>
                    <domain>{rdpEntry.Domain}</domain>
                    <password storeAsClearText=""True"">{rdpEntry.Password}</password>
                </logonCredentials>
                <connectionSettings inherit=""FromParent"" />
                <gatewaySettings inherit=""FromParent"" />
                <remoteDesktop inherit=""FromParent"" />
                <localResources inherit=""FromParent"" />
                <securitySettings inherit=""FromParent"" />
                <displaySettings inherit=""FromParent"" />
            </server>");
                }
                sb.Append(
                    @"
        </group>");
            }
            sb.Append(
                @"
    </file>
</RDCMan>");
            var savefile = new SaveFileDialog
            {
                FileName = "SAAS-Exported.rdg",
                Filter = "RDCMan file (*.rdg)|*.rdg|All files (*.*)|*.*",
                DefaultExt = "rdg",
                AddExtension = true
            };
            if (savefile.ShowDialog() == DialogResult.OK)
            {
                using (StreamWriter sw = new StreamWriter(savefile.FileName))
                    sw.Write(sb);
            }
            Cursor = Cursors.Default;
        }

        private static CookieContainer GetUriCookieContainer()
        {
            CookieContainer cookies = null;
            var datasize = 8192 * 16;
            var cookieData = new StringBuilder(datasize);
            if (!InternetGetCookieEx(_lcsUrl, null, cookieData, ref datasize, InternetCookieHttponly, IntPtr.Zero))
            {
                if (datasize < 0)
                    return null;
                cookieData = new StringBuilder(datasize);
                if (!InternetGetCookieEx(
                    _lcsUrl,
                    null, cookieData,
                    ref datasize,
                    InternetCookieHttponly,
                    IntPtr.Zero))
                    return null;
            }
            if (cookieData.Length > 0)
            {
                cookies = new CookieContainer();
                Properties.Settings.Default.cookie = cookieData.ToString().Replace(';', ',');
                Properties.Settings.Default.Save();
                cookies.SetCookies(new Uri(_lcsUrl), Properties.Settings.Default.cookie);
                cookies.SetCookies(new Uri(_lcsUpdateUrl), Properties.Settings.Default.cookie);
            }
            return cookies;
        }

        private void SaasAddNsgRule_Click(object sender, EventArgs e)
        {
            using (var form = new AddNsg())
            {
                form.ShowDialog();
                if (form.Cancelled || (form.Rule == null)) return;
                Cursor = Cursors.WaitCursor;
                var tasks = new List<Task>();
                foreach (DataGridViewRow row in saasDataGridView.SelectedRows)
                {
                    tasks.Add(Task.Run(() => new HttpClientHelper(_cookies) {LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.AddNsgRule((CloudHostedInstance)row.DataBoundItem, form.Rule.FirstOrDefault().Key, form.Rule.FirstOrDefault().Value)));
                }
                Task.WhenAll(tasks).Wait();
                Cursor = Cursors.Default;
            }
        }

        private void ChangeProjectMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            using (var form = new ChooseProject())
            {
                form.HttpClientHelper = _httpClientHelper;
                form.ShowDialog();
                if (!form.Cancelled && (form.LcsProject != null))
                {
                    Projects = form.Projects;
                    if (_selectedProject == null || form.LcsProject.Id != _selectedProject.Id)
                    {
                        _cheInstancesSource.DataSource = null;
                        _cheInstancesSource.ResetBindings(false);
                        _saasInstancesSource.DataSource = null;
                        _saasInstancesSource.ResetBindings(false);
                        _selectedProject = form.LcsProject;

                        if(!Instances.Exists(x => x.LcsProjectId == _selectedProject.Id))
                        {
                            var instance = new ProjectInstance()
                            {
                                LcsProjectId = _selectedProject.Id,
                            };
                            Instances.Add(instance);
                        }
                    }
                    refreshMenuItem.Enabled = true;
                    _httpClientHelper.ChangeLcsProjectId(_selectedProject.Id.ToString());
                    _cookies = _httpClientHelper.CookieContainer;
                    GetLcsProjectFromCookie();
                    SetLcsProjectText();
                    RefreshChe(false);
                    RefreshSaas(false);
                }
            }
            Cursor = Cursors.Default;
        }

        private void SaasDeleteNsgRule_Click(object sender, EventArgs e)
        {
            using (var form = new DeleteNsg())
            {
                form.ShowDialog();
                if (form.Cancelled || (String.IsNullOrEmpty(form.Rule))) return;
                Cursor = Cursors.WaitCursor;
                var tasks = new List<Task>();
                foreach (DataGridViewRow row in saasDataGridView.SelectedRows)
                {
                    tasks.Add(Task.Run(() => new HttpClientHelper(_cookies) {LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.DeleteNsgRule((CloudHostedInstance)row.DataBoundItem, form.Rule)));
                }
                Task.WhenAll(tasks).Wait();
                Cursor = Cursors.Default;
            }
        }

        private void StartInstanceMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            var tasks = new List<Task>();
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                tasks.Add(Task.Run(() => new HttpClientHelper(_cookies) {LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.StartStopDeployment((CloudHostedInstance)row.DataBoundItem, "start")));
            }
            Task.WhenAll(tasks).Wait();
            Cursor = Cursors.Default;
        }

        private void StopInstanceMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            var tasks = new List<Task>();
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                tasks.Add(Task.Run(() => new HttpClientHelper(_cookies) {LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.StartStopDeployment((CloudHostedInstance)row.DataBoundItem, "stop")));
            }
            Task.WhenAll(tasks).Wait();
            Cursor = Cursors.Default;
        }

        private void DeallocateMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            var tasks = new List<Task>();
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var instance = (CloudHostedInstance)row.DataBoundItem;
                if (MessageBox.Show($"Deallocation is the step before deletion. Do you really want to deallocate {instance.DisplayName} instance?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    tasks.Add(Task.Run(() => new HttpClientHelper(_cookies){LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.StartStopDeployment(instance, "deallocate")));
                }
            }
            Task.WhenAll(tasks).Wait();
            Cursor = Cursors.Default;
        }

        private void DeleteMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            var tasks = new List<Task>();
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var instance = (CloudHostedInstance)row.DataBoundItem;
                if (MessageBox.Show($"Deletion cannot be cancelled or rolled back. Do you really want to delete {instance.DisplayName} instance?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    tasks.Add(Task.Run(() => new HttpClientHelper(_cookies) {LcsUrl = _lcsUrl, LcsUpdateUrl = _lcsUpdateUrl, LcsProjectId = _selectedProject.Id.ToString()}.DeleteEnvironment(instance)));
                }
            }
            Task.WhenAll(tasks).Wait();
            Cursor = Cursors.Default;
        }

        private void DataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hti = SelectedDataGridView.HitTest(e.X, e.Y);
            if (hti.RowIndex < 0 || SelectedDataGridView.Rows[hti.RowIndex].Selected) return;
            SelectedDataGridView.ClearSelection();
            SelectedDataGridView.Rows[hti.RowIndex].Selected = true;
        }

        private void CheShowPasswordsMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in cheDataGridView.SelectedRows)
            {
                var instance = (CloudHostedInstance)row.DataBoundItem;
                foreach (var vm in instance.Instances)
                {
                    var form = new Credentials
                    {
                        CredentialsDict = _httpClientHelper.GetCredentials(instance.EnvironmentId, vm.ItemName),
                        Caption = $"Instance: {instance.InstanceId}, VM: {vm.MachineName}"
                    };
                    form.Show();
                }
            }
        }

        private void SaasShowPasswordsMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in saasDataGridView.SelectedRows)
            {
                var credentials = new Dictionary<string, string>();
                var instance = (CloudHostedInstance)row.DataBoundItem;
                foreach (var vm in instance.Instances)
                {
                    var creds = _httpClientHelper.GetCredentials(instance.EnvironmentId, vm.ItemName);
                    credentials = credentials.Concat(creds).GroupBy(d => d.Key).ToDictionary(d => d.Key, d => d.First().Value);

                }
                foreach (var cred in instance.SqlAzureCredentials.Select(x => x.DeploymentItemName).Distinct())
                {
                    var creds = _httpClientHelper.GetCredentials(instance.EnvironmentId, cred);
                    credentials = credentials.Concat(creds).GroupBy(d => d.Key).ToDictionary(d => d.Key, d => d.First().Value);
                }
                var form = new Credentials
                {
                    Caption = $"Instance: {instance.InstanceId}",
                    CredentialsDict = credentials
                };
                form.Show();
            }
        }

        private void CustomLinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new CustomLinks())
            {
                form.ShowDialog();
                if (form.Cancelled) return;
                RemoveCustomLinksMenuItems();
                CreateCustomLinksMenuItems();
            }
        }

        private void InstanceContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var mousepos = MousePosition;
            if (!(sender is ContextMenuStrip cms)) return;
            var relMousePos = cms.PointToClient(mousepos);
            if (cms.ClientRectangle.Contains(relMousePos))
            {
                var dgvRelMousePos = SelectedDataGridView.PointToClient(mousepos);
                var hti = SelectedDataGridView.HitTest(dgvRelMousePos.X, dgvRelMousePos.Y);
                if (hti.RowIndex == -1)
                {
                    e.Cancel = true;
                }
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void CreateCustomLinksMenuItems()
        {
            Links = JsonConvert.DeserializeObject<List<CustomLink>>(Properties.Settings.Default.links);
            if (Links == null || Links.Count == 0)
                return;

            var cheCustomLinksMenuItem = new ToolStripMenuItem("Custom links")
            {
                Name = "cheCustomLinksMenuItem"
            };
            var saasCustomLinksMenuItem = new ToolStripMenuItem("Custom links")
            {
                Name = "saasCustomLinksMenuItem"
            };

            cheInstanceContextMenu.Items.Add(cheCustomLinksMenuItem);
            saasInstanceContextMenu.Items.Add(saasCustomLinksMenuItem);

            foreach (var link in Links)
            {
                ToolStripItem cheSubItem = new ToolStripMenuItem(link.LinkLabel)
                {
                    ToolTipText = link.Link
                };
                ToolStripItem saasSubItem = new ToolStripMenuItem(link.LinkLabel)
                {
                    ToolTipText = link.Link
                };
                cheSubItem.Click += CustomLinkClicked;
                saasSubItem.Click += CustomLinkClicked;
                cheCustomLinksMenuItem.DropDownItems.Add(cheSubItem);
                saasCustomLinksMenuItem.DropDownItems.Add(saasSubItem);
            }
        }

        private void RemoveCustomLinksMenuItems()
        {
            cheInstanceContextMenu.Items.RemoveByKey("cheCustomLinksMenuItem");
            saasInstanceContextMenu.Items.RemoveByKey("saasCustomLinksMenuItem");
        }

        private string ParseCustomLink(string template, CloudHostedInstance instance)
        {
            var replacements = new Dictionary<string, string>();
            foreach (var prop in instance.GetType().GetProperties())
            {
                var value = "";
                var val = prop.GetValue(instance, null);
                if(val != null)
                {
                    value = val.ToString();
                }
                replacements.Add(prop.Name, value);
            }
            return template.FormatPlaceholders(replacements);
        }

        private void CustomLinkClicked(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                var link = ParseCustomLink(((ToolStripMenuItem)sender).ToolTipText, item);
                try
                {
                    Process.Start(link);
                }
                catch { }
            }
            Cursor = Cursors.Default;
        }

        private void ProjectUsersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/v2/ProjectUserManagement/{_selectedProject.Id}");
        }

        private void OpenWorkItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/v2/WorkItemsManagement/{_selectedProject.Id}");
        }

        private void SubscriptionEstimatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://sizing.lcs.dynamics.com/SubscriptionEstimator/Estimate/{_selectedProject.Id}");
        }

        private void SubscriptionsAvailableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/V2/OrganizationOfferDetail/{_selectedProject.Id}");
        }

        private void ProjectSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/v2/ProjectSettings/{_selectedProject.Id}");
        }

        private void AssetLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/v2/AssetLibrary/{_selectedProject.Id}");
        }

        private void ServiceRequestsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/V2/WorkItemsManagement/{_selectedProject.Id}?ActiveVerticalPivot=3");
        }

        private void SupportIssuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://lcs.dynamics.com/V2/WorkItemsManagement/{_selectedProject.Id}?ActiveVerticalPivot=2");
        }

        private void SystemDiagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start($"https://diag.lcs.dynamics.com/Home/Index/{_selectedProject.Id}");
        }

        private void LogonToApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                foreach (var link in item.NavigationLinks)
                {
                    if (link.DisplayName == "Log on to environment")
                    {
                        Process.Start(link.NavigationUri);
                        break;
                    }
                }
            }
            Cursor = Cursors.Default;
        }

        private void InstanceDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                Process.Start(_httpClientHelper.GetEnvironmentDetailsUrl(item));
            }
            Cursor = Cursors.Default;
        }

        private void EnvironmentMonitoringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                Process.Start($"https://diag.lcs.dynamics.com/Monitoring/Index/{_selectedProject.Id}?environmentId={item.EnvironmentId}");
            }
            Cursor = Cursors.Default;
        }

        private void DetailedVersionInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                Process.Start($"https://diag.lcs.dynamics.com/BuildInfo/Index/{_selectedProject.Id}?lcsEnvironmentId={item.EnvironmentId}");
            }
            Cursor = Cursors.Default;
        }

        private void EnvironmentChangeHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                Process.Start($"https://lcs.dynamics.com/V2/EnvironmentHistory/{_selectedProject.Id}?LcsEnvironmentName={item.DisplayName}&EnvironmentId={item.EnvironmentId}&EnvironmentType={item.SaasEnvironmentType}");
            }
            Cursor = Cursors.Default;
        }

        private void DataPackagesHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var item = (CloudHostedInstance)row.DataBoundItem;
                Process.Start($"https://lcs.dynamics.com/V2/ConfigurationAndDataManagementHistory/{_selectedProject.Id}?environmentId={item.EnvironmentId}");
            }
            Cursor = Cursors.Default;
        }

        private DataGridView SelectedDataGridView => tabControl.SelectedTab == tabControl.TabPages["cheTabPage"] ? cheDataGridView : saasDataGridView;

        private void OpenRDPConnectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var rdpList = _httpClientHelper.GetRdpConnectionDetails((CloudHostedInstance)row.DataBoundItem);
                foreach (var rdpEntry in rdpList)
                {
                    using (new RdpCredentials(rdpEntry.Address, $"{rdpEntry.Domain}\\{rdpEntry.Username}", rdpEntry.Password))
                    {
                        var rdcProcess = new Process
                        {
                            StartInfo =
                            {
                                FileName = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\mstsc.exe"),
                                Arguments = "/v " + $"{rdpEntry.Address}:{rdpEntry.Port}"
                            }
                        };
                        rdcProcess.Start();
                    }
                }
            }
            Cursor = Cursors.Default;
        }

        private void ShowRDPDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var instance = (CloudHostedInstance)row.DataBoundItem;
                var rdpList = _httpClientHelper.GetRdpConnectionDetails(instance);
                var details = new StringBuilder();
                foreach (var rdpEntry in rdpList)
                {
                    if(details.Length != 0)
                    {
                        details.AppendLine();
                        details.AppendLine();
                    }
                    details.AppendLine($"Virtual machine name: {rdpEntry.Machine}");
                    details.AppendLine($"Hostname: {rdpEntry.Address}:{rdpEntry.Port}");
                    details.AppendLine($"Username: {rdpEntry.Domain}\\{rdpEntry.Username}");
                    details.Append($"Password: {rdpEntry.Password}");
                }
                var form = new RdpDetails
                {
                    Text = $"RDP connection details for {instance.DisplayName}",
                    Details = details.ToString()
                };
                form.Show();
            }
            Cursor = Cursors.Default;
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if(WindowState == FormWindowState.Normal || WindowState == FormWindowState.Maximized)
            {
                if (IsOverlapped(this))
                {
                    Activate();
                }
                else
                {
                    _previousState = WindowState;
                    WindowState = FormWindowState.Minimized;
                }
            }
            else if(WindowState == FormWindowState.Minimized)
            {
                WindowState = _previousState;
            }
        }

        private void LoginToLCSMenuItem_Click(object sender, EventArgs e)
        {
            WebBrowserHelper.FixBrowserVersion();
            using (var form = new Login())
            {
                form.ShowDialog();
                if (form.Cancelled) return;
                _cookies = GetUriCookieContainer();
                if (_cookies == null) return;
                _httpClientHelper = new HttpClientHelper(_cookies)
                {
                    LcsUrl = _lcsUrl,
                    LcsUpdateUrl = _lcsUpdateUrl
                };
                if (_selectedProject != null)
                {
                    _httpClientHelper.ChangeLcsProjectId(_selectedProject.Id.ToString());
                }
                changeProjectMenuItem.Enabled = true;
                cheInstanceContextMenu.Enabled = true;
                saasInstanceContextMenu.Enabled = true;
                logoutToolStripMenuItem.Enabled = true;
                loginToLcsMenuItem.Enabled = false;
                ChangeProjectMenuItem_Click(null, null);
            }
        }

        private void HotfixesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            var menuItem = (sender as ToolStripMenuItem);
            HotfixesType hotfixesType;
            var label = "";
            switch (menuItem.Name)
            {
                case "cheMetadataHotfixesToolStripMenuItem":
                case "saasApplicationMetadataHotfixesToolStripMenuItem":
                    hotfixesType = HotfixesType.Metadata;
                    label = "application metadata updates";
                    break;
                case "cheApplicationBinaryHotfixesToolStripMenuItem":
                case "saasApplicationBinaryHotfixesToolStripMenuItem":
                    hotfixesType = HotfixesType.ApplicationBinary;
                    label = "application binary hotfixes";
                    break;
                case "chePlatformHotfixesToolStripMenuItem":
                case "saasPlatformBinaryHotfixesToolStripMenuItem":
                    hotfixesType = HotfixesType.PlatformBinary;
                    label = "platform binary hotfixes";
                    break;
                default:
                //case "saasCriticalMetadataHotfixesToolStripMenuItem":
                    hotfixesType = HotfixesType.CriticalMetadata;
                    label = "critical metadata hotfixes";
                    break;
            }

            foreach (DataGridViewRow row in SelectedDataGridView.SelectedRows)
            {
                var diagId = _httpClientHelper.GetDiagEnvironmentId((CloudHostedInstance)row.DataBoundItem); 
                var kbs = _httpClientHelper.GetAvailableHotfixes(diagId, (int)hotfixesType);
                if (kbs == null)
                {
                    MessageBox.Show($"Request to get available updates failed. Please try again later.");
                    continue;
                }
                if (kbs.Count == 0)
                {
                    MessageBox.Show($"There are no {label} available for {((CloudHostedInstance)row.DataBoundItem).DisplayName} instance.");
                    continue;
                }
                using (var form = new AvailableKBs())
                {
                    form.Hotfixes = kbs;
                    form.Text = $"{kbs.Count} {label} available for {((CloudHostedInstance)row.DataBoundItem).DisplayName} instance.";
                    form.ShowDialog();
                }
            }
            Cursor = Cursors.Default;
        }

        private void LogoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                    $"When you log off, all locally saved information about your projects and instances will be deleted. You will need to refresh data from LCS.{Environment.NewLine}{Environment.NewLine}Application will restart.{Environment.NewLine}{Environment.NewLine}Do you want to proceed?",
                    "Confirmation", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            Properties.Settings.Default.projects = "";
            Properties.Settings.Default.instances = "";
            Properties.Settings.Default.cookie = "";
            Properties.Settings.Default.Save();
            Application.Restart();
        }

        public static bool IsOverlapped(IWin32Window window)
        {
            if (window == null)
                throw new ArgumentNullException("window");
            if (window.Handle == IntPtr.Zero)
                throw new InvalidOperationException("Window does not yet exist");
            if (!IsWindowVisible(window.Handle))
                return false;

            IntPtr hWnd = window.Handle;
            HashSet<IntPtr> visited = new HashSet<IntPtr> { hWnd };

            RECT thisRect = new RECT();
            GetWindowRect(hWnd, out thisRect);

            while ((hWnd = GetWindow(hWnd, GW_HWNDPREV)) != IntPtr.Zero && !visited.Contains(hWnd))
            {
                visited.Add(hWnd);
                RECT testRect, intersection;
                testRect = intersection = new RECT();
                if (IsWindowVisible(hWnd) && GetWindowRect(hWnd, out testRect) && IntersectRect(out intersection, ref thisRect, ref testRect))
                {
                    return true;
                }
            }
            return false;
        }
        public void SetLoginButtonEnabled()
        {
            loginToLcsMenuItem.Enabled = true;
        }
    }

    public enum HotfixesType
    {
        Metadata = 8,
        PlatformBinary = 11,
        ApplicationBinary = 9,
        CriticalMetadata = 16
    }

    public static class StringExtension
    {
        private static readonly Regex RegexMatch = new Regex(@"\%\%([^\}]+)\%\%", RegexOptions.Compiled);
        public static string FormatPlaceholders(this string str, Dictionary<string, string> fields)
        {
            return fields == null ? str : RegexMatch.Replace(str, match => fields[match.Groups[1].Value]);
        }
    }
}
