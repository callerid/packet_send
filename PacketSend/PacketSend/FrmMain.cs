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

namespace PacketSend
{
    public partial class FrmMain : Form
    {
        private List<WiresharkFile> WiresharkFiles = new List<WiresharkFile>();
        private List<TextBox> TBFiles = new List<TextBox>();
        private List<TextBox> TBPacketCounts = new List<TextBox>();
        private List<CheckBox> CKBRuns = new List<CheckBox>();

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

        public FrmMain()
        {
            InitializeComponent();
            rtbConsole.BackColor = Color.White;

            // Setup all wireshark files
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);
            WiresharkFiles.Add(null);

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
                        TBFiles[file_count].Text = child_node.ChildNodes.Count > 0 ? (child_node.ChildNodes[0].Value == null ? "" : child_node.ChildNodes[0].Value) : "";

                        if (!string.IsNullOrEmpty(TBFiles[file_count].Text))
                        {
                            WiresharkFiles[file_count] = new WiresharkFile(TBFiles[file_count].Text);
                            TBPacketCounts[file_count].Text = WiresharkFiles[file_count].PacketCount.ToString();
                        }

                        CKBRuns[file_count].Checked = bool.Parse(child_node.Attributes[0].Value);

                        file_count++;
                    }
                }
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

            string file_name = GetFile();

            if (file_name == "error") return;

            WiresharkFiles[file_number] = new WiresharkFile(file_name);
            TBFiles[file_number].Text = file_name;
            TBPacketCounts[file_number].Text = WiresharkFiles[file_number].PacketCount.ToString();

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
                }
            }

            return -1;
        }

        private string GetFile()
        {
            OpenFileDialog ofdGetFild = new OpenFileDialog();
            ofdGetFild.Filter = "Wireshark files (*.cap, *.pcap) | *.cap; *.pcap";
            DialogResult r = ofdGetFild.ShowDialog();

            if(r == DialogResult.OK)
            {
                return ofdGetFild.FileName;
            }

            return "error";

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
                    TBFiles[i].BackColor = Color.LightBlue;
                    Common.WaitFor(10);
                    
                    if(run_speed == WiresharkFile.RunSpeeds.Original)
                    {
                        WiresharkFiles[i].RunFile();
                    }
                    else
                    {
                        WiresharkFiles[i].RunFileWithSpeed(run_speed);
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

            TBFiles[file_number].BackColor = Color.LightBlue;
            Common.WaitFor(10);

            if(run_speed == WiresharkFile.RunSpeeds.Original)
            {
                WiresharkFiles[file_number].RunFile();
            }
            else
            {
                WiresharkFiles[file_number].RunFileWithSpeed(run_speed);
            }

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
                    xml_writer.WriteString(string.IsNullOrEmpty(TBFiles[i].Text) ? "" : TBFiles[i].Text);
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
        }

        private void btnClearConsole_Click(object sender, EventArgs e)
        {
            rtbConsole.Text = "";
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

            tbFileInfoFilename.Text = WiresharkFiles[file_number].FileName;
            tbFileInfoSize.Text = WiresharkFiles[file_number].FileSizeKB.ToString() + "KB";
            tbFileInfoCreated.Text = WiresharkFiles[file_number].FileCreatedOn;
            tbFileInfoNumberOfPackets.Text = WiresharkFiles[file_number].PacketCount + " (SIP: " + WiresharkFiles[file_number].SIP_Packets + "  ::  RTP: " + WiresharkFiles[file_number].RTP_Packets + ")";


            if(rbOriginal.Checked)
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
    }
}
