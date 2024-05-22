using FFmpeg.AutoGen;
using System.Drawing;

namespace EasyFFmpeg
{
    public unsafe class VideoInfo
    {
        /// <summary>
        /// (AVCodecContext의 값 참조)
        /// </summary>
        public long BitRate { get; set; }
        public int GopSize { get; set; }

        /// <summary>
        /// 비디오 인코딩에서 B-프레임(Bi-directional frames)의 최대 개수를 설정하는 값.
        /// <para> 이전과 이후의 프레임을 참조하여 압축 효율을 높이는 프레임 타입으로, 인코딩 효율성을 높이고 파일 크기를 줄이는 데 도움 </para>
        /// <para> 인코딩 시 필수 요소는 아니지만, B-프레임을 사용할 경우 인코딩 효율성과 품질을 조정하는 중요한 요소 </para>
        /// <para> B-프레임을 많이 사용하면 디코딩 복잡도가 증가 </para>
        /// <para> 0: B-프레임을 사용하지 않음, 1 이상: 푀대 B-프레임 수 설정 </para>
        /// <para> (AVCodecContext의 값 참조) </para>
        /// </summary>
        public int MaxBFrames { get; set; }

        public Size FrameSize { get; set; }

        /// <summary>
        /// 비디오 스트림의 픽셀의 종횡비
        /// <para> 인코딩 시 필수적인 요소는 아니지만, 올바르게 설정하지 않으면 비디오가 왜곡될 수 있음 </para>
        /// <para> (AVStream 또는 AVCodecContext의 값 참조) </para>
        /// </summary>
        public AVRational SampleAspectRatio { get; set; }

        /// <summary>
        /// 비디오 파일의 시간 정보를 표현하는 기본 단위. 분수 형태로 나타내며, timebase = num / den 형태로 표현
        /// <para> den 값이 클수록 타임스탬프의 정밀도가 높아지며, 작은 단위로 시간 간격을 표현 </para>
        /// <para> (AVStream의 값 참조) </para>
        /// </summary>
        public AVRational Timebase { get; set; }

        public AVRational FrameRate { get; set; }
    }
}
