using PacketSend.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PcapDotNet.Core;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace PacketSend
{
    public partial class FrmMain : Form
    {
        private List<WiresharkFile> WiresharkFiles = new List<WiresharkFile>();
        private List<TextBox> TBFiles = new List<TextBox>();
        private List<TextBox> TBPacketCounts = new List<TextBox>();
        private List<CheckBox> CKBRuns = new List<CheckBox>();
        private List<Button> BTNStops = new List<Button>();
        private List<Button> BTNOpens = new List<Button>();

        private bool _saved = false;
        private bool Saved
        {
            get
            {
                return _saved;
            }

            set
            {
                _saved = value;

                if(!_saved)
                {
                    btnSaveConfig.BackColor = Color.Pink;
                    Text = "Packet Send ***(UNSAVED CONFIG)***";
                }
                else
                {
                    btnSaveConfig.BackColor = SystemColors.Control;
                    Text = "Packet Send";
                }
            }
        }

        private int CurrentFileNumber = -1;

        public FrmMain()
        {
            InitializeComponent();
            lbVersion.Text = "Version: " + ProductVersion;
            rtbConsole.BackColor = Color.White;
            rtbSIPConsole.BackColor = Color.White;

            // Setup all wireshark files
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);

            // Setup buttons
            BTNStops.Add(btnStopFile1);
            BTNStops.Add(btnStopFile2);
            BTNStops.Add(btnStopFile3);
            BTNStops.Add(btnStopFile4);
            BTNStops.Add(btnStopFile5);
            BTNStops.Add(btnStopFile6);

            BTNOpens.Add(btnOpenFile1);
            BTNOpens.Add(btnOpenFile2);
            BTNOpens.Add(btnOpenFile3);
            BTNOpens.Add(btnOpenFile4);
            BTNOpens.Add(btnOpenFile5);
            BTNOpens.Add(btnOpenFile6);

            // Setup textboxes
            TBFiles.Add(tbFile1);
            TBFiles.Add(tbFile2);
            TBFiles.Add(tbFile3);
            TBFiles.Add(tbFile4);
            TBFiles.Add(tbFile5);
            TBFiles.Add(tbFile6);

            TBPacketCounts.Add(tbPacketCount1);
            TBPacketCounts.Add(tbPacketCount2);
            TBPacketCounts.Add(tbPacketCount3);
            TBPacketCounts.Add(tbPacketCount4);
            TBPacketCounts.Add(tbPacketCount5);
            TBPacketCounts.Add(tbPacketCount6);

            // Setup checkboxes
            CKBRuns.Add(ckbNext1);
            CKBRuns.Add(ckbNext2);
            CKBRuns.Add(ckbNext3);
            CKBRuns.Add(ckbNext4);
            CKBRuns.Add(ckbNext5);
            CKBRuns.Add(ckbNext6);

            // Get Network Adapters
            WiresharkFile.UpdateNetworkAdapters();
            WiresharkFile.ConsoleText = rtbConsole;
            WiresharkFile.SIPConsoleText = rtbSIPConsole;
            WiresharkFile.StopFeatureTimeInterval = 50;

            ckbCleanMode.Text = "Clean Mode (Only SIP && RTP - " + WiresharkFile.StopFeatureTimeInterval + " ms interval)";

            // Fill drop down with network adapters
            cbNetworkAdapters.Items.Clear();
            foreach(LivePacketDevice device in WiresharkFile.NetworkAdapters)
            {
                foreach(DeviceAddress address in device.Addresses)
                {
                    if (address.Address.ToString().Contains("Internet6")) continue;
                    cbNetworkAdapters.Items.Add(address.Address.ToString());
                }
            }

            if(cbNetworkAdapters.Items.Count > 0)
            {
                cbNetworkAdapters.SelectedIndex = 0;
                WiresharkFile.SetSelectedNetworkAdapter(cbNetworkAdapters.Items[cbNetworkAdapters.SelectedIndex].ToString());
            }

            // Load previous config if possible
            if(!string.IsNullOrEmpty(Properties.Settings.Default.CurrentConfigFile))
            {
                LoadConfig(Properties.Settings.Default.CurrentConfigFile);
            }

        }

        private void LoadConfig(string filename)
        {
            if (!File.Exists(filename)) return;

            XmlDocument xml_read = new XmlDocument();
            xml_read.Load(filename);

            foreach(XmlNode node in xml_read.DocumentElement.ChildNodes)
            {
                if(node.Name == "interval_speed")
                {
                    switch(node.ChildNodes[0].Value)
                    {
                        case "Original":
                            rbOriginal.Checked = true;
                            break;
                        case "100":
                            rb100.Checked = true;
                            break;
                        case "250":
                            rb250.Checked = true;
                            break;
                        case "500":
                            rb500.Checked = true;
                            break;
                        case "1000":
                            rb1000.Checked = true;
                            break;
                        case "Zero":
                            rbZero.Checked = true;
                            break;
                        default:
                            rbOriginal.Checked = true;
                            break;
                    }
                }

                if(node.Name == "files")
                {
                    int file_count = 0;
                    foreach(XmlNode child_node in node.ChildNodes)
                    {
                        string full_path = child_node.ChildNodes.Count > 0 ? (child_node.ChildNodes[0].Value == null ? "" : child_node.ChildNodes[0].Value) : "";
                        string only_filename = full_path.Substring(full_path.LastIndexOf("\\") + 1);

                        TBFiles[file_count].Text = only_filename;

                        if (!string.IsNullOrEmpty(TBFiles[file_count].Text))
                        {
                            WiresharkFiles[file_count] = new WiresharkFile(full_path);
                            TBPacketCounts[file_count].Text = WiresharkFiles[file_count].PacketCount.ToString();
                        }

                        CKBRuns[file_count].Checked = bool.Parse(child_node.Attributes[0].Value);

                        file_count++;
                    }
                }
            }

            for(int i = 0; i < 5; i++)
            {
                if (WiresharkFiles[i] == null) continue;

                string info_file_tb_name = "tbFile1";

                switch (i)
                {
                    case 0:
                        info_file_tb_name = "tbFile1";
                        break;
                    case 1:
                        info_file_tb_name = "tbFile2";
                        break;
                    case 2:
                        info_file_tb_name = "tbFile3";
                        break;
                    case 3:
                        info_file_tb_name = "tbFile4";
                        break;
                    case 4:
                        info_file_tb_name = "tbFile5";
                        break;
                    case 5:
                        info_file_tb_name = "tbFile6";
                        break;
                }

                TextBox info_tb_file = new TextBox() { Name = info_file_tb_name };
                tbFile_Click(info_tb_file, null);
            }

            Properties.Settings.Default.CurrentConfigFile = filename;
            lbCurrentConfigFile.Text = Properties.Settings.Default.CurrentConfigFile;
            Saved = true;

            Common.ConsoleWriteLine(rtbConsole);
            Common.ConsoleWriteLine(rtbConsole, "Config File Loaded:\n   " + filename);
        }

        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            int file_number = GetFileNumber(sender);

            if (file_number == -1) return;

            List<string> file_names = GetFiles();

            if (file_names == null) return;

            if(file_names.Count == 1)
            {
                WiresharkFiles[file_number] = new WiresharkFile(file_names[0]);
                TBFiles[file_number].Text = file_names[0].Substring(file_names[0].LastIndexOf("\\") + 1);
                TBPacketCounts[file_number].Text = WiresharkFiles[file_number].PacketCount.ToString();

                string info_file_tb_name = "tbFile1";

                switch (file_number)
                {
                    case 0:
                        info_file_tb_name = "tbFile1";
                        break;
                    case 1:
                        info_file_tb_name = "tbFile2";
                        break;
                    case 2:
                        info_file_tb_name = "tbFile3";
                        break;
                    case 3:
                        info_file_tb_name = "tbFile4";
                        break;
                    case 4:
                        info_file_tb_name = "tbFile5";
                        break;
                    case 5:
                        info_file_tb_name = "tbFile6";
                        break;
                }

                TextBox info_tb_file = new TextBox() { Name = info_file_tb_name };
                tbFile_Click(info_tb_file, null);
            }
            else
            {
                for (int i = 0; i < file_names.Count; i++)
                {
                    if (i > 5) break;

                    WiresharkFiles[i] = new WiresharkFile(file_names[i]);
                    TBFiles[i].Text = file_names[i].Substring(file_names[i].LastIndexOf("\\") + 1);
                    TBPacketCounts[i].Text = WiresharkFiles[file_number].PacketCount.ToString();

                    string info_file_tb_name = "tbFile1";

                    switch (i)
                    {
                        case 0:
                            info_file_tb_name = "tbFile1";
                            break;
                        case 1:
                            info_file_tb_name = "tbFile2";
                            break;
                        case 2:
                            info_file_tb_name = "tbFile3";
                            break;
                        case 3:
                            info_file_tb_name = "tbFile4";
                            break;
                        case 4:
                            info_file_tb_name = "tbFile5";
                            break;
                        case 5:
                            info_file_tb_name = "tbFile6";
                            break;
                    }

                    TextBox info_tb_file = new TextBox() { Name = info_file_tb_name };
                    tbFile_Click(info_tb_file, null);

                }
            }

            Saved = false;

        }

        private int GetFileNumber(object sender)
        {

            if(sender is TextBox)
            {
                TextBox tb_clicked = (TextBox)sender;

                switch (tb_clicked.Name)
                {
                    case "tbFile1":
                        return 0;
                    case "tbFile2":
                        return 1;
                    case "tbFile3":
                        return 2;
                    case "tbFile4":
                        return 3;
                    case "tbFile5":
                        return 4;
                    case "tbFile6":
                        return 5;
                }
            }

            if (sender is Button)
            {
                Button btn_clicked = (Button)sender;

                switch (btn_clicked.Name)
                {
                    case "btnLoadFile1":
                        return 0;
                    case "btnLoadFile2":
                        return 1;
                    case "btnLoadFile3":
                        return 2;
                    case "btnLoadFile4":
                        return 3;
                    case "btnLoadFile5":
                        return 4;
                    case "btnLoadFile6":
                        return 5;

                    case "btnOpenFile1":
                        return 0;
                    case "btnOpenFile2":
                        return 1;
                    case "btnOpenFile3":
                        return 2;
                    case "btnOpenFile4":
                        return 3;
                    case "btnOpenFile5":
                        return 4;
                    case "btnOpenFile6":
                        return 5;

                    case "btnRunFile1":
                        return 0;
                    case "btnRunFile2":
                        return 1;
                    case "btnRunFile3":
                        return 2;
                    case "btnRunFile4":
                        return 3;
                    case "btnRunFile5":
                        return 4;
                    case "btnRunFile6":
                        return 5;

                    case "btnStopFile1":
                        return 0;
                    case "btnStopFile2":
                        return 1;
                    case "btnStopFile3":
                        return 2;
                    case "btnStopFile4":
                        return 3;
                    case "btnStopFile5":
                        return 4;
                    case "btnStopFile6":
                        return 5;
                }
            }

            return -1;
        }

        private List<string> GetFiles()
        {
            OpenFileDialog ofdGetFile = new OpenFileDialog();
            ofdGetFile.Multiselect = true;
            ofdGetFile.Filter = "Wireshark files (*.cap, *.pcap) | *.cap; *.pcap";
            DialogResult r = ofdGetFile.ShowDialog();

            if(r == DialogResult.OK)
            {
                List<string> files = new List<string>();

                for (int i = 0; i < ofdGetFile.FileNames.Length; i++)
                {
                    if (i > 5) break;
                    files.Add(ofdGetFile.FileNames[i]);
                }

                return files;
            }

            return null;

        }

        private void cbNetworkAdapters_SelectedIndexChanged(object sender, EventArgs e)
        {
            WiresharkFile.SetSelectedNetworkAdapter(cbNetworkAdapters.Items[cbNetworkAdapters.SelectedIndex].ToString());
        }

        private void btnRunSequence_Click(object sender, EventArgs e)
        {
            WiresharkFile.RunSpeeds run_speed = WiresharkFile.RunSpeeds.Original;

            if(rbOriginal.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.Original;
            }
            else if(rb100.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s100;
            }
            else if (rb250.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s250;
            }
            else if (rb500.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s500;
            }
            else if (rb1000.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s1000;
            }

            for(int i = 0; i < 6; i++)
            {
                if (WiresharkFiles[i] == null) continue;

                if(CKBRuns[i].Checked)
                {

                    string info_file_tb_name = "tbFile1";

                    switch (i)
                    {
                        case 0:
                            info_file_tb_name = "tbFile1";
                            break;
                        case 1:
                            info_file_tb_name = "tbFile2";
                            break;
                        case 2:
                            info_file_tb_name = "tbFile3";
                            break;
                        case 3:
                            info_file_tb_name = "tbFile4";
                            break;
                        case 4:
                            info_file_tb_name = "tbFile5";
                            break;
                        case 5:
                            info_file_tb_name = "tbFile6";
                            break;
                    }

                    TextBox info_tb_file = new TextBox() { Name = info_file_tb_name };
                    tbFile_Click(info_tb_file, null);

                    TBFiles[i].BackColor = Color.LightBlue;
                    Common.WaitFor(10);
                    
                    if(run_speed == WiresharkFile.RunSpeeds.Original)
                    {
                        if (ckbCleanMode.Checked)
                        {
                            WiresharkFiles[i].RunWithStopFeature();
                        }
                        else
                        {
                            WiresharkFiles[i].RunFile();
                        }
                    }
                    else
                    {
                        if (ckbCleanMode.Checked)
                        {
                            WiresharkFiles[i].RunWithStopFeature();
                        }
                        else
                        {
                            WiresharkFiles[i].RunFileWithSpeed(run_speed);
                        }
                    }

                    TBFiles[i].BackColor = Color.LightGreen;
                }
            }

            for(int i = 0; i < 6; i++)
            {
                TBFiles[i].BackColor = SystemColors.Control;
            }
        }

        private void btnRunFile_Click(object sender, EventArgs e)
        {
            int file_number = GetFileNumber(sender);

            if (file_number == -1) return;

            if (WiresharkFiles[file_number] == null) return;

            WiresharkFile.StopRunningAllFiles = false;

            WiresharkFile.RunSpeeds run_speed = WiresharkFile.RunSpeeds.Original;

            if (rbOriginal.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.Original;
            }
            else if (rb100.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s100;
            }
            else if (rb250.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s250;
            }
            else if (rb500.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s500;
            }
            else if (rb1000.Checked)
            {
                run_speed = WiresharkFile.RunSpeeds.s1000;
            }

            string info_file_tb_name = "tbFile1";

            switch (file_number)
            {
                case 0:
                    info_file_tb_name = "tbFile1";
                    break;
                case 1:
                    info_file_tb_name = "tbFile2";
                    break;
                case 2:
                    info_file_tb_name = "tbFile3";
                    break;
                case 3:
                    info_file_tb_name = "tbFile4";
                    break;
                case 4:
                    info_file_tb_name = "tbFile5";
                    break;
                case 5:
                    info_file_tb_name = "tbFile6";
                    break;
            }

            TextBox info_tb_file = new TextBox() { Name = info_file_tb_name };
            tbFile_Click(info_tb_file, null);

            TBFiles[file_number].BackColor = Color.LightBlue;
            Common.WaitFor(10);

            if (ckbLoopSend.Checked)
            {
                for (int i = 0; i < ndLoops.Value; i++)
                {
                    lbCurrentLoop.Text = "Loop: " + (i + 1).ToString() + " of " + ndLoops.Value;

                    Application.DoEvents();

                    if (run_speed == WiresharkFile.RunSpeeds.Original)
                    {
                        if (ckbCleanMode.Checked)
                        {
                            WiresharkFiles[file_number].RunWithStopFeature();
                        }
                        else
                        {
                            WiresharkFiles[file_number].RunFile();
                        }
                    }
                    else
                    {
                        if (ckbCleanMode.Checked)
                        {
                            WiresharkFiles[file_number].RunWithStopFeature();
                        }
                        else
                        {
                            WiresharkFiles[file_number].RunFileWithSpeed(run_speed);
                        }
                    }
                }
            }
            else
            {
                if (run_speed == WiresharkFile.RunSpeeds.Original)
                {
                    if (ckbCleanMode.Checked)
                    {
                        WiresharkFiles[file_number].RunWithStopFeature();
                    }
                    else
                    {
                        WiresharkFiles[file_number].RunFile();
                    }
                }
                else
                {
                    if (ckbCleanMode.Checked)
                    {
                        WiresharkFiles[file_number].RunWithStopFeature();
                    }
                    else
                    {
                        WiresharkFiles[file_number].RunFileWithSpeed(run_speed);
                    }
                }
            }

            lbCurrentLoop.Text = "Not Running";
            TBFiles[file_number].BackColor = SystemColors.Control;

        }

        private void btnSaveAsConfig_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }

        private void SaveConfig(string filename = "")
        {
            if(filename == "")
            {
                SaveFileDialog sfdSaveAs = new SaveFileDialog();
                sfdSaveAs.Filter = "Packet Send Configs (*.tfg) | *.tfg";

                DialogResult r = sfdSaveAs.ShowDialog();

                if (r != DialogResult.OK) return;

                filename = sfdSaveAs.FileName;
            }

            if (string.IsNullOrEmpty(filename)) return;

            using (FileStream fileStream = new FileStream(filename, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(fileStream))
            using (XmlTextWriter xml_writer = new XmlTextWriter(sw))
            {
                xml_writer.Formatting = Formatting.Indented;
                xml_writer.Indentation = 4;


                xml_writer.WriteStartDocument();
                xml_writer.WriteStartElement("packet_send_config");

                xml_writer.WriteStartElement("interval_speed");

                string interval = "Original";

                if (rb100.Checked)
                {
                    interval = "100";
                }
                else if (rb250.Checked)
                {
                    interval = "250";
                }
                else if (rb500.Checked)
                {
                    interval = "500";
                }
                else if (rb1000.Checked)
                {
                    interval = "1000";
                }
                else if (rbZero.Checked)
                {
                    interval = "Zero";
                }
                else
                {
                    interval = "Original";
                }

                xml_writer.WriteString(interval);
                xml_writer.WriteEndElement();

                xml_writer.WriteStartElement("files"); // Start files
                for (int i = 0; i < 6; i++)
                {
                    xml_writer.WriteStartElement("file"); // Start file
                    xml_writer.WriteAttributeString("run_enabled", CKBRuns[i].Checked ? "True" : "False");

                    if (WiresharkFiles[i] == null)
                    {
                        xml_writer.WriteString("");
                    }
                    else
                    {
                        xml_writer.WriteString(string.IsNullOrEmpty(WiresharkFiles[i].FileName) ? "" : WiresharkFiles[i].FileName);
                    }

                    xml_writer.WriteEndElement(); // End file
                }

                xml_writer.WriteEndElement(); // End files
                xml_writer.WriteEndElement(); // End packet_send_config

                xml_writer.WriteEndDocument();
                xml_writer.Close();

                Properties.Settings.Default.CurrentConfigFile = filename;
                lbCurrentConfigFile.Text = Properties.Settings.Default.CurrentConfigFile;
                Saved = true;

                Common.ConsoleWriteLine(rtbConsole, "");
                Common.ConsoleWriteLine(rtbConsole, "Config File Saved:\n   " + filename);

            }
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofdLoadConfig = new OpenFileDialog();
            ofdLoadConfig.Filter = "Packet Send Configs (*.tfg) | *.tfg";

            DialogResult r = ofdLoadConfig.ShowDialog();

            if (r != DialogResult.OK) return;

            LoadConfig(ofdLoadConfig.FileName);

        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();

            if(!Saved)
            {
                DialogResult r = MessageBox.Show("Config File has been changed. Save changes?", "Changes Made", MessageBoxButtons.YesNoCancel);

                if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (r == DialogResult.Yes)
                {
                    SaveConfig(Properties.Settings.Default.CurrentConfigFile);
                }
            }
        }

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(Properties.Settings.Default.CurrentConfigFile))
            {
                SaveConfig();
            }
            else
            {
                SaveConfig(Properties.Settings.Default.CurrentConfigFile);
            }
        }

        private void ChangesMade(object sender, EventArgs e)
        {
            Saved = false;
            UpdateEstTime();
        }

        private void btnClearConsole_Click(object sender, EventArgs e)
        {
            rtbConsole.Text = "";
            rtbSIPConsole.Text = "";
        }

        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            rbOriginal.Checked = true;

            WiresharkFiles.Clear();

            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);

            for (int i = 0; i < 6; i++)
            {
                TBFiles[i].Text = "";
                CKBRuns[i].Checked = false;
                TBPacketCounts[i].Text = "";
            }

            Saved = true;
            Properties.Settings.Default.CurrentConfigFile = "";
            lbCurrentConfigFile.Text = Properties.Settings.Default.CurrentConfigFile;

        }

        private void tbFile_Click(object sender, EventArgs e)
        {
            int file_number = GetFileNumber(sender);

            if (file_number == -1) return;

            if (WiresharkFiles[file_number] == null) return;

            CurrentFileNumber = file_number;

            tbFileInfoFilename.Text = WiresharkFiles[file_number].FileName;
            tbFileInfoSize.Text = WiresharkFiles[file_number].FileSizeKB.ToString() + "KB";
            tbFileInfoCreated.Text = WiresharkFiles[file_number].FileCreatedOn;
            tbFileInfoNumberOfPackets.Text = WiresharkFiles[file_number].PacketCount + " (SIP: " + WiresharkFiles[file_number].SIP_Packets + "  ::  RTP: " + WiresharkFiles[file_number].RTP_Packets + ")";
            tbFileInfoSIPGateway.Text = WiresharkFiles[file_number].SIPGateway.ToString();

            if(ckbCleanMode.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[file_number].DetailedPackets * WiresharkFile.StopFeatureTimeInterval) / 1000.0f);
            }
            else if(rbOriginal.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime(WiresharkFiles[file_number].FileEstTime);
            }
            else if(rb100.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[file_number].PacketCount * 100) / 1000000.0f);
            }
            else if (rb250.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[file_number].PacketCount * 250) / 1000000.0f);
            }
            else if (rb500.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[file_number].PacketCount * 500) / 1000000.0f);
            }
            else if (rb1000.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[file_number].PacketCount * 1000) / 1000000.0f);
            }
        }

        private void UpdateEstTime()
        {
            if (CurrentFileNumber == -1) return;

            if (WiresharkFiles[CurrentFileNumber] == null) return;

            if (ckbCleanMode.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[CurrentFileNumber].DetailedPackets * WiresharkFile.StopFeatureTimeInterval) / 1000.0f);
            }
            else if (rbOriginal.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime(WiresharkFiles[CurrentFileNumber].FileEstTime);
            }
            else if (rb100.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[CurrentFileNumber].PacketCount * 100) / 1000000.0f);
            }
            else if (rb250.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[CurrentFileNumber].PacketCount * 250) / 1000000.0f);
            }
            else if (rb500.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[CurrentFileNumber].PacketCount * 500) / 1000000.0f);
            }
            else if (rb1000.Checked)
            {
                tbFileInfoEstTime.Text = Common.ConvertSecondsToReadableTime((WiresharkFiles[CurrentFileNumber].PacketCount * 1000) / 1000000.0f);
            }
        }

        private void btnStopFile_Click(object sender, EventArgs e)
        {
            WiresharkFile.StopRunningAllFiles = true;
        }

        private void btnOpenFile_Click(object sender, EventArgs e)
        {
            int file_number = GetFileNumber(sender);

            if (file_number == -1) return;

            if (WiresharkFiles[file_number] == null) return;

            if(File.Exists(WiresharkFiles[file_number].FileName))
            {
                Process.Start(WiresharkFiles[file_number].FileName);
            }

        }

        private void ckbCleanMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateEstTime();

            if(ckbCleanMode.Checked)
            {
                rb100.Enabled = false;
                rb1000.Enabled = false;
                rb250.Enabled = false;
                rb500.Enabled = false;
                rbOriginal.Enabled = false;
                rbZero.Enabled = false;

                rb10ms.Enabled = true;
                rb100ms.Enabled = true;
                rb25ms.Enabled = true;
                rb50ms.Enabled = true;
            }
            else
            {
                rb100.Enabled = true;
                rb1000.Enabled = true;
                rb250.Enabled = true;
                rb500.Enabled = true;
                rbOriginal.Enabled = true;
                rbZero.Enabled = true;

                rb10ms.Enabled = false;
                rb100ms.Enabled = false;
                rb25ms.Enabled = false;
                rb50ms.Enabled = false;
            }
        }

        private void btnCopySIPGateway_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(tbFileInfoSIPGateway.Text);
            lbLastCopy.Text = "Last Copy: " + DateTime.Now.ToShortTimeString();
        }

        private void rbCleanModeTimeInterval_Click(object sender, EventArgs e)
        {
            if (rb10ms.Checked)
            {
                WiresharkFile.StopFeatureTimeInterval = 10;
            }
            else if (rb25ms.Checked)
            {
                WiresharkFile.StopFeatureTimeInterval = 25;
            }
            else if (rb50ms.Checked)
            {
                WiresharkFile.StopFeatureTimeInterval = 50;
            }
            else if (rb100ms.Checked)
            {
                WiresharkFile.StopFeatureTimeInterval = 100;
            }

            ckbCleanMode.Text = "Clean Mode (Only SIP && RTP - " + WiresharkFile.StopFeatureTimeInterval + " ms interval)";
            UpdateEstTime();
        }

        private void rb10ms_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
