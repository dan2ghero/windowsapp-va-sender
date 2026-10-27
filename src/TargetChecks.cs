using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsAppSender {
 static class TargetChecks {
  static void Require(bool value,string message){if(!value)throw new Exception(message);}
  static IEnumerable<Control> Controls(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control nested in Controls(child))yield return nested;}
  public static void Fixture(string metadata,int frequency) {
   var wave=new MemoryStream();using(var w=new BinaryWriter(wave,System.Text.Encoding.ASCII,true)) {
    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+48000*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(48000);w.Write(96000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(96000);
    for(int i=0;i<48000;i++)w.Write((short)(1000*Math.Sin(2*Math.PI*frequency*i/48000)));
   }
   wave.Position=0;
   using(var player=new SoundPlayer(wave))using(var form=new Form {Text="Games sender test "+frequency,BackColor=frequency==440?Color.Red:Color.Blue,ClientSize=new Size(384,216),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-5000,-5000)}) {
    form.Shown+=(s,e)=>{using(var p=Process.GetCurrentProcess())File.WriteAllText(metadata,new JavaScriptSerializer().Serialize(new {WindowHandle=unchecked((ulong)form.Handle.ToInt64()),ProcessId=p.Id,ProcessStartTicks=p.StartTime.ToUniversalTime().Ticks,Executable=p.ProcessName+".exe",Title=form.Text}));player.PlayLooping();};
    Application.Run(form);player.Stop();
   }
  }
  static double Amplitude(byte[] data,int frequency) {
   int start=48000,count=Math.Min(48000,data.Length/4-start);Require(count>10000,"not enough PCM");double re=0,im=0;
   for(int n=0;n<count;n++){int i=start+n;double v=BitConverter.ToInt16(data,i*4),phase=2*Math.PI*frequency*i/48000;re+=v*Math.Cos(phase);im+=v*Math.Sin(phase);}return 2*Math.Sqrt(re*re+im*im)/count;
  }
  public static int Run(string directory) {
   Directory.CreateDirectory(directory);directory=Path.GetFullPath(directory);var report=new List<string>();var children=new List<Process>();
   try {
    string exe=Application.ExecutablePath;
    foreach(int frequency in new[]{440,880}) {
     string metadata=Path.Combine(directory,frequency+".json");if(File.Exists(metadata))File.Delete(metadata);
     var p=Process.Start(new ProcessStartInfo(exe,Args.Join(new[]{"--fixture",metadata,frequency.ToString()})){UseShellExecute=false,CreateNoWindow=true});children.Add(p);
     var timeout=Stopwatch.StartNew();while(!File.Exists(metadata)&&timeout.ElapsedMilliseconds<7000)Thread.Sleep(40);Require(File.Exists(metadata),"fixture startup");
    }
    Thread.Sleep(300);var json=new JavaScriptSerializer();var a=json.Deserialize<ApplicationTargetData>(File.ReadAllText(Path.Combine(directory,"440.json")));var b=json.Deserialize<ApplicationTargetData>(File.ReadAllText(Path.Combine(directory,"880.json")));
    var list=ApplicationTarget.List();var selected=list.Single(t=>t.ProcessId==a.ProcessId&&t.WindowHandle==a.WindowHandle);Require(list.Any(t=>t.ProcessId==b.ProcessId),"second same-executable instance enumerated");selected.Validate("ENG");
    var c=new Config{Target=selected};Require(c.Filter().Contains("hwnd="+a.WindowHandle+":"),"selected HWND");Require(!c.Filter().Contains("window_exe="),"no name-based ambiguity");
    bool rejected=false;try{new ApplicationTarget(a.WindowHandle,b.ProcessId,b.ProcessStartTicks,"other.exe","Other").Validate("ENG");}catch(InvalidOperationException){rejected=true;}Require(rejected,"window/PID mismatch rejected");
    var settings=json.Serialize(c);Require(!settings.Contains("WindowHandle")&&!settings.Contains("ProcessId")&&!settings.Contains("Target"),"runtime target not persisted");
    report.Add("PASS: application enumeration, exact HWND/PID selection across two identical executable names, mismatch rejection, and no persisted HWND/PID.");
    using(var main=new MainForm()) {
     main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-10000,-10000);main.Show();Application.DoEvents();
     var combo=Controls(main).OfType<ComboBox>().Single();combo.SelectedItem=combo.Items.OfType<ApplicationTarget>().Single(t=>t.ProcessId==a.ProcessId);
     var field=typeof(MainForm).GetField("config",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
     Require(((Config)field.GetValue(main)).Target.ProcessId==a.ProcessId,"UI assigns selected audio PID");
     Require(Controls(main).OfType<TextBox>().Any(box=>box.Text.Contains("hwnd="+a.WindowHandle+":")),"UI INFO uses selected video HWND");
     Controls(main).OfType<Button>().Single(button=>button.Text=="REFRESH").PerformClick();Require(((ApplicationTarget)combo.SelectedItem).ProcessId==a.ProcessId,"refresh preserves selected instance");
     using(var bitmap=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"main-selected.png"));}
     combo.SelectedItem=combo.Items.OfType<ApplicationTarget>().Single(t=>t.ProcessId==b.ProcessId);Require(((Config)field.GetValue(main)).Target.ProcessId==b.ProcessId,"UI switches audio and video together");
    }
    report.Add("PASS: main APPLICATION selection updates video HWND and audio PID together; REFRESH preserves the live selection.");
    string capture=Path.Combine(directory,"selected.pcm");if(File.Exists(capture))File.Delete(capture);
    using(var p=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","AppAudio.exe"),Args.Join(new[]{"--pid",selected.ProcessId.ToString(),"--seconds","3","--output",capture})){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true})) {
     var log=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(10000)){p.Kill();throw new Exception("audio capture timeout");}Require(p.ExitCode==0,"capture helper failed: "+log.Result);
     File.WriteAllText(Path.Combine(directory,"audio-capture.txt"),log.Result);
    }
    byte[] data=File.ReadAllBytes(capture);double wanted=Amplitude(data,440),other=Amplitude(data,880);Require(wanted>0.5&&other<wanted*0.1,"process audio separation: "+wanted+" / "+other);
    report.Add("PASS: audio from selected PID only; 440 Hz="+wanted.ToString("F2")+", other-process 880 Hz="+other.ToString("F2"));
    children[0].Kill();children[0].WaitForExit();rejected=false;try{selected.Validate("ENG");}catch(InvalidOperationException){rejected=true;}Require(rejected,"closed target rejected");
    Require(!ApplicationTarget.List().Any(t=>t.ProcessId==a.ProcessId),"refresh removes closed target");report.Add("PASS: closed target is rejected and removed on refresh.");
    File.WriteAllLines(Path.Combine(directory,"TARGET-RESULTS.txt"),report);return 0;
   }catch(Exception e){report.Add("FAIL: "+e);File.WriteAllLines(Path.Combine(directory,"TARGET-RESULTS.txt"),report);return 1;}
   finally{foreach(var p in children){try{if(!p.HasExited)p.Kill();p.Dispose();}catch{}}}
  }
  public class ApplicationTargetData {public ulong WindowHandle{get;set;}public int ProcessId{get;set;}public long ProcessStartTicks{get;set;}}
 }
}
