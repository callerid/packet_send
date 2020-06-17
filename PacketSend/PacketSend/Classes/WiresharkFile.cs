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
        public int RunningSpeedPacketCount = 0;
        private List<Packet> AlteredPackets = new List<Packet>();
        public RunSpeeds RunSpeed = RunSpeeds.Original;
        public int RTP_Packets = 0;
        public int SIP_Packets = 0;

        public WiresharkFile(string filepath)
        {
            FileName = filepath;
            PacketCount = 0;

            // Create the offline device
            OfflineDevice = new OfflinePacketDevice(filepath);

            // Open the capture file
            using (PacketCommunicator communicator =
                OfflineDevice.Open(65536,                                   // portion of the packet to capture
                                                                            // 65536 guarantees that the whole packet will be captured on all the link layers
                                    PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                    1000))                                  // read timeout
            {
                // Read and dispatch packets until EOF is reached
                communicator.ReceivePackets(0, ProcessNewFilePackets);
            }

            Common.ConsoleWriteLine(ConsoleText);
            Common.ConsoleWriteLine(ConsoleText, "File Loaded: " + FileName.Substring(FileName.LastIndexOf("\\") + 1) + "\n   - SIP Packets = " + SIP_Packets + "\n   - RTP Packets = " + RTP_Packets);
        }

        public void RunFileWithSpeed(RunSpeeds run_speed)
        {
            if (string.IsNullOrEmpty(FileName)) return;
            if (run_speed == RunSpeeds.Original) return;
            if (PacketCount == 0 || SelectedNetworkAdapter == null) return;

            // Retrieve the length of the capture file
            long capLength = new FileInfo(FileName).Length;

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

                    // Allocate a send buffer
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

        private void ProcessNewFilePackets(Packet packet)
        {
            PacketCount++;

            if (IsSIP(packet)) SIP_Packets++;
            if (IsRTP(packet)) RTP_Packets++;
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

    }
}
