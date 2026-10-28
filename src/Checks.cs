using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
namespace WindowsAppSender {
 static class Checks {
  static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
  public static int Run(string path) {
   var results=new List<string>();
   try {
    Require(Compatibility.CanAttempt(19045)&&Compatibility.CanAttempt(19041)&&Compatibility.CanAttempt(22631)&&!Compatibility.CanAttempt(18363),"Windows 10 compatibility boundary");
    results.Add("PASS: Win10 builds 19041/19045 and Win11 may attempt API activation; older builds rejected.");
    using(var current=System.Diagnostics.Process.GetCurrentProcess())using(var identity=new ProcessIdentity(current.Id)) {
     Require(identity.IsRunning,"limited-query current process running");
     Require(identity.StartTicks==current.StartTime.ToUniversalTime().Ticks,"limited-query creation identity");
    }
    results.Add("PASS: limited-query process identity matches creation time; running state queried without Process.HasExited.");
    foreach(string size in new[]{"960x540","1280x720","1920x1080"})foreach(int fps in new[]{30,60})foreach(string protocol in new[]{"RTMP","SRT"})foreach(string adjust in new[]{"CROP","COMPRESS","LETTER-BOX"})foreach(string video in new[]{"H264 (CPU)","H264 (NVEnc)","H264 (AMD)"}) {
     var c=new Config{Target=new ApplicationTarget(1234,5678,9,"game.exe","Game"),Size=size,Fps=fps,Protocol=protocol,Adjustment=adjust,Video=video};var a=c.Command("srt://example:1935?x=a&y=b");
     int expected=size=="960x540"?(fps==30?3000:4000):size=="1280x720"?(fps==30?4500:6000):(fps==30?6000:8000);
     Require(c.Bitrate==expected,"bitrate");Require(a[a.IndexOf("-g")+1]==fps.ToString(),"GOP");Require(a[a.IndexOf("-bf")+1]=="0","B frames");Require(a[a.IndexOf("-f",a.IndexOf("-f")+1)+1]==c.Muxer,"muxer");
     Require(c.EffectiveAudio==(protocol=="RTMP"?"AAC":"Opus"),"audio");Require(Args.Parse(Args.Join(a)).SequenceEqual(a),"quoting");
     Require(c.Command("",true).Last()=="pipe:1","local test");
     var preview=c.Command("",true);Require(preview[preview.LastIndexOf("-f")+1]==(protocol=="RTMP"?"flv":"mpegts"),"preview container");
     Require(a[a.IndexOf("-bufsize")+1]==expected+"k","VBV size");
     Require(a[a.IndexOf("-thread_queue_size")+1]=="64","input queue");
    }
    results.Add("PASS: 108 SRT/RTMP protocol/size/FPS/adjustment/encoder combinations; bitrate, GOP, B frames, muxer, audio, quoting and local preview route.");
    foreach(string value in new[]{"", "a b", "a\"b", "trailing slash\\", "srt://host?x=a&y=b", "日本語 フォルダ\\"})Require(Args.Parse(Args.Join(new[]{value})).Single()==value,"quote edge case");
    results.Add("PASS: URL ampersands, spaces, quotes, trailing backslash and Japanese paths survive native argument quoting.");
    bool rejected=false;try{new Config{CustomEnabled=true,Custom="-i other.mp4"}.Encoding();}catch(ArgumentException){rejected=true;}Require(rejected,"custom input rejected");
    Require(new Config{Protocol="SRT",Audio="AAC"}.EffectiveAudio=="AAC","SRT AAC");
    foreach(string codec in new[]{"-c:a","-codec:a","-acodec","-c:a:0"}) {
     var c=new Config{Protocol="RTMP",CustomEnabled=true,Custom="-c:v libx264 "+codec+" libopus -ar 44100 -ac 1 -b:a 64k"};var options=c.Encoding();
     Require(options[options.LastIndexOf("-c:a")+1]=="aac","forced custom AAC");
     Require(options.Count(x=>x=="-c:a")==1&&options.Count(x=>x=="-ar")==1&&options.Count(x=>x=="-ac")==1&&options.Count(x=>x=="-b:a")==1,"duplicate audio options");
     Require(options[options.IndexOf("-c:v")+1]=="libx264","custom video preserved");
    }
    results.Add("PASS: CUSTOM input redirection rejected; SRT AAC selectable.");
    var whip=new Config{Protocol="WebRTC (WHIP)",CustomEnabled=true,Custom="-whip_flags dtls_active",Size="1920x1080",Fps=60};
    rejected=false;try{whip.Command("http://localhost:8889/input/whip");}catch(ArgumentException){rejected=true;}Require(rejected,"WHIP disabled in command generation");
    Require(!whip.IsOutputUrlValid("http://localhost:8889/input/whip"),"WHIP URLs rejected");
    whip.MigrateLegacySettings();Require(whip.Protocol=="SRT"&&!whip.CustomEnabled&&whip.Custom==""&&whip.Size=="1920x1080"&&whip.Fps==60,"legacy migration");
    Require(!new Config{Protocol="SRT"}.IsOutputUrlValid("http://localhost/whip"),"SRT scheme restriction");
    Require(!new Config{Protocol="RTMP"}.IsOutputUrlValid("http://localhost/whip"),"RTMP scheme restriction");
    results.Add("PASS: WHIP disabled; old WHIP settings migrate to SRT with CUSTOM off while retaining size/FPS; RTMP CUSTOM audio options are not duplicated.");
    Require(new Config().AudioFilter()=="aresample=async=1000:first_pts=0","default sync unchanged");
    Require(new Config{AudioSyncMs=-200}.AudioFilter().EndsWith("atrim=start=0.2,asetpts=PTS-STARTPTS"),"advance audio");
    Require(new Config{AudioSyncMs=200}.AudioFilter().EndsWith("adelay=200:all=1"),"delay audio");
    rejected=false;try{new Config{AudioSyncMs=2001}.AudioFilter();}catch(ArgumentException){rejected=true;}Require(rejected,"sync limit");
    results.Add("PASS: sync 0 leaves audio unchanged, -200ms advances audio, +200ms delays audio, range is bounded.");
    File.WriteAllLines(path,results);return 0;
   }catch(Exception e){results.Add("FAIL: "+e);File.WriteAllLines(path,results);return 1;}
  }
  public static int Encode(string directory) {
   Directory.CreateDirectory(directory);var report=new List<string>();
   string ff=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","ffmpeg.exe");
   foreach(string size in new[]{"960x540","1280x720","1920x1080"})foreach(int fps in new[]{30,60})foreach(string adjustment in new[]{"CROP","COMPRESS","LETTER-BOX"}) {
    var c=new Config{Size=size,Fps=fps,Adjustment=adjustment,Video="H264 (NVEnc)",Protocol="SRT"};string file=Path.GetFullPath(Path.Combine(directory,size+"-"+fps+"-"+adjustment+".ts"));
    var command=c.Command(file,true,true);command[command.Count-1]=file;int duration=command.IndexOf("-t");command[duration+1]="1";command.InsertRange(0,new[]{"-y"});
    using(var p=new Process{StartInfo=new ProcessStartInfo(ff,Args.Join(command)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true}}) {
     p.Start();var errors=p.StandardError.ReadToEndAsync();byte[] silence=new byte[192000];p.StandardInput.BaseStream.Write(silence,0,silence.Length);p.StandardInput.Close();
     if(!p.WaitForExit(20000)){p.Kill();throw new Exception("Encode timeout");}string log=errors.GetAwaiter().GetResult();Require(p.ExitCode==0,"Encode failed: "+log);
    }
    using(var p=new Process{StartInfo=new ProcessStartInfo(ff,Args.Join(new[]{"-hide_banner","-i",file,"-f","null","NUL"})){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true}}) {
     p.Start();string decode=p.StandardError.ReadToEnd();p.WaitForExit();Require(p.ExitCode==0,"Decode failed");Require(decode.Contains(size+" ")&&decode.Contains(fps+" fps"),"Dimensions/FPS mismatch");
    }
    report.Add("PASS: "+size+" / "+fps+" fps / "+adjustment+" / NVEnc / Opus / local encode + decode");
   }
   File.WriteAllLines(Path.Combine(directory,"ENCODE-RESULTS.txt"),report);return 0;
  }
 }
}
