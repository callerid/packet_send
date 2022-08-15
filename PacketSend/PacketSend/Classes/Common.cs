using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PacketSend.Classes
{
    using System.Diagnostics;

    public static class TimeUtils
    {
        public static long GetNanoseconds()
        {
            double timestamp = Stopwatch.GetTimestamp();
            double nanoseconds = 1_000_000_000.0 * timestamp / Stopwatch.Frequency;

            return (long)nanoseconds;
        }
    }

    class Common
    {
        public static void WaitForNanoSeconds(long ns)
        {
            long current = TimeUtils.GetNanoseconds();

            while (TimeUtils.GetNanoseconds() - current < ns)
            {
                Application.DoEvents();
            }
            
        }

        public static void WaitFor(int milliSeconds)
        {
            var sw = new Stopwatch();
            sw.Start();
            while (sw.ElapsedMilliseconds < milliSeconds)
            {
                Application.DoEvents();
            }
            sw.Stop();
        }

        public static void ConsoleWriteLine(RichTextBox rtbConsole, string text = "", bool newline = true)
        {
            rtbConsole.Text += text + (newline ? Environment.NewLine : "");
            rtbConsole.SelectionStart = rtbConsole.Text.Length;
            rtbConsole.ScrollToCaret();
        }

        public static string ConvertNanoSecondsToReadableTime(long nanoseconds)
        {
            float secs = (nanoseconds / 1000000000.0f);
            TimeSpan t = TimeSpan.FromSeconds(secs);

            return string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                            t.Hours,
                            t.Minutes,
                            t.Seconds,
                            t.Milliseconds);
        }

        public static string ConvertSecondsToReadableTime(float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);

            return string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                            t.Hours,
                            t.Minutes,
                            t.Seconds,
                            t.Milliseconds);
        }
    }
}
