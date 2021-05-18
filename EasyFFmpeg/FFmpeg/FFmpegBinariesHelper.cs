using FFmpeg.AutoGen;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EasyFFmpeg
{
    public class FFmpegBinariesHelper
    {
        internal static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("Plugins", "FFmpeg", Environment.Is64BitProcess ? "64bit" : "32bit");
                while (current != null)
                {
                    var ffmpegBinaryPath = Path.Combine(current, probe);
                    if (Directory.Exists(ffmpegBinaryPath))
                    {
                        Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                        ffmpeg.RootPath = ffmpegBinaryPath;
                        return;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ffmpeg.RootPath = "/lib/x86_64-linux-gnu/";
            }
            else
            {
                throw new NotSupportedException(); // fell free add support for platform of you choose
            }
        }
    }
}
