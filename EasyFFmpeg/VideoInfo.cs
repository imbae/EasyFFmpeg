using FFmpeg.AutoGen;
using System.Drawing;

namespace EasyFFmpeg
{
    public class VideoInfo
    {
        public string CodecName { get; set; }
        public Size SourceFrameSize { get; set; }
        public Size DestinationFrameSize { get; set; }
        public AVPixelFormat SourcePixelFormat { get; set; }
        public AVPixelFormat DestinationPixelFormat { get; set; }
        public AVRational Sample_aspect_ratio { get; set; }
        public AVRational Timebase { get; set; }
        public AVRational Framerate { get; set; }
    }
}
