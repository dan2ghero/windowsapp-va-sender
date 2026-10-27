using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace WindowsAppSender {
 public class Config {
  [ScriptIgnore] public ApplicationTarget Target {get;set;}
  public int AudioSyncMs {get;set;}=0;
  public string AudioFilter() {
   if(AudioSyncMs < -2000||AudioSyncMs > 2000)throw new ArgumentException(T("Audio sync must be between -2000 and +2000 ms."));
   string value="aresample=async=1000:first_pts=0";
   if(AudioSyncMs<0)value+=",atrim=start="+(-AudioSyncMs/1000.0).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+",asetpts=PTS-STARTPTS";
   else if(AudioSyncMs>0)value+=",adelay="+AudioSyncMs+":all=1";
   return value;
  }
  public string Language {get;set;}="ENG";
  public string T(string english,params object[] args) {return I18n.T(Language,english,args);}
  public string Protocol {get;set;}="SRT";
  public string Size {get;set;}="1280x720";
  public int Fps {get;set;}=30;
  public string Adjustment {get;set;}="LETTER-BOX";
  public string Video {get;set;}="H264 (NVEnc)";
  public string Audio {get;set;}="Opus";
  public bool CustomEnabled {get;set;}=false;
  public string Custom {get;set;}="";
  public int Bitrate { get { return Size=="960x540" ? (Fps==30?3000:4000) : Size=="1920x1080" ? (Fps==30?6000:8000) : (Fps==30?4500:6000); } }
  public string Muxer {get {EnsureSupported();return Protocol=="RTMP"?"flv":"mpegts";}}
  public string EffectiveAudio {get {return Protocol=="RTMP"?"AAC":Audio;}}
  public void MigrateLegacySettings() {
   if(Language!="ENG"&&Language!="JPN")Language="ENG";
   if(Protocol=="WebRTC (WHIP)"){Protocol="SRT";CustomEnabled=false;Custom="";}
  }
  void EnsureSupported() {
   if(Protocol!="SRT"&&Protocol!="RTMP")throw new ArgumentException(T("This version supports SRT / RTMP. WHIP is disabled."));
  }
  public bool IsOutputUrlValid(string value) {
   Uri uri;
   if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||string.IsNullOrWhiteSpace(uri.Host))return false;
   if(Protocol=="RTMP")return uri.Scheme=="rtmp"||uri.Scheme=="rtmps";
   return Protocol=="SRT"&&uri.Scheme=="srt";
  }
  public string Filter(bool synthetic=false) {
   string[] wh=Size.Split('x'); string w=wh[0],h=wh[1];
   if(!synthetic&&Target==null)throw new InvalidOperationException(T("Select an application before starting."));
   string source=synthetic?"testsrc2=size=2560x1080:rate="+Fps:"gfxcapture=hwnd="+Target.WindowHandle+":max_framerate="+Fps+",hwdownload,format=bgra";
   string adjust=Adjustment=="CROP" ? "scale="+w+":"+h+":force_original_aspect_ratio=increase,crop="+w+":"+h : Adjustment=="COMPRESS" ? "scale="+w+":"+h : "scale="+w+":"+h+":force_original_aspect_ratio=decrease,pad="+w+":"+h+":(ow-iw)/2:(oh-ih)/2:black";
   return source+","+adjust+",setsar=1,fps="+Fps+",format=yuv420p[v]";
  }
  public List<string> Encoding() {
   EnsureSupported();
   if(CustomEnabled) {
    var custom=Args.Parse(Custom);
    string[] blocked={"-i","-f","-map","-filter_complex","-filter_complex_script","-y","-n","-progress","-report","-passlogfile","-filter_script","-vf","-af","-filter:v","-filter:a","-t","-to","-shortest","-attach","-dump_attachment","-vstats_file"};
    if(custom.Count==0 || custom.Any(s=>blocked.Contains(s.Split('=')[0],StringComparer.OrdinalIgnoreCase))) throw new ArgumentException(T("CUSTOM accepts encoding options only. The app manages inputs, URL, filters, output format and duration."));
    // Each option takes one value. Positional output filenames/URLs are not accepted.
    for(int i=0;i<custom.Count;i+=2) if(!custom[i].StartsWith("-") || i+1>=custom.Count || custom[i+1].StartsWith("-")) throw new ArgumentException(T("Enter CUSTOM options as -option value pairs. Do not specify a URL or output file."));
    if(custom.Contains("-whip_flags")||custom.Contains("-authorization"))throw new ArgumentException(T("WHIP is disabled. Remove WHIP-only options from CUSTOM."));
    if(Protocol=="RTMP") {
     // Replace existing per-audio options instead of appending duplicate stream options.
     for(int i=custom.Count-2;i>=0;i-=2) {
      string key=custom[i].ToLowerInvariant();
      if(key=="-acodec"||key=="-ab"||key=="-c:a"||key.StartsWith("-c:a:")||key=="-codec:a"||key.StartsWith("-codec:a:")||key=="-b:a"||key.StartsWith("-b:a:")||key=="-ar"||key.StartsWith("-ar:")||key=="-ac"||key.StartsWith("-ac:")||new[]{"-application","-frame_duration","-fec","-packet_loss","-vbr","-mapping_family","-apply_phase_inv"}.Contains(key))custom.RemoveRange(i,2);
     }
     custom.AddRange(new[]{"-c:a","aac","-b:a","128k","-ar","48000","-ac","2"});
    }
    return custom;
   }
   var a=new List<string>();
   if(Video=="H264 (CPU)") a.AddRange(new[]{"-c:v","libx264","-preset","veryfast","-tune","zerolatency","-x264-params","nal-hrd=cbr:force-cfr=1:scenecut=0"});
   else if(Video=="H264 (AMD)") a.AddRange(new[]{"-c:v","h264_amf","-usage","ultralowlatency","-quality","speed","-rc","cbr","-preanalysis","0","-vbaq","0"});
   else {
    a.AddRange(new[]{"-c:v","h264_nvenc","-preset","p3","-tune","ll","-zerolatency","1","-rc","cbr","-no-scenecut","1"});
   }
   a.AddRange(new[]{"-b:v",Bitrate+"k","-minrate",Bitrate+"k","-maxrate",Bitrate+"k","-bufsize",Bitrate+"k","-r",Fps.ToString(),"-g",Fps.ToString(),"-bf","0","-pix_fmt","yuv420p","-c:a",EffectiveAudio=="AAC"?"aac":"libopus","-b:a","128k","-ar","48000","-ac","2"});
   if(EffectiveAudio=="Opus") a.AddRange(new[]{"-application","lowdelay","-frame_duration","20"});
   return a;
  }
  public List<string> Command(string url, bool test=false, bool synthetic=false) {
   EnsureSupported();
   var a=new List<string>{"-hide_banner","-loglevel","warning","-nostats","-thread_queue_size","64","-probesize","32","-analyzeduration","0","-f","s16le","-ar","48000","-ac","2","-i","pipe:0","-filter_complex",Filter(synthetic),"-map","[v]","-map","0:a:0","-af",AudioFilter()};
   a.AddRange(Encoding()); if(test) a.AddRange(new[]{"-t","20"});
   a.AddRange(new[]{"-shortest","-f",test?(Protocol=="RTMP"?"flv":"mpegts"):Muxer,test?"pipe:1":url}); return a;
  }
 }
 public static class Args {
  public static string Quote(string value) {
   if(value.Length>0 && !value.Any(c=>char.IsWhiteSpace(c)||c=='"')) return value;
   var s=new StringBuilder("\""); int slash=0;
   foreach(char c in value) {if(c=='\\') {slash++;continue;} if(c=='"') {s.Append('\\',slash*2+1);s.Append(c);} else {s.Append('\\',slash);s.Append(c);} slash=0;}
   s.Append('\\',slash*2);return s.Append('"').ToString();
  }
  public static string Join(IEnumerable<string> args) {return string.Join(" ",args.Select(Quote));}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CommandLineToArgvW(string command,out int count);
  [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr ptr);
  public static List<string> Parse(string command) {
   int count; IntPtr p=CommandLineToArgvW("app.exe "+command,out count);if(p==IntPtr.Zero) throw new ArgumentException("Unable to parse CUSTOM options.");
   try {var list=new List<string>();for(int i=1;i<count;i++) list.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(p,i*IntPtr.Size)));return list;} finally {LocalFree(p);}
  }
 }
}
