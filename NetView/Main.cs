using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace NetViewer
{
    public partial class Main : Form
    {


        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        private Process[] processlist;
        private IEnumerable<NetStatItem> conItems;



        public Main()
        {
            InitializeComponent();
            processlist = Process.GetProcesses();
        }
        private void tm_Tick(object sender, EventArgs e)
        {
            if (this.tm.Interval != 5000)
            {
                this.tm.Stop();
                this.tm.Interval = 5000;
                this.tm.Start();
            }
            doWork();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            this.tm.Stop();
            this.tm.Interval = 15;
            this.tm.Start();
        }

        private void doWork()
        {
            string[] lines;
            using (Process cmd = new Process())
            {
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.CreateNoWindow = true;

                cmd.Start();
                cmd.StandardInput.WriteLine("netstat -anob");
                cmd.StandardInput.WriteLine("exit");
                var raw = cmd.StandardOutput.ReadToEnd();
                cmd.Close();
                lines = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
            List<NetStatItem> items = new List<NetStatItem>();
            NetStatItem con = null;
            foreach (string ln in lines)
            {
                if (ln.EndsWith("exit", StringComparison.InvariantCultureIgnoreCase))
                    break;
                if (Regex.IsMatch(ln, @"^\s*(TCP|UDP)"))
                {
                    if (con != null)
                    {
                        foreach (Process process in processlist)
                        {
                            if (process.Id == con.Pid)
                            {
                                con.Program = process.ProcessName;
                                con.ProgramPath = GetProcessName(con.Pid) ?? "";
                            }
                        }
                        items.Add(con);
                        con = null;
                    }
                    Regex StockregExp = new Regex(@"(?<Protocol>TCP|UDP)\s*(?<fromip>([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})|(\[[a-fA-F0-9:%]{0,}\])):(?<fromPort>[0-9]{0,5})\s*(?<toip>([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})|(\[[a-fA-F0-9:\%*]{0,}\])|\*\:\*):?(?<toPort>[0-9]{0,5})\s*(?<state>[a-zA-Z_]{0,})\s*(?<pid>[0-9]{1,5})");
                    Match m = StockregExp.Match(ln);
                    if (m.Success)
                    {
                        con = new NetStatItem();
                        con.Protocol = m.Groups["Protocol"].Value.Trim();
                        con.SourceIp = m.Groups["fromip"].Value.Trim();
                        con.SourcePort = m.Groups["fromPort"].Value.Trim();
                        con.TargetIp = m.Groups["toip"].Value.Trim();
                        con.TargetPort = m.Groups["toPort"].Value.Trim();
                        con.Status = m.Groups["state"].Value.Trim();
                        if (int.TryParse(m.Groups["pid"].Value, out int id))
                            con.Pid = id;
                    }
                }
                else
                {
                    if (con != null)
                    {
                        if (!string.IsNullOrEmpty(con.Program) && string.IsNullOrEmpty(con.ProgramPath))
                            con.ProgramPath = ln.Trim();
                        if (string.IsNullOrEmpty(con.Program))
                            con.Program = ln.Trim();
                    }
                }
            }
            if (comboBox1.SelectedIndex <= 0)
                conItems = items;
            else
                conItems = from el in items where el.Status == comboBox1.Text select el;


            int indx;
            if (listView1.SelectedIndices.Count > 0)
                indx = listView1.SelectedIndices[0];
            else
                indx = 0;

            SetSortDir(columnSort);
            listView1_ColumnClick(this, new ColumnClickEventArgs(columnSort));
            if (conItems.Count() > 0)
            {
                listView1.VirtualListSize = conItems.Count();

                if (indx == -1)
                    indx = 0;
                if (indx > listView1.Items.Count - 1)
                    indx = listView1.Items.Count - 1;

                listView1.Items[indx].Selected = true;
                listView1.Items[indx].EnsureVisible();
                return;
            }
            listView1.VirtualListSize = 0;
        }


        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var el = conItems.ElementAt(e.ItemIndex);

            e.Item = new ListViewItem(el.Program);
            e.Item.SubItems.Add(el.SourceIp);
            e.Item.SubItems.Add(el.SourcePort);
            e.Item.SubItems.Add(el.TargetIp);
            e.Item.SubItems.Add(el.TargetPort);
            e.Item.SubItems.Add(el.Protocol);
            e.Item.SubItems.Add(el.Status);
            e.Item.SubItems.Add(el.Pid.ToString());
            if (!string.IsNullOrEmpty(el.ProgramPath))
                e.Item.SubItems.Add(el.ProgramPath);
            else
                e.Item.SubItems.Add("");
        }

        private void listView1_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    ListViewItem itm = listView1.GetItemAt(e.X, e.Y);
                    if (listView1.SelectedIndices.Count == 0 && itm != null)
                    {
                        listView1.SelectedIndices.Clear();
                        itm.Selected = true;
                        itm.EnsureVisible();
                    }
                    if (listView1.SelectedIndices.Count > 0)
                        this.mnuSearchTargetIP.Enabled = true;
                    else
                        this.mnuSearchTargetIP.Enabled = false;
                    this.mnuUrlSearch.Show(listView1, e.Location);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        #region Sort on column click
        private int columnSort = 0;
        private int mLastSortColumn = -1;

        private void listView1_ColumnClick(object sender, System.Windows.Forms.ColumnClickEventArgs e)
        {

            if (conItems == null || conItems.Count() == 0)
                return;

            SortOrder dir = SetSortDir(e.Column);
            columnSort = e.Column;
            switch (columnSort)
            {
                case 0:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.Program) : conItems.OrderByDescending((u) => u.Program);
                    break;
                case 1:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.SourceIp) : conItems.OrderByDescending((u) => u.SourceIp);
                    break;
                case 2:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.SourcePort) : conItems.OrderByDescending((u) => u.SourcePort);
                    break;
                case 3:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.TargetIp) : conItems.OrderByDescending((u) => u.TargetIp);
                    break;
                case 4:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.TargetPort) : conItems.OrderByDescending((u) => u.TargetPort);
                    break;
                case 5:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.Protocol) : conItems.OrderByDescending((u) => u.Protocol);
                    break;
                case 6:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.Status) : conItems.OrderByDescending((u) => u.Status);
                    break;
                case 7:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.Pid) : conItems.OrderByDescending((u) => u.Pid);
                    break;
                default:
                    conItems = (dir == SortOrder.Ascending) ? conItems.OrderBy((u) => u.ProgramPath) : conItems.OrderByDescending((u) => u.ProgramPath);
                    break;
            }
            listView1.Invalidate();
        }
        private SortOrder GetSortDir(int column)
        {
            if (listView1.Columns[column].Text.StartsWith("«"))
                return SortOrder.Ascending;
            else if (listView1.Columns[column].Text.StartsWith("»"))
                return SortOrder.Descending;
            else
                return SortOrder.None;
        }


        private SortOrder SetSortDir(int column)
        {
            SortOrder dir = SortOrder.None;
            if (mLastSortColumn != column)
                ClearSort();
            else
                dir = GetSortDir(column);

            mLastSortColumn = column;
            switch (dir)
            {
                case SortOrder.Ascending:
                    listView1.Columns[column].Text = listView1.Columns[column].Text.Replace("«", "»");
                    listView1.Sorting = SortOrder.Descending;
                    return SortOrder.Descending;
                case SortOrder.Descending:
                    listView1.Columns[column].Text = listView1.Columns[column].Text.Replace("»", "«");
                    listView1.Sorting = SortOrder.Ascending;
                    return SortOrder.Ascending;
                default:
                    listView1.Columns[column].Text = "«" + listView1.Columns[column].Text;
                    listView1.Sorting = SortOrder.Ascending;
                    return SortOrder.Ascending;
            }
        }

        private void ClearSort()
        {
            if (mLastSortColumn > -1)
            {
                if (listView1.Columns[mLastSortColumn].Text.StartsWith("«"))
                    listView1.Columns[mLastSortColumn].Text = listView1.Columns[mLastSortColumn].Text.Replace("«", "");
                else if (listView1.Columns[mLastSortColumn].Text.StartsWith("»"))
                    listView1.Columns[mLastSortColumn].Text = listView1.Columns[mLastSortColumn].Text.Replace("»", "");
                mLastSortColumn = -1;
            }
        }

        #endregion  Sort on column click


        private void mnuSearchTargetIP_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                var id = listView1.SelectedIndices[0];

                var uu = Uri.EscapeDataString(conItems.ElementAt(id).TargetIp);

                IPAddress address;
                if (IPAddress.TryParse(conItems.ElementAt(id).TargetIp, out address))
                {
                    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && AddressValidate(address))
                        Process.Start("http://www.google.com.au/search?q=" + Uri.EscapeDataString(address.ToString()));
                }
            }
        }
        private bool AddressValidate(IPAddress ip)
        {
            return !IPAddress.Any.Equals(ip) && !IPAddress.Broadcast.Equals(ip) && !IPAddress.Loopback.Equals(ip);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetProcessName(int pid)
        {
            var processHandle = OpenProcess(0x0400 | 0x0010, false, pid);
            if (processHandle == IntPtr.Zero)
                return null;
            const int lengthSb = 2000;
            var sb = new StringBuilder(lengthSb);
            string result = null;
            if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, lengthSb) > 0)
                result = sb.ToString();
            CloseHandle(processHandle);
            return result;
        }
    }
}
