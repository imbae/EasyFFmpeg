using FFmpeg.AutoGen;
using System;

namespace EasyFFmpeg
{
    public unsafe class H264VideoStreamEncoder : IDisposable
    {
        private AVFormatContext* oFormatContext;
        private AVCodecContext* oCodecContext;
        private AVCodec* oCodec;
        
        public void OpenOutputURL(string fileName, VideoInfo videoInfo)
        {
            AVStream* out_stream;

            //output file
            var _oFormatContext = oFormatContext;

            ffmpeg.avformat_alloc_output_context2(&_oFormatContext, null, null, fileName);

            oCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

            out_stream = ffmpeg.avformat_new_stream(_oFormatContext, oCodec);

            oCodecContext = ffmpeg.avcodec_alloc_context3(oCodec);

            oCodecContext->height = videoInfo.SourceFrameSize.Height;
            oCodecContext->width = videoInfo.SourceFrameSize.Width;
            oCodecContext->sample_aspect_ratio = videoInfo.Sample_aspect_ratio;
            oCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            oCodecContext->time_base = new AVRational { num = 1, den = 15 };
            oCodecContext->framerate = ffmpeg.av_inv_q(videoInfo.Framerate);

            //ffmpeg.av_opt_set(oCodecContext->priv_data, "profile", "baseline", 0);

            if ((_oFormatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            {
                oCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            //open codecd
            ffmpeg.avcodec_open2(oCodecContext, oCodec, null).ThrowExceptionIfError();

            ffmpeg.avcodec_parameters_from_context(out_stream->codecpar, oCodecContext);
            out_stream->time_base = oCodecContext->time_base;

            //Show some Information
            ffmpeg.av_dump_format(_oFormatContext, 0, fileName, 1);

            if (ffmpeg.avio_open(&_oFormatContext->pb, fileName, ffmpeg.AVIO_FLAG_WRITE) < 0)
            {
                Console.WriteLine("Failed to open output file! \n");
            }

            //Write File Header
            ffmpeg.avformat_write_header(_oFormatContext, null).ThrowExceptionIfError();

            oFormatContext = _oFormatContext;
        }

       

        public void TryEncodeNextPacket(AVFrame uncompressed_frame)
        {
            var encoded_packet = ffmpeg.av_packet_alloc();
            ffmpeg.av_init_packet(encoded_packet);

            try
            {
                int error;

                do
                {
                    //Supply a raw video frame to the output condec context
                    ffmpeg.avcodec_send_frame(oCodecContext, &uncompressed_frame).ThrowExceptionIfError();

                    //read encodeded packet from output codec context
                    error = ffmpeg.avcodec_receive_packet(oCodecContext, encoded_packet);

                    int encodedStreamIndex = encoded_packet->stream_index;

                    //set packet pts & dts for timestamp
                    if (encoded_packet->pts != ffmpeg.AV_NOPTS_VALUE)
                        encoded_packet->pts = ffmpeg.av_rescale_q(encoded_packet->pts, oCodecContext->time_base, oFormatContext->streams[encodedStreamIndex]->time_base);
                    if (encoded_packet->dts != ffmpeg.AV_NOPTS_VALUE)
                        encoded_packet->dts = ffmpeg.av_rescale_q(encoded_packet->dts, oCodecContext->time_base, oFormatContext->streams[encodedStreamIndex]->time_base);

                    //write frame in video file
                    ffmpeg.av_write_frame(oFormatContext, encoded_packet);

                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN) || error == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF));
            }
            finally
            {
                ffmpeg.av_packet_unref(encoded_packet);
            }
        }

        public void FlushEncode()
        {
            ffmpeg.avcodec_send_frame(oCodecContext, null);
        }




        #region Dispose

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    var _oFormatContext = oFormatContext;

                    //Write file trailer
                    ffmpeg.av_write_trailer(_oFormatContext);
                    ffmpeg.avformat_close_input(&_oFormatContext);

                    //메모리 해제
                    ffmpeg.avcodec_close(oCodecContext);
                    ffmpeg.av_free(oCodecContext);
                    ffmpeg.av_free(oCodec);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // 이 코드를 변경하지 마세요. 'Dispose(bool disposing)' 메서드에 정리 코드를 입력합니다.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
