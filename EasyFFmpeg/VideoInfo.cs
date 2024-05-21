using FFmpeg.AutoGen;
using System.Drawing;

namespace EasyFFmpeg
{
    public unsafe class VideoInfo
    {
        public long BitRate { get; set; }
        public int GopSize { get; set; }
        public int MaxBFrames { get; set; }
        public Size FrameSize { get; set; }
        public AVRational Sample_aspect_ratio { get; set; }
        public AVRational Timebase { get; set; }
        public AVRational FrameRate { get; set; }
    }
}
