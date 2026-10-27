using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsAppSender {
 static class UiChecks {
  static void Require(bool value,string message){if(!value)throw new Exception(message);}
  static IEnumerable<Control> All(Control parent) {yield return parent;foreach(Control child in parent.Controls)foreach(Control nested in All(child))yield return nested;}
  static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-30000,-30000);f.Show();Application.DoEvents();}
  static void Image(Form f,string file){f.PerformLayout();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(file);}}
  public static int Run(string directory) {
   Directory.CreateDirectory(directory);var result=new List<string>();
   try {
    var json=new JavaScriptSerializer();
    Require(new Config().Language=="ENG","default language");
    Require(json.Deserialize<Config>("{\"Protocol\":\"SRT\"}").Language=="ENG","old settings default English");
    var invalid=new Config{Language="other"};invalid.MigrateLegacySettings();Require(invalid.Language=="ENG","invalid language fallback");
    Config saved;
    using(var form=new SettingsForm(new Config{Size="1920x1080",Fps=60})) {
     Show(form);
     var selector=All(form).OfType<ComboBox>().Single(c=>c.Items.Contains("ENG")&&c.Items.Contains("JPN"));
     Require((string)selector.SelectedItem=="ENG","ENG selected");
     Require(All(form).OfType<Label>().Any(c=>c.Text=="LANGUAGE"),"LANGUAGE label");
     Image(form,Path.Combine(directory,"settings-eng.png"));
     selector.SelectedItem="JPN";Application.DoEvents();
     All(form).OfType<NumericUpDown>().Single().Value=-200;
     Require(All(form).OfType<Label>().Any(c=>c.Text=="出力サイズ"),"JPN labels immediately update");
     Image(form,Path.Combine(directory,"settings-jpn.png"));
     selector.SelectedItem="ENG";Application.DoEvents();Require(All(form).OfType<Label>().Any(c=>c.Text=="OUTPUT-SIZE"),"JPN to ENG roundtrip");
     selector.SelectedItem="JPN";((Button)form.AcceptButton).PerformClick();saved=form.Result;
     Require(form.DialogResult==DialogResult.OK&&saved.Language=="JPN"&&saved.Size=="1920x1080"&&saved.Fps==60&&saved.AudioSyncMs==-200,"SAVE persists language, sync and encoder settings");
    }
    string path=Path.Combine(directory,"saved-settings.json");File.WriteAllText(path,json.Serialize(saved));var restored=json.Deserialize<Config>(File.ReadAllText(path));
    Require(restored.Language=="JPN"&&restored.AudioSyncMs==-200,"language and sync persistence");
    using(var form=new SettingsForm(restored)) {
     Show(form);All(form).OfType<ComboBox>().Single(c=>c.Items.Contains("ENG")).SelectedItem="ENG";
     ((Button)form.CancelButton).PerformClick();Require(form.Result.Language=="JPN","CANCEL does not commit language");
    }
    foreach(string language in new[]{"ENG","JPN"}) {
     var config=new Config{Language=language};
     using(var main=new MainForm(config)) {
      Show(main);main.RefreshInfo();Require(main.Text.EndsWith(" "+Theme.Version),"title version");Require(All(main).OfType<Label>().Any(c=>c.Text==Theme.Version),"footer version");Require(All(main).OfType<Button>().Any(b=>b.Text==config.T("SETTING")),"localized main button");
      Image(main,Path.Combine(directory,"main-"+language.ToLowerInvariant()+".png"));
     }
     using(var custom=new CustomForm(false,"",Args.Join(config.Encoding()),language)) {Show(custom);Require(All(custom).OfType<CheckBox>().Single().Text==config.T("Use CUSTOM options"),"localized CUSTOM");Image(custom,Path.Combine(directory,"custom-"+language.ToLowerInvariant()+".png"));}
     try{new Config{Language=language,CustomEnabled=true,Custom="-i bad"}.Encoding();throw new Exception("validation expected");}catch(ArgumentException e){Require(e.Message==I18n.T(language,"CUSTOM accepts encoding options only. The app manages inputs, URL, filters, output format and duration."),"localized validation error");}
    }
    var en=new Config{Target=new ApplicationTarget(1234,5678,9,"game.exe","Game")};var jp=new Config{Language="JPN",Target=en.Target};Require(en.Command("srt://example:1935").SequenceEqual(jp.Command("srt://example:1935")),"language must not change sender command");
    result.Add("PASS: ENG default; old/missing/invalid language migration; ENG/JPN selection and immediate settings refresh.");
    result.Add("PASS: SAVE, JSON persistence, CANCEL, and existing size/FPS retained.");
    result.Add("PASS: ENG/JPN main, SETTING and CUSTOM labels and validation messages; six UI screenshots rendered.");
    result.Add("PASS: Sender command is identical in ENG and JPN.");
    File.WriteAllLines(Path.Combine(directory,"UI-RESULTS.txt"),result);return 0;
   }catch(Exception e){result.Add("FAIL: "+e);File.WriteAllLines(Path.Combine(directory,"UI-RESULTS.txt"),result);return 1;}
  }
 }
}
