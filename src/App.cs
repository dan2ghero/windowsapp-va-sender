using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsAppSender {
 static class Theme {
  public static string Version {get{return "v"+typeof(Theme).Assembly.GetName().Version.ToString(3);}}
  public static Control Footer(string text) {
   var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
   panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));
   panel.Controls.Add(Label(text,8,true),0,0);var version=Label(Version,8,true);version.Anchor=AnchorStyles.Top|AnchorStyles.Right;panel.Controls.Add(version,1,0);return panel;
  }
  public static Color Bg=Color.FromArgb(18,21,28), Panel=Color.FromArgb(28,33,43), Text=Color.FromArgb(233,237,245), Muted=Color.FromArgb(151,162,183), Accent=Color.FromArgb(116,151,245);
  public static Label Label(string text,int size=10,bool muted=false) {return new Label {Text=text,ForeColor=muted?Muted:Text,AutoSize=true,Font=new Font("Segoe UI",size,FontStyle.Regular),Margin=new Padding(0,2,0,2)};}
  public static Button Button(string text,bool accent=false) {var b=new Button {Text=text,Height=44,Width=120,FlatStyle=FlatStyle.Flat,BackColor=accent?Accent:Panel,ForeColor=accent?Bg:Text,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(0,0,12,0)};b.FlatAppearance.BorderSize=0;return b;}
  public static TextBox Box(bool multi=false) {return new TextBox {Multiline=multi,BackColor=Panel,ForeColor=Text,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Consolas",10),Dock=DockStyle.Fill,ScrollBars=multi?ScrollBars.Vertical:ScrollBars.None};}
  public static void Form(Form f,string title,Size size) {f.Text=title;f.ClientSize=size;f.BackColor=Bg;f.ForeColor=Text;f.Font=new Font("Segoe UI",10);f.StartPosition=FormStartPosition.CenterScreen;f.AutoScaleMode=AutoScaleMode.Dpi;f.MinimumSize=new Size(size.Width,size.Height);}
 }
 sealed class MainForm:Form {
  Config config; readonly TextBox url=Theme.Box(),info=Theme.Box(true),logs=Theme.Box(true);
  readonly Button start=Theme.Button("START",true),test=Theme.Button("TEST · 20s"),settings=Theme.Button("SETTING");
  readonly Label status=Theme.Label("READY",9,true),hint=Theme.Label("",9,true);
  readonly ComboBox application=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat,DropDownWidth=900};
  readonly Button refresh=Theme.Button("REFRESH");
  readonly Label applicationHint=Theme.Label("Select an application window. Its process audio is captured automatically.",9,true);
  Session session; bool busy; readonly CancellationTokenSource setupCancellation=new CancellationTokenSource();
  string configFile {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");}}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,string l);
  public MainForm(Config initial=null) {
   Theme.Form(this,"windowsapp-va-sender",new Size(800,720));
   config=initial??LoadSettings();
   var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(32,24,32,24),ColumnCount=1,RowCount=16};
   int[] rows={52,28,24,34,22,24,34,28,24,128,14,46,26,24,80,24};
   foreach(int h in rows)root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   root.Controls.Add(Theme.Label("windowsapp-va-sender",23),0,0);
   root.Controls.Add(Theme.Label("APPLICATION VIDEO + PROCESS AUDIO",9,true),0,1);
   root.Controls.Add(Theme.Label("APPLICATION",10,true),0,2);
   var targetRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,116));
   refresh.Dock=DockStyle.Fill;refresh.Height=30;refresh.Margin=new Padding(12,0,0,0);targetRow.Controls.Add(application,0,0);targetRow.Controls.Add(refresh,1,0);root.Controls.Add(targetRow,0,3);root.Controls.Add(applicationHint,0,4);
   root.Controls.Add(Theme.Label("OUTPUT",10,true),0,5);root.Controls.Add(url,0,6);root.Controls.Add(hint,0,7);
   root.Controls.Add(Theme.Label("INFO",10,true),0,8);info.ReadOnly=true;root.Controls.Add(info,0,9);
   var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};buttons.Controls.AddRange(new Control[]{settings,test,start});root.Controls.Add(buttons,0,11);
   root.Controls.Add(status,0,12);root.Controls.Add(Theme.Label("SESSION LOG",9,true),0,13);logs.ReadOnly=true;root.Controls.Add(logs,0,14);
   root.Controls.Add(Theme.Footer("Windows 11 · selected application / process tree audio"),0,15);
   Controls.Add(root);
   settings.Click+=(s,e)=>{using(var dialog=new SettingsForm(config)) if(dialog.ShowDialog(this)==DialogResult.OK) {config=dialog.Result;config.Target=application.SelectedItem as ApplicationTarget;Save();ApplyLanguage();}};
   refresh.Click+=(s,e)=>RefreshApplications();
   application.SelectedIndexChanged+=(s,e)=>{config.Target=application.SelectedItem as ApplicationTarget;RefreshInfo();};
   url.TextChanged+=(s,e)=>RefreshInfo();
   start.Click+=async(s,e)=>{if(busy) {session?.Stop();status.Text=config.T("STOPPING…");}else await Run(false);};
   test.Click+=async(s,e)=>await Run(true);
   FormClosing+=(s,e)=>{setupCancellation.Cancel();session?.Stop();};
   Shown+=async(s,e)=>{RefreshApplications();RefreshInfo();if(!Bootstrap.Ready) {try{await PrepareTools();}catch(Exception ex){Log(ex.Message+" "+config.T("Retry initial setup with TEST / START."));}}}; ApplyLanguage();
  }
  Config LoadSettings() {try {var c=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(configFile)); c.MigrateLegacySettings();Validate(c);return c;}catch{return new Config();}}
  static void Validate(Config c) {
   if(!new[]{"SRT","RTMP"}.Contains(c.Protocol)||!new[]{"ENG","JPN"}.Contains(c.Language)||!new[]{"960x540","1280x720","1920x1080"}.Contains(c.Size)||!new[]{30,60}.Contains(c.Fps)||!new[]{"CROP","COMPRESS","LETTER-BOX"}.Contains(c.Adjustment)||!new[]{"H264 (CPU)","H264 (NVEnc)","H264 (AMD)"}.Contains(c.Video)||!new[]{"AAC","Opus"}.Contains(c.Audio))throw new ArgumentException(c.T("Invalid settings."));c.Encoding();c.AudioFilter();
  }
  void Save() {try {File.WriteAllText(configFile,new JavaScriptSerializer().Serialize(config));}catch(Exception e){Log(config.T("Could not save settings: {0}",e.Message));}}
  void ApplyLanguage() {I18n.Apply(this,config.Language);if(!busy)status.Text=config.T("READY");RefreshInfo();}
  void RefreshApplications() {
   var old=application.SelectedItem as ApplicationTarget;
   application.BeginUpdate();try {application.Items.Clear();foreach(var item in ApplicationTarget.List())application.Items.Add(item);
    if(old!=null)foreach(ApplicationTarget item in application.Items)if(item.SameInstance(old)){application.SelectedItem=item;break;}
   } finally {application.EndUpdate();}
   config.Target=application.SelectedItem as ApplicationTarget;RefreshInfo();
  }
  public void RefreshInfo() {
   hint.Text=config.Protocol=="RTMP"?"rtmp://server/application/stream-key":"srt://server:port?streamid=…";
   if(url.IsHandleCreated)SendMessage(url.Handle,0x1501,IntPtr.Zero,hint.Text);
   if(config.Target==null){info.Text=config.T("Select an application to preview the FFmpeg parameters.");return;}
   try {info.Text=(config.CustomEnabled?config.T("CUSTOM")+"\r\n":""+config.Size+" / "+config.Fps+" fps / CBR "+(config.Bitrate/1000.0).ToString("0.#")+" Mbps / GOP 1s\r\n")+"ffmpeg "+Args.Join(config.Command("<OUTPUT URL>"));}catch(Exception e){info.Text=e.Message;}
  }
  void Log(string line) {if(IsDisposed||Disposing)return; if(InvokeRequired){try{BeginInvoke(new Action<string>(Log),line);}catch{}return;} if(logs.TextLength>24000)logs.Clear();logs.AppendText(line+Environment.NewLine);}
  async Task PrepareTools() {
   start.Enabled=false;test.Enabled=false;settings.Enabled=false;status.Text=config.T("PREPARING FFMPEG…");
   try {await Bootstrap.Ensure(s=>{if(!IsDisposed&&!Disposing) {if(InvokeRequired)BeginInvoke(new Action(()=>status.Text=s));else status.Text=s;}},setupCancellation.Token,config.Language);}
   finally {if(!IsDisposed){start.Enabled=true;test.Enabled=true;settings.Enabled=true;status.Text=config.T("READY");}}
  }
  async Task Run(bool preview) {
   try {
    if(Environment.OSVersion.Version.Build<20348)throw new InvalidOperationException(config.T("Process audio capture requires Windows build 20348 or later. Use Windows 11."));
    if(!Bootstrap.Ready)await PrepareTools();
    Validate(config);string dest=url.Text.Trim();
    if(config.Target==null)throw new InvalidOperationException(config.T("Select an application before starting."));
    config.Target.Validate(config.Language);
    if(!preview&&!config.IsOutputUrlValid(dest)) {
     string expected=config.Protocol=="SRT"?"srt://":config.T("rtmp:// or rtmps://");
     throw new ArgumentException(config.T("Enter an OUTPUT URL starting with {0}.",expected));
    }
    busy=true;settings.Enabled=false;test.Enabled=false;application.Enabled=false;refresh.Enabled=false;url.ReadOnly=true;start.Text=config.T("STOP");status.Text=config.T(preview?"TESTING · 20 SECONDS":"SENDING");logs.Clear();
    session=new Session(Log);session.Start(config,dest,preview);await session.Completion;
   } catch(Exception e) {string message=I18n.T(config.Language,e.Message);Log(message);if(!IsDisposed&&!Disposing)MessageBox.Show(this,message,"windowsapp-va-sender",MessageBoxButtons.OK,MessageBoxIcon.Information);}
   finally {session?.Dispose();session=null;busy=false;if(!IsDisposed){settings.Enabled=true;test.Enabled=true;application.Enabled=true;refresh.Enabled=true;url.ReadOnly=false;start.Text=config.T("START");status.Text=config.T("READY");}}
  }
 }
 sealed class SettingsForm:Form {
  public Config Result;
  readonly ComboBox language=Combo("ENG","JPN"),protocol=Combo("RTMP","SRT"),size=Combo("960x540","1280x720","1920x1080"),fps=Combo("30","60"),adjust=Combo("CROP","COMPRESS","LETTER-BOX"),video=Combo("H264 (CPU)","H264 (NVEnc)","H264 (AMD)"),audio=Combo("AAC","Opus");
  readonly NumericUpDown audioSync=new NumericUpDown {Minimum=-2000,Maximum=2000,Increment=10,Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text};
  bool custom;string customText;Label note=Theme.Label("",9,true);Button customButton=Theme.Button("CUSTOM");
  static ComboBox Combo(params string[] values) {var c=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat};c.Items.AddRange(values);return c;}
  public SettingsForm(Config original) {
   Theme.Form(this,"SETTING / windowsapp-va-sender",new Size(700,730));FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   Result=original;custom=original.CustomEnabled;customText=original.Custom;
   var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=2,RowCount=13};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,55));root.Controls.Add(Theme.Label("SETTING",22),0,0);root.SetColumnSpan(root.GetControlFromPosition(0,0),2);
   var boxes=new[]{language,protocol,size,fps,adjust,video,audio};var labels=new[]{"LANGUAGE","PROTOCOL","OUTPUT-SIZE","FPS","ADJUSTMENT","VIDEO CODEC","AUDIO CODEC"};
   for(int i=0;i<boxes.Length;i++){root.RowStyles.Add(new RowStyle(SizeType.Absolute,49));root.Controls.Add(Theme.Label(labels[i],9,true),0,i+1);root.Controls.Add(boxes[i],1,i+1);}
   language.SelectedItem=original.Language;protocol.SelectedItem=original.Protocol;size.SelectedItem=original.Size;fps.SelectedItem=original.Fps.ToString();adjust.SelectedItem=original.Adjustment;video.SelectedItem=original.Video;audio.SelectedItem=original.Audio;
   audioSync.Value=original.AudioSyncMs;root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.Controls.Add(Theme.Label("AUDIO SYNC (ms)",9,true),0,8);root.Controls.Add(audioSync,1,8);
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));var syncHint=Theme.Label("Negative: advance audio · Positive: delay audio",9,true);root.Controls.Add(syncHint,0,9);root.SetColumnSpan(syncHint,2);
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.Controls.Add(note,0,10);root.SetColumnSpan(note,2);
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.Controls.Add(customButton,0,11);var customHint=Theme.Label("Edit video / audio output options",9,true);root.Controls.Add(customHint,1,11);
   var bottom=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=Theme.Button("SAVE",true);var cancel=Theme.Button("CANCEL");bottom.Controls.AddRange(new Control[]{save,cancel});root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.Controls.Add(bottom,0,12);root.SetColumnSpan(bottom,2);Controls.Add(root);
   protocol.SelectedIndexChanged+=(s,e)=>UpdateAudio();size.SelectedIndexChanged+=(s,e)=>UpdateNote();fps.SelectedIndexChanged+=(s,e)=>UpdateNote();
   language.SelectedIndexChanged+=(s,e)=>{I18n.Apply(this,(string)language.SelectedItem);UpdateNote();};
   customButton.Click+=(s,e)=>{using(var d=new CustomForm(custom,customText,Args.Join(Current(false).Encoding()),(string)language.SelectedItem))if(d.ShowDialog(this)==DialogResult.OK){custom=d.EnabledCustom;customText=d.Value;UpdateNote();}};
   save.Click+=(s,e)=>{try {var c=Current();c.Encoding();Result=c;DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"CUSTOM");}};
   cancel.Click+=(s,e)=>DialogResult=DialogResult.Cancel;AcceptButton=save;CancelButton=cancel;I18n.Apply(this,original.Language);UpdateAudio();
  }
  Config Current(bool includeCustom=true) {return new Config{AudioSyncMs=(int)audioSync.Value,Language=(string)language.SelectedItem,Protocol=(string)protocol.SelectedItem,Size=(string)size.SelectedItem,Fps=int.Parse((string)fps.SelectedItem),Adjustment=(string)adjust.SelectedItem,Video=(string)video.SelectedItem,Audio=(string)audio.SelectedItem,CustomEnabled=includeCustom&&custom,Custom=customText};}
  void UpdateAudio() {string p=(string)protocol.SelectedItem;audio.Enabled=p=="SRT";if(p=="RTMP")audio.SelectedItem="AAC";UpdateNote();}
  void UpdateNote() {if(size.SelectedItem==null||fps.SelectedItem==null)return;var c=Current();note.Text=custom?c.T("CUSTOM enabled · overrides encoder, bitrate and audio"):c.T("CBR {0} Mbps · GOP {1} (1 sec) · B frames 0 · Audio 128 kbps",(c.Bitrate/1000.0).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture),c.Fps);customButton.Text=c.T(custom?"CUSTOM · ON":"CUSTOM");}
 }
 sealed class CustomForm:Form {
  public bool EnabledCustom;public string Value;
  public CustomForm(bool enabled,string value,string example,string language="ENG") {
   Theme.Form(this,"CUSTOM / windowsapp-va-sender",new Size(700,430));StartPosition=FormStartPosition.CenterParent;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,20));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
   var on=new CheckBox {Text="Use CUSTOM options",Checked=enabled,AutoSize=true,ForeColor=Theme.Text};root.Controls.Add(on,0,0);
   var help=Theme.Label("Advanced: replaces video / audio encoding options.\nThe app manages the URL, inputs, filters and output format.\nExample: -c:v h264_nvenc -preset p3 -b:v 4500k -g 30 -bf 0 -c:a libopus -b:a 128k",9,true);root.Controls.Add(help,0,1);
   var editor=Theme.Box(true);editor.Text=string.IsNullOrWhiteSpace(value)?example:value;root.Controls.Add(editor,0,2);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=Theme.Button("APPLY",true);var cancel=Theme.Button("CANCEL");buttons.Controls.AddRange(new Control[]{save,cancel});root.Controls.Add(buttons,0,4);Controls.Add(root);
   save.Click+=(s,e)=>{try{new Config {Language=language,CustomEnabled=on.Checked,Custom=editor.Text}.Encoding();EnabledCustom=on.Checked;Value=editor.Text;DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,I18n.T(language,ex.Message));}};cancel.Click+=(s,e)=>DialogResult=DialogResult.Cancel;I18n.Apply(this,language);
  }
 }
 static class Entry {
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [STAThread] static int Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SetProcessDPIAware();
   try {
    if(args.Length>0&&args[0]=="--self-test")return Checks.Run(args.Length>1?args[1]:"checks.txt");
    if(args.Length>0&&args[0]=="--fixture"){TargetChecks.Fixture(args[1],int.Parse(args[2]));return 0;}
    if(args.Length>0&&args[0]=="--target-checks")return TargetChecks.Run(args[1]);
    if(args.Length>0&&args[0]=="--ui-checks")return UiChecks.Run(args[1]);
    if(args.Length>0&&args[0]=="--encode-checks")return Checks.Encode(args[1]);
    if(args.Length>0&&args[0]=="--stop-test") {
     using(var run=new Session(s=>{})) {
      run.Start(new Config(),"",true,true);int[] ids=run.OwnedProcessIds;System.Threading.Thread.Sleep(1500);run.Stop();
      if(!run.Completion.Wait(5000))throw new Exception("STOP timeout");
      foreach(int id in ids){try{using(var p=Process.GetProcessById(id))if(!p.HasExited)throw new Exception("Child remains: "+id);}catch(ArgumentException){}}
      File.WriteAllText(args[1],"PASS: STOP completed; all three owned child processes exited.");return 0;
     }
    }
    if(args.Length>0&&args[0]=="--pipeline-test") {
     var messages=new List<string>();var clock=Stopwatch.StartNew();
     using(var run=new Session(s=>{lock(messages)messages.Add(s);})) {
      var cfg=new Config{Video=args[1]=="CPU"?"H264 (CPU)":"H264 (NVEnc)",Protocol=args[1].StartsWith("RTMP")?"RTMP":"SRT",Audio=args[1]=="SRT-AAC"?"AAC":"Opus"};
      if(args[1]=="RTMP-CUSTOM"){cfg.CustomEnabled=true;cfg.Custom=Args.Join(new Config().Encoding());}
      run.Start(cfg,"",true,true);
      if(!run.Completion.Wait(35000)){run.Stop();throw new Exception("Pipeline timed out");}
      bool ok=run.Successful&&clock.Elapsed.TotalSeconds>=19;
      lock(messages){messages.Add((ok?"PASS":"FAIL")+": simulated 20s preview, elapsed "+clock.Elapsed.TotalSeconds.ToString("F2")+" sec");File.WriteAllLines(args[2],messages);}return ok?0:1;
     }
    }
    if(args.Length>0&&args[0]=="--render") {using(var main=new MainForm()){Prepare(main);main.RefreshInfo();using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[1]);}}using(var set=new SettingsForm(new Config())){Prepare(set);using(var image=new Bitmap(set.Width,set.Height)){set.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[2]);}}return 0;}
    Application.Run(new MainForm());return 0;
   }catch(Exception e){MessageBox.Show(e.ToString(),"windowsapp-va-sender");return 1;}
  }
  static void Prepare(Form f) {f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-30000,-30000);f.Show();Application.DoEvents();f.PerformLayout();}
 }
}
