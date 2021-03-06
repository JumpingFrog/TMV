﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using AForge;
using AForge.Video;
using AForge.Video.FFMPEG;



namespace TMV_Encoder__AForge_
{
    public partial class Main : Form
    {
        public FileStream sfile;
        public int result;
        public OpenFileDialog oform;
        public static string apath = AppDomain.CurrentDomain.BaseDirectory;
        private bool closing = false;

        public Main() {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(Main_Closing);
            logbox.Text = "TMV Encoder v0.2a started";
        }
        
        void Main_Closing(object sender, FormClosingEventArgs e)
        {
            closing = true;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            oform = new System.Windows.Forms.OpenFileDialog();
            oform.Filter = "AVI Files|*.avi";
            oform.CheckFileExists = true;

            if (oform.ShowDialog() == DialogResult.OK) {
                if (sfile != null) {
                    sfile.Dispose();
                }
                logbox.Text += Environment.NewLine + "Selected file: " + oform.FileName + " press encode to begin." + Environment.NewLine;
            }
        }

        private void btn_encode_Click(object sender, EventArgs e) {
            if (oform != null && File.Exists(oform.FileName)) { //has a filestream been opened?
                hScrollBar1.Enabled = false;
                checkBox1.Enabled = false;
                btn_encode.Enabled = false;
                // create instance of video reader
                VideoFileReader reader = new VideoFileReader();
                VideoFileWriter writer = new VideoFileWriter();
                reader.Open(oform.FileName);
                if (checkBox1.Checked) { //Is the user requesting a AVI?
                    writer.Open(apath + "output.wmv", 320, 200, reader.FrameRate, VideoCodec.WMV2);
                }
                // print some of its attributes
                logbox.Text += "Width: " + reader.Width + "px" + Environment.NewLine;
                logbox.Text += ("Height: " + reader.Height + "px" + Environment.NewLine);
                logbox.Text += ("Fps: " + reader.FrameRate + "fps"+ Environment.NewLine);
                logbox.Text += ("Codec: " + reader.CodecName + Environment.NewLine);
                logbox.Text += ("Frames: " + reader.FrameCount + Environment.NewLine);
                //start encoding classes
                TMVVideo tvid = new TMVVideo();
                TMVEncoder tmv = new TMVEncoder();
                //tmvframe.Threshold = hScrollBar1.Value;
                Bitmap videoFrame = new Bitmap(320,200);
                logbox.Text += "Conversion started @ " + DateTime.Now.ToString();
                string logtxt = logbox.Text;
                logbox.Text += "Current Frame: 0";
                TMVFont renderfont = new TMVFont(apath + "font.bin");
                TMVFrame tframe;
                for (int i = 0; i < reader.FrameCount; i++) {
                    videoFrame = resize_image(reader.ReadVideoFrame());
                    tframe = tmv.encode(videoFrame);
                    tvid.addFrame(tframe);
                    obox.Image = tframe.renderFrame(renderfont);
                    pbar.Value = (int)((i * 100) / (reader.FrameCount-1));
                    logbox.Text = logtxt + Environment.NewLine + "Current Frame: " + i + "/" + (reader.FrameCount-1);
                    if (checkBox1.Checked) { //Is the user requesting a AVI?
                        writer.WriteVideoFrame((Bitmap)obox.Image);
                    }
                    if (closing)
                    {
                        return;
                    }
                    fbox.Image = videoFrame;
                    Application.DoEvents();
                }
                logbox.Text += Environment.NewLine + "All frames converted, attempting to interleave audio.";
                if (File.Exists(apath + "temp.wav")) { //remove any previous streams
                    File.Delete(apath + "temp.wav");
                }
                AviManager aviManager = new AviManager(oform.FileName, true);
                try { //try to read the stream
                    AudioStream waveStream = aviManager.GetWaveStream();
                    logbox.Text += Environment.NewLine + "Audio stream found:";
                    logbox.Text += Environment.NewLine + "Sample Rate: " + waveStream.CountSamplesPerSecond.ToString();
                    logbox.Text += Environment.NewLine + "Bits:" + waveStream.CountBitsPerSample.ToString();
                    logbox.Text += Environment.NewLine + "Number of Channels: " + waveStream.CountChannels.ToString();
                    File.Delete(apath + "temp.wav");
                    waveStream.ExportStream(apath+"temp.wav");
                    waveStream.Close();
                    aviManager.Close();

                    byte[] audio_data = readWav(apath + "temp.wav");

                    if (reader.FrameRate > 99) { //sometimes frame rate is stored fixed point CRUDE
                        tvid.setFPS((decimal)(reader.FrameRate / 10.0));
                        tvid.loadAudio(audio_data);
                        tvid.save();
                    }
                    else {
                        tvid.setFPS(reader.FrameRate);
                        tvid.loadAudio(audio_data);
                        tvid.save();
                    }
                }
                catch { //error somewhere here, continue silent.
                    logbox.Text += Environment.NewLine+"Error, source video does not have WAV audio, video will be silent.";
                    
                    if (reader.FrameRate > 99) { //sometimes frame rate is stored fixed point CRUDE
                        tvid.setFPS((decimal)(reader.FrameRate / 10.0));
                        tvid.loadAudio(new Byte[reader.FrameCount]);
                        tvid.save();
                    }
                    else {
                        tvid.setFPS(reader.FrameRate);
                        tvid.loadAudio(new Byte[reader.FrameCount]);
                        tvid.save();
                    }
                }

                logbox.Text += Environment.NewLine + "Conversion finished @ " + DateTime.Now.ToString();
                writer.Close();
                reader.Close();
                hScrollBar1.Enabled = true;
                checkBox1.Enabled = true;
                btn_encode.Enabled = true;

            }
            else {
                logbox.Text += Environment.NewLine + "Error: Select a file (Using File -> Open) before attempting to encode.";
            }
        }

        private Bitmap resize_image(Bitmap frame) {
            Bitmap output = new Bitmap(320, 200);
            Graphics g = Graphics.FromImage((Image)output);
            g.DrawImage((Image)frame, 0, 0, 320, 200);
            frame.Dispose();
            return output;
        }

        private byte[] readWav(string fpath) { //Generates data by parsing WAVE https://ccrma.stanford.edu/courses/422/projects/WaveFormat/
            FileStream fs = new FileStream(fpath, FileMode.Open, FileAccess.Read, FileShare.Read);
            BinaryReader wavr = new BinaryReader(fs);
            string ChunkID = "";
            for (int i = 0; i < 4; i++) {
                ChunkID += wavr.ReadChar();
            }
            if (ChunkID == "RIFF") {
                UInt32 ChunkSize = wavr.ReadUInt32();
                string Format = "";
                for (int i = 0; i < 4; i++) {
                    Format += wavr.ReadChar();
                }
                if (Format == "WAVE") {
                    //Onto Sub Chunk 1
                    string Subchunk1ID = "";
                    for (int i = 0; i < 4; i++) {
                        Subchunk1ID += wavr.ReadChar();
                    }
                    Console.WriteLine(Subchunk1ID);
                    UInt32 Subchunk1Size = wavr.ReadUInt32();
                    UInt16 AudioFormat = wavr.ReadUInt16(); //MUST be 1 for PCM
                    if (AudioFormat != 1) {
                        logbox.Text += Environment.NewLine + "Error: Audio stream is not PCM encoded!";
                        return null;
                    }
                    UInt16 NumChannels = wavr.ReadUInt16();
                    if (NumChannels == 0) {
                        throw new NoNullAllowedException();
                    }
                    UInt32 SampleRate = wavr.ReadUInt32();
                    UInt32 ByteRate = wavr.ReadUInt32();
                    UInt16 BlockAlign = wavr.ReadUInt16();
                    UInt16 BitsPerSample = wavr.ReadUInt16();
                    if (BitsPerSample == 0) {
                        throw new NoNullAllowedException();
                    }
                    for (int i = 16; i < Subchunk1Size; i++) { //read excess bytes :D 
                        wavr.ReadByte();
                    }
                    //End of SubChunk1
                    string Subchunk2ID = "";
                    for (int i = 0; i < 4; i++) {
                        Subchunk2ID += wavr.ReadChar();
                    }
                    Console.WriteLine(BitsPerSample);
                    Console.WriteLine(NumChannels);
                    UInt32 Subchunk2Size = wavr.ReadUInt32();
                    if (Subchunk2Size == 0) {
                        throw new NoNullAllowedException();
                    }
                    byte[] result = new byte[Subchunk2Size / (NumChannels)];
                    int p = 0;
                    for (int i = 0; i < Subchunk2Size; i += NumChannels) { //WARNING. Assuming 1 byte.
                        int temp = 0;
                        for (int c = 0; c < NumChannels; c++) {
                            temp += wavr.ReadByte();
                        }
                        result[p] = (byte)(temp / NumChannels); //Stereo to Mono
                        p++;
                    }
                    return result;
                }
                else {
                    return null;
                }
            }
            else {
                return null;
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
            about abox = new about();
            abox.Show();
        }

        private void hScrollBar1_Scroll(object sender, ScrollEventArgs e) {
            if (hScrollBar1.Value == 60) {
                scrlVal.ForeColor = Color.Red;
                scrlVal.Text = hScrollBar1.Value.ToString();
            }
            else {
                scrlVal.ForeColor = Color.Black;
                scrlVal.Text = hScrollBar1.Value.ToString();
            }
        }

    }
}
