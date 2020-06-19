using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.Icmp;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;

namespace PacketSend.Classes
{

    class WiresharkFile
    {
        public static RichTextBox ConsoleText;
        public static RichTextBox SIPConsoleText;

        public static IList<LivePacketDevice> NetworkAdapters = new List<LivePacketDevice>();
        private static PacketDevice SelectedNetworkAdapter = null;

        public static void UpdateNetworkAdapters()
        {
            // Retrieve the device list from the local machine
            NetworkAdapters = LivePacketDevice.AllLocalMachine;

            if (NetworkAdapters.Count == 0)
            {
                MessageBox.Show("No Netword Adapters found.");
                return;
            }

        }

        public static void SetSelectedNetworkAdapter(string item)
        {
            int index = -1;
            int cnt = 0;

            foreach (LivePacketDevice device in NetworkAdapters)
            {
                foreach (DeviceAddress address in device.Addresses)
                {
                    if(address.Address.ToString() == item)
                    {
                        index = cnt;
                        break;
                    }
                }

                cnt++;
            }

            if(index == -1)
            {
                MessageBox.Show("Could not set Selected Adapter.");
            }

            SelectedNetworkAdapter = NetworkAdapters[index];
        }

        public enum RunSpeeds
        {
            Original,
            Zero,
            s100,
            s250,
            s500,
            s1000
        }

        // Wireshark File Instances
        public string FileName = "";
        public OfflinePacketDevice OfflineDevice;
        public int PacketCount = 0;
        private int TimePacketCount = 0;
        public int RunningSpeedPacketCount = 0;
        private List<Packet> AlteredPackets = new List<Packet>();
        public RunSpeeds RunSpeed = RunSpeeds.Original;
        public int RTP_Packets = 0;
        public int SIP_Packets = 0;
        public long FileSizeKB = 0;
        public string FileCreatedOn = "";
        public MacAddress SIPGateway = new MacAddress("00:00:00:00:00:00");

        private long _file_est_time = 0;
        public long FileEstTime
        {
            get
            {
                return (long)EndTime.Subtract(StartTime).TotalSeconds;
            }

            set
            {
                _file_est_time = value;
            }
        }

        private DateTime StartTime;
        private DateTime EndTime;

        // Stop feature
        public static bool StopRunningAllFiles = false;
        public int PacketsSentBeforeStop = 0;
        public int PacketsNotSentAfterStop = 0;
        public static int StopFeatureTimeInterval = 100; // Speed of detailed mode
        public int DetailedPackets = 0;

        public WiresharkFile(string filepath)
        {
            if (!File.Exists(filepath)) return;

            FileName = filepath;
            PacketCount = 0;
            TimePacketCount = 0;
            FileInfo file = new FileInfo(filepath);
            FileSizeKB = file.Length / 1000;
            FileCreatedOn = file.CreationTime.ToString();
            FileEstTime = 0;

            // Create the offline device
            OfflineDevice = new OfflinePacketDevice(filepath);

            // Count packets
            using (PacketCommunicator communicator =
                OfflineDevice.Open(65536,                                   // portion of the packet to capture
                                                                            // 65536 guarantees that the whole packet will be captured on all the link layers
                                    PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                    1000))                                  // read timeout
            {
                // Read and dispatch packets until EOF is reached
                communicator.ReceivePackets(0, ProcessNewFilePackets);
            }

            // Estimate time
            using (PacketCommunicator communicator =
                OfflineDevice.Open(65536,                                   // portion of the packet to capture
                                                                            // 65536 guarantees that the whole packet will be captured on all the link layers
                                    PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                    1000))                                  // read timeout
            {
                // Read and dispatch packets until EOF is reached
                communicator.ReceivePackets(0, EstimateTime);
            }

            Common.ConsoleWriteLine(ConsoleText);
            Common.ConsoleWriteLine(ConsoleText, "File Loaded: " + FileName.Substring(FileName.LastIndexOf("\\") + 1) + "\n   - SIP Packets = " + SIP_Packets + "\n   - RTP Packets = " + RTP_Packets);
        }

        public void RunFileWithSpeed(RunSpeeds run_speed)
        {
            if (string.IsNullOrEmpty(FileName)) return;
            if (run_speed == RunSpeeds.Original) return;
            if (PacketCount == 0 || SelectedNetworkAdapter == null) return;
            
            int time_interval = 100;

            switch(run_speed)
            {
                case RunSpeeds.Original:
                case RunSpeeds.s100:
                    time_interval = 100;
                    break;
                case RunSpeeds.Zero:
                    time_interval = 1;
                    break;
                case RunSpeeds.s250:
                    time_interval = 250;
                    break;
                case RunSpeeds.s500:
                    time_interval = 500;
                    break;
                case RunSpeeds.s1000:
                    time_interval = 1000;
                    break;
                default:
                    time_interval = 100;
                    break;
            }

            DateTime time_stamp = DateTime.Now;

            // Open the capture file
            OfflinePacketDevice selectedInputDevice = new OfflinePacketDevice(FileName);

            using (PacketCommunicator inputCommunicator =
                selectedInputDevice.Open(65536, // portion of the packet to capture
                                                // 65536 guarantees that the whole packet will be captured on all the link layers
                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                         1000)) // read timeout
            {
                // Open the output device
                using (PacketCommunicator outputCommunicator =
                    SelectedNetworkAdapter.Open(100, PacketDeviceOpenAttributes.Promiscuous, 1000))
                {
                    // Fill the buffer with the packets from the file
                    AlteredPackets = new List<Packet>();
                    Packet packet;
                    int packet_count = 0;
                    while (inputCommunicator.ReceivePacket(out packet) == PacketCommunicatorReceiveResult.Ok)
                    {
                        // Create the builder that will build our packets
                        EthernetLayer ethernet_layer = packet.Ethernet == null ? null : (EthernetLayer)packet.Ethernet.ExtractLayer();
                        IpV4Layer ipv4_layer = packet.Ethernet.IpV4 == null ? null : (IpV4Layer)packet.Ethernet.IpV4.ExtractLayer();
                        IcmpLayer icmp_layer = packet.Ethernet.IpV4.Icmp == null ? null : (IcmpLayer)packet.Ethernet.IpV4.Icmp.ExtractLayer();
                        TransportLayer transport_layer = packet.Ethernet.IpV4.Transport == null ? null : (TransportLayer)packet.Ethernet.IpV4.Transport.ExtractLayer();
                        PayloadLayer datagram_layer = packet.Ethernet.IpV4.Payload == null ? null : (PayloadLayer)packet.Ethernet.IpV4.Payload.ExtractLayer();

                        try
                        {
                            if(ipv4_layer.Length < 1) // Catch null Length
                            {
                                // Do Nothing
                            }
                        }
                        catch
                        {
                            ipv4_layer = null;
                        }

                        List<ILayer> layers = new List<ILayer>();

                        if(IsRTP(packet))
                        {
                            if (ethernet_layer != null) layers.Add(ethernet_layer);
                            if (ipv4_layer != null) layers.Add(ipv4_layer);
                            if (datagram_layer != null) layers.Add(datagram_layer);
                        }
                        else
                        {
                            if (ethernet_layer != null) layers.Add(ethernet_layer);
                            if (ipv4_layer != null) layers.Add(ipv4_layer);
                            if (icmp_layer != null) layers.Add(icmp_layer);

                            if (transport_layer != null) layers.Add(transport_layer);
                            if (datagram_layer != null && IsRTP(packet)) layers.Add(datagram_layer);
                        }

                        PacketBuilder builder = new PacketBuilder(layers);
                        
                        Packet altered_packet = builder.Build(time_stamp.AddMilliseconds((packet_count * time_interval) / 1000));
                        ProcessAlteredPacket(altered_packet);

                        packet_count++;
                    }

                    using (PacketSendBuffer sendBuffer = new PacketSendBuffer(4294967295))
                    {

                        foreach(Packet p in AlteredPackets)
                        {
                            sendBuffer.Enqueue(p);
                        }

                        // Transmit the queue
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        long startTimeMs = stopwatch.ElapsedMilliseconds;
                        Common.ConsoleWriteLine(ConsoleText);
                        Common.ConsoleWriteLine(ConsoleText, "File:\n   " + FileName.Substring(FileName.LastIndexOf("\\") + 1));
                        Common.ConsoleWriteLine(ConsoleText, "   Start Time: " + startTimeMs);
                        outputCommunicator.Transmit(sendBuffer, true);
                        long endTimeMs = stopwatch.ElapsedMilliseconds;
                        Common.ConsoleWriteLine(ConsoleText, "   End Time: " + endTimeMs);
                        long elapsedTimeMs = endTimeMs - startTimeMs;
                        Common.ConsoleWriteLine(ConsoleText, "   Elapsed Time: " + elapsedTimeMs);
                        double averagePacketsPerSecond = elapsedTimeMs == 0 ? double.MaxValue : (double)AlteredPackets.Count / elapsedTimeMs * 1000;

                        Common.ConsoleWriteLine(ConsoleText, "   Elapsed time: " + elapsedTimeMs + " ms");
                        Common.ConsoleWriteLine(ConsoleText, "   Total packets generated = " + AlteredPackets.Count);
                        Common.ConsoleWriteLine(ConsoleText, "   Average packets per second = " + averagePacketsPerSecond);
                        Common.ConsoleWriteLine(ConsoleText, "");
                    }
                }
            }
        }

        private void ProcessAlteredPacket(Packet altered_packet)
        {
            AlteredPackets.Add(altered_packet);
        }

        public void RunFile()
        {
            if (string.IsNullOrEmpty(FileName)) return;

            if (PacketCount == 0 || SelectedNetworkAdapter == null) return;

            // Retrieve the length of the capture file
            long capLength = new FileInfo(FileName).Length;
            
            // Open the capture file
            OfflinePacketDevice selectedInputDevice = new OfflinePacketDevice(FileName);

            using (PacketCommunicator inputCommunicator =
                selectedInputDevice.Open(65536, // portion of the packet to capture
                                                // 65536 guarantees that the whole packet will be captured on all the link layers
                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                         1000)) // read timeout
            {
                using (PacketCommunicator outputCommunicator =
                    SelectedNetworkAdapter.Open(100, PacketDeviceOpenAttributes.Promiscuous, 1000))
                {
                    // Check the MAC type
                    if (inputCommunicator.DataLink != outputCommunicator.DataLink)
                    {
                        Console.WriteLine(
                            "Warning: the datalink of the capture differs from the one of the selected interface.");
                        Console.WriteLine("Press a key to continue, or CTRL+C to stop.");
                        //Console.ReadKey();
                    }

                    // Allocate a send buffer
                    using (PacketSendBuffer sendBuffer = new PacketSendBuffer((uint)capLength))
                    {
                        // Fill the buffer with the packets from the file
                        int numPackets = 0;
                        Packet packet;
                        while (inputCommunicator.ReceivePacket(out packet) == PacketCommunicatorReceiveResult.Ok)
                        {
                            sendBuffer.Enqueue(packet);
                            ++numPackets;
                        }

                        // Transmit the queue
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        long startTimeMs = stopwatch.ElapsedMilliseconds;
                        Common.ConsoleWriteLine(ConsoleText);
                        Common.ConsoleWriteLine(ConsoleText, "File:\n   " + FileName.Substring(FileName.LastIndexOf("\\") + 1));
                        Common.ConsoleWriteLine(ConsoleText, "   Start Time: " + startTimeMs);
                        outputCommunicator.Transmit(sendBuffer, true);
                        long endTimeMs = stopwatch.ElapsedMilliseconds;
                        Common.ConsoleWriteLine(ConsoleText, "   End Time: " + endTimeMs);
                        long elapsedTimeMs = endTimeMs - startTimeMs;
                        Common.ConsoleWriteLine(ConsoleText, "   Elapsed Time: " + elapsedTimeMs);
                        double averagePacketsPerSecond = elapsedTimeMs == 0 ? double.MaxValue : (double)numPackets / elapsedTimeMs * 1000;

                        Common.ConsoleWriteLine(ConsoleText, "   Elapsed time: " + elapsedTimeMs + " ms");
                        Common.ConsoleWriteLine(ConsoleText, "   Total packets generated = " + numPackets);
                        Common.ConsoleWriteLine(ConsoleText, "   Average packets per second = " + averagePacketsPerSecond);
                        Common.ConsoleWriteLine(ConsoleText, "");
                    }
                }
            }
        }

        private bool IsInternalIP(string[] ip_parts)
        {
            /*
            * Internal Schemes
                10.0.0.0 to 10.255.255.255 - Class A
                172.16.0.0 to 172.31.255.255 - Class B
                192.168.0.0 to 192.168.255.255 - Class C
            */

            if (int.Parse(ip_parts[0]) == 10) return true;
            if (int.Parse(ip_parts[0]) == 172 && int.Parse(ip_parts[1]) == 16) return true;
            if (int.Parse(ip_parts[0]) == 192 && int.Parse(ip_parts[1]) == 168) return true;

            return false;

        }

        private void ProcessNewFilePackets(Packet packet)
        {
            PacketCount++;

            if (IsSIP(packet))
            {
                SIP_Packets++;
                DetailedPackets++;

                string type = GetSIPType(packet);

                if (type == "Invite" && SIPGateway == new MacAddress("00:00:00:00:00:00"))
                {
                    string[] source_ip = packet.Ethernet.IpV4.Source.ToString().Split('.');
                    string[] destination_ip = packet.Ethernet.IpV4.Destination.ToString().Split('.');

                    if (!IsInternalIP(source_ip))
                    {
                        SIPGateway = packet.Ethernet.Source;
                    }
                    else
                    {
                        SIPGateway = packet.Ethernet.Destination;
                    }
                }

                if (FilterPacket(type) != "No Filter")
                {
                    DetailedPackets--;
                }

            }

            if (IsRTP(packet))
            {
                RTP_Packets++;
                DetailedPackets++;
            }

            if(PacketCount == 1)
            {
                StartTime = packet.Timestamp;
            }
        }

        private string FilterPacket(string type)
        {
            if (
                type == "Register" ||
                type == "ACK" ||
                type == "Update" ||
                type == "PRACK" ||
                type == "Subscribe" ||
                type == "Notify" ||
                type == "Publish" ||
                type == "Message" ||
                type == "Info" ||
                type == "Options" ||
                type.Contains("181") ||
                type.Contains("182") ||
                type.Contains("183") ||
                type.Contains("199") ||
                type.Contains("202") ||
                type.Contains("204"))
            {

                return type;

            }

            return "No Filter";
        }

        private void EstimateTime(Packet packet)
        {
            TimePacketCount++;

            if (TimePacketCount == 1)
            {
                StartTime = packet.Timestamp;
            }

            if(TimePacketCount == PacketCount)
            {
                EndTime = packet.Timestamp;
            }
        }

        public static bool IsSIP(Packet packet)
        {
            byte[] packet_bytes = packet.ToArray<Byte>();
            string ascii_text = Encoding.ASCII.GetString(packet_bytes);

            if (ascii_text.ToLower().Contains("sip")) return true;

            return false;
        }
        
        public static int[] RTP_LENGTHS = { 74, 78, 86, 214, 216, 218, 220, 222, 224, 226, 230, 232 };
        public static bool IsRTP(Packet packet)
        {
            int packet_length = packet.Length;

            foreach(int rtp_length in RTP_LENGTHS)
            {
                if (packet_length == rtp_length) return true;
            }

            return false;
        }

        // SIP types
        private string GetSIPType(Packet packet)
        {
            string ascii_text = Encoding.ASCII.GetString(packet.Ethernet.IpV4.Payload.ToArray());

            if(ascii_text.IndexOf("sip") == -1)
            {
                if(ascii_text.Length > 12)
                {
                    return "Unknown: " + ascii_text.Substring(0, 12);
                }
                return "Unknown! (no hint)";
            }

            string raw_type = ascii_text.Substring(8, ascii_text.IndexOf("sip") - 8).ToLower();

            if (raw_type.Contains("invite"))
            {
                return "Invite";
            }

            if (raw_type.Contains("trying"))
            {
                return "Trying";
            }

            if (raw_type.Contains("ringing"))
            {
                return "Ringing";
            }

            if (raw_type.Contains("200 ok"))
            {
                return "200 OK";
            }

            if (raw_type.Contains("ack"))
            {
                return "ACK";
            }

            if (raw_type.Contains("bye"))
            {
                return "Bye";
            }

            if (raw_type.Contains("cancel"))
            {
                return "Cancel";
            }

            if (raw_type.Contains("notify"))
            {
                return "Notify";
            }

            if (raw_type.Contains("register"))
            {
                return "Register";
            }

            if (raw_type.Contains("603 decl"))
            {
                return "603 Declined";
            }

            if (raw_type.Contains("480 temp"))
            {
                return "480 Temporarily unavailable";
            }

            if (raw_type.Contains("401 unaut"))
            {
                return "401 Unauthorized";
            }

            if (raw_type.Contains("407 prox"))
            {
                return "407 Proxy Authentication required";
            }

            if (raw_type.Contains("181 call"))
            {
                return "181 Call is being forwarded";
            }

            if (raw_type.Contains("182 queued"))
            {
                return "182 Queued";
            }

            if (raw_type.Contains("183 sess"))
            {
                return "183 Session Progress";
            }

            if (raw_type.Contains("199 ear"))
            {
                return "199 Early Dialog Terminated";
            }

            if (raw_type.Contains("202 acce"))
            {
                return "202 Accepted";
            }

            if (raw_type.Contains("204 no n"))
            {
                return "204 No Notification";
            }

            if (raw_type.Contains("487 requ"))
            {
                return "487 Request Terminated";
            }

            if (raw_type.Contains("487 requ"))
            {
                return "487 Request Cancelled";
            }

            if (raw_type.Contains("subsc"))
            {
                return "Subscribe";
            }

            if (raw_type.Contains("481 call"))
            {
                return "481 Call Leg/ Transaction Does Not Exist";
            }

            if (raw_type.Contains("486 busy"))
            {
                return "486 Busy Here";
            }

            if (raw_type.Contains("update"))
            {
                return "Update";
            }

            if (raw_type.Contains("refer"))
            {
                return "Refer";
            }

            if (raw_type.Contains("prack"))
            {
                return "PRACK";
            }

            if (raw_type.Contains("publish"))
            {
                return "Publish";
            }

            if (raw_type.Contains("message"))
            {
                return "Message";
            }

            if (raw_type.Contains("info"))
            {
                return "Info";
            }

            if (raw_type.Contains("options"))
            {
                return "Options";
            }

            return "Unknown SIP: " + raw_type;
        }

        // Stop feature
        public void RunWithStopFeature()
        {
            if (string.IsNullOrEmpty(FileName)) return;
            if (PacketCount == 0 || SelectedNetworkAdapter == null) return;

            PacketsSentBeforeStop = 0;
            PacketsNotSentAfterStop = 0;

            SIPConsoleText.Text = "Sending File:\n " + FileName + "\n";

            // Retrieve the length of the capture file
            long capLength = new FileInfo(FileName).Length;

            Common.ConsoleWriteLine(ConsoleText);
            Common.ConsoleWriteLine(ConsoleText, "Preparing file timestamps...");
            Common.WaitFor(50);

            // Open the capture file
            OfflinePacketDevice selectedInputDevice = new OfflinePacketDevice(FileName);

            using (PacketCommunicator inputCommunicator =
                selectedInputDevice.Open(65536, // portion of the packet to capture
                                                // 65536 guarantees that the whole packet will be captured on all the link layers
                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                         1000)) // read timeout
            {
                Application.DoEvents();

                if (StopRunningAllFiles)
                {
                    Common.ConsoleWriteLine(ConsoleText, "Preparing of Packets Interupted. Stopping...");
                    return;
                }

                // Open the output device
                using (PacketCommunicator outputCommunicator =
                    SelectedNetworkAdapter.Open(100, PacketDeviceOpenAttributes.Promiscuous, 1000))
                {

                    Application.DoEvents();

                    if (StopRunningAllFiles)
                    {
                        Common.ConsoleWriteLine(ConsoleText, "Preparing of Packets Interupted. Stopping...");
                        return;
                    }

                    // Fill the buffer with the packets from the file
                    AlteredPackets = new List<Packet>();
                    Packet packet;
                    int packet_count = 0;
                    DateTime StartTimeStamp = DateTime.Now;
                    Dictionary<string, int> FilteredPackets = new Dictionary<string, int>();
                    while (inputCommunicator.ReceivePacket(out packet) == PacketCommunicatorReceiveResult.Ok)
                    {
                        Application.DoEvents();

                        if (!IsSIP(packet) && !IsRTP(packet)) continue;

                        if (IsSIP(packet))
                        {
                            string type = GetSIPType(packet);

                            if (FilterPacket(type) != "No Filter")
                            {

                                if (!FilteredPackets.ContainsKey(type))
                                {
                                    FilteredPackets.Add(type, 1);
                                }
                                else
                                {
                                    FilteredPackets[type]++;
                                }

                                continue;
                            }
                        }

                        // Create the builder that will build our packets
                        EthernetLayer ethernet_layer = packet.Ethernet == null ? null : (EthernetLayer)packet.Ethernet.ExtractLayer();
                        IpV4Layer ipv4_layer = packet.Ethernet.IpV4 == null ? null : (IpV4Layer)packet.Ethernet.IpV4.ExtractLayer();
                        IcmpLayer icmp_layer = packet.Ethernet.IpV4.Icmp == null ? null : (IcmpLayer)packet.Ethernet.IpV4.Icmp.ExtractLayer();
                        TransportLayer transport_layer = packet.Ethernet.IpV4.Transport == null ? null : (TransportLayer)packet.Ethernet.IpV4.Transport.ExtractLayer();
                        PayloadLayer datagram_layer = packet.Ethernet.IpV4.Payload == null ? null : (PayloadLayer)packet.Ethernet.IpV4.Payload.ExtractLayer();

                        try
                        {
                            if (ipv4_layer.Length < 1) // Catch null Length
                            {
                                // Do Nothing
                            }
                        }
                        catch
                        {
                            ipv4_layer = null;
                        }

                        List<ILayer> layers = new List<ILayer>();

                        if (IsRTP(packet))
                        {
                            if (ethernet_layer != null) layers.Add(ethernet_layer);
                            if (ipv4_layer != null) layers.Add(ipv4_layer);
                            if (datagram_layer != null) layers.Add(datagram_layer);
                        }
                        else
                        {
                            if (ethernet_layer != null) layers.Add(ethernet_layer);
                            if (ipv4_layer != null) layers.Add(ipv4_layer);
                            if (icmp_layer != null) layers.Add(icmp_layer);

                            if (transport_layer != null) layers.Add(transport_layer);
                            if (datagram_layer != null && IsRTP(packet)) layers.Add(datagram_layer);
                        }

                        PacketBuilder builder = new PacketBuilder(layers);

                        Packet altered_packet = builder.Build(StartTimeStamp.AddMilliseconds(packet_count * StopFeatureTimeInterval));
                        ProcessAlteredPacket(altered_packet);

                        packet_count++;
                    }

                    Common.ConsoleWriteLine(ConsoleText, "Sending packets. Total to packets to send: " + AlteredPackets.Count);
                    Common.WaitFor(50);

                    if (StopRunningAllFiles)
                    {
                        Common.ConsoleWriteLine(ConsoleText, "Sending of Packets Interupted. Stopping...");
                        return;
                    }

                    int sip_sent = 0;
                    int rtp_sent = 0;
                    for (int i = 0; i < AlteredPackets.Count; i++)
                    {
                        Application.DoEvents();

                        if (StopRunningAllFiles)
                        {
                            PacketsNotSentAfterStop = AlteredPackets.Count - PacketsSentBeforeStop;
                            StopRunningAllFiles = false;
                            Common.ConsoleWriteLine(ConsoleText, "\nSending of Packets Interupted. Stopping...");
                            break;
                        }

                        if (i % 50 == 0)
                        {
                            Common.ConsoleWriteLine(ConsoleText, "   Current Packet: " + i.ToString());
                        }

                        outputCommunicator.SendPacket(AlteredPackets[i]);

                        if (IsSIP(AlteredPackets[i]))
                        {
                            sip_sent++;
                            bool is_vital_packet = false;
                            string sip_type = GetSIPType(AlteredPackets[i]);

                            if (
                                sip_type == "Invite" ||
                                sip_type == "Trying" ||
                                sip_type == "Ringing" ||
                                sip_type == "200 OK" ||
                                sip_type == "Cancel" ||
                                sip_type == "Bye")
                            {
                                is_vital_packet = true;
                            }

                                if (!string.IsNullOrEmpty(sip_type))
                            {
                                Common.ConsoleWriteLine(SIPConsoleText, (is_vital_packet ? " --> " : "") + "SIP: (" + sip_type + ") Packet Sent.");
                            }
                        }

                        if(IsRTP(AlteredPackets[i]))
                        {
                            rtp_sent++;
                        }

                        PacketsSentBeforeStop++;

                        // Wait before sending next packet
                        Common.WaitFor(StopFeatureTimeInterval);

                    }

                    Common.ConsoleWriteLine(ConsoleText);
                    Common.ConsoleWriteLine(ConsoleText, "File:\n" + FileName + "\n");
                    Common.ConsoleWriteLine(ConsoleText, "   Packets Sent: " + PacketsSentBeforeStop);
                    Common.ConsoleWriteLine(ConsoleText, "   SIP Packets Sent: " + sip_sent);
                    Common.ConsoleWriteLine(ConsoleText, "   RTP Packets Sent: " + rtp_sent);

                    Common.ConsoleWriteLine(ConsoleText, "   ----------------");
                    Common.ConsoleWriteLine(ConsoleText, "   Filtered Packets");
                        Common.ConsoleWriteLine(ConsoleText, "   ----------------");
                    foreach (string sip_type in FilteredPackets.Keys)
                    {
                        Common.ConsoleWriteLine(ConsoleText, "      " + FilteredPackets[sip_type].ToString() + " " + sip_type + " Packets Removed.");
                    }
                    Common.ConsoleWriteLine(ConsoleText, "   ----------------");

                    Common.ConsoleWriteLine(ConsoleText, "   Packets Not Sent: " + PacketsNotSentAfterStop);
                }
            }
        }

    }
}
