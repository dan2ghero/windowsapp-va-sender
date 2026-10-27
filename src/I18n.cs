using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace WindowsAppSender {
 static class I18n {
  static readonly Dictionary<string,string> Japanese=new Dictionary<string,string> {
   {"APPLICATION","アプリケーション"},{"REFRESH","更新"},
   {"AUDIO SYNC (ms)","音声同期補正 (ms)"},
   {"Negative: advance audio · Positive: delay audio","負の値: 音声を先行 / 正の値: 音声を遅延"},
   {"Audio sync must be between -2000 and +2000 ms.","音声同期補正は-2000～+2000msで設定してください。"},
   {"Select an application window. Its process audio is captured automatically.","アプリケーションのウィンドウを選択すると、そのプロセスの音声も取得します。"},
   {"Windows 11 · selected application / process tree audio","Windows 11 · 選択したアプリ / プロセスツリー音声"},
   {"Select an application before starting.","開始する前にアプリケーションを選択してください。"},
   {"Select an application to preview the FFmpeg parameters.","アプリケーションを選択するとFFmpegのパラメータを表示します。"},
   {"The selected window is no longer available. Refresh the application list.","選択したウィンドウは利用できません。一覧を更新してください。"},
   {"The selected application changed. Refresh and select it again.","選択したアプリの起動状態が変わりました。更新して選択し直してください。"},
   {"Restore the selected application window before starting.","開始する前に選択したアプリケーションの最小化を解除してください。"},
   {"Video window: {0}; audio process: {1} (PID {2})","映像ウィンドウ: {0} / 音声プロセス: {1} (PID {2})"},
   {"APPLICATION VIDEO + PROCESS AUDIO","アプリケーション映像とプロセス音声"},
   {"OUTPUT","送信先"},{"INFO","設定情報"},{"SETTING","設定"},
   {"START","開始"},{"STOP","停止"},{"TEST · 20s","テスト · 20秒"},
   {"READY","待機中"},{"STOPPING…","停止中…"},{"SENDING","送信中"},
   {"TESTING · 20 SECONDS","テスト中 · 20秒"},{"SESSION LOG","動作ログ"},
   {"SETTING / windowsapp-va-sender","設定 / windowsapp-va-sender"},
   {"PROTOCOL","転送方式"},{"OUTPUT-SIZE","出力サイズ"},{"FPS","フレームレート"},
   {"ADJUSTMENT","映像補正"},{"VIDEO CODEC","映像コーデック"},{"AUDIO CODEC","音声コーデック"},
   {"LANGUAGE","言語 / LANGUAGE"},{"SAVE","保存"},{"CANCEL","キャンセル"},{"APPLY","適用"},
   {"CUSTOM","カスタム"},{"CUSTOM · ON","カスタム · 有効"},
   {"Edit video / audio output options","映像・音声の出力オプションを直接編集"},
   {"CUSTOM enabled · overrides encoder, bitrate and audio","CUSTOM有効 · エンコーダー・ビットレート・音声設定を上書き"},
   {"CBR {0} Mbps · GOP {1} (1 sec) · B frames 0 · Audio 128 kbps","CBR {0} Mbps · GOP {1} (1秒) · Bフレーム0 · 音声128 kbps"},
   {"CUSTOM / windowsapp-va-sender","カスタム / windowsapp-va-sender"},
   {"Use CUSTOM options","CUSTOMを使用する"},
   {"Advanced: replaces video / audio encoding options.\nThe app manages the URL, inputs, filters and output format.\nExample: -c:v h264_nvenc -preset p3 -b:v 4500k -g 30 -bf 0 -c:a libopus -b:a 128k","上級者向け。映像・音声の出力オプションを置き換えます。\nURL、入力、フィルター、転送形式はアプリが管理します。\n例: -c:v h264_nvenc -preset p3 -b:v 4500k -g 30 -bf 0 -c:a libopus -b:a 128k"},
   {"Invalid settings.","設定値が不正です。"},
   {"Could not save settings: {0}","設定を保存できません: {0}"},
   {"Retry initial setup with TEST / START.","初回準備はTEST / STARTで再試行できます。"},
   {"PREPARING FFMPEG…","FFmpegを準備中…"},
   {"Process audio capture requires Windows build 20348 or later. Use Windows 11.","この音声取得方式はOSビルド20348以降が必要です。Windows 11を使用してください。"},
   {"Enter an OUTPUT URL starting with {0}.","OUTPUTに {0} で始まる送信先URLを入力してください。"},
   {"rtmp:// or rtmps://","rtmp:// または rtmps://"},
   {"This version supports SRT / RTMP. WHIP is disabled.","この版はSRT / RTMPに対応しています。WHIPは無効です。"},
   {"CUSTOM accepts encoding options only. The app manages inputs, URL, filters, output format and duration.","CUSTOMは映像・音声の出力オプション専用です。入力、URL、フィルター、転送形式、時間指定はアプリが管理します。"},
   {"Enter CUSTOM options as -option value pairs. Do not specify a URL or output file.","CUSTOMは -オプション 値 の組で入力してください。URL・出力ファイルは指定できません。"},
   {"WHIP is disabled. Remove WHIP-only options from CUSTOM.","WHIPは無効です。CUSTOMのWHIP専用オプションを削除してください。"},
   {"Unable to parse CUSTOM options.","CUSTOMを読み取れません。"},
   {"tools/{0} was not found.","tools/{0} が見つかりません。"},
   {"Pipe closed: {0}","パイプ終了: {0}"},
   {"FFmpeg exited with an error ({0}). Check the log.","FFmpegがエラー終了しました ({0})。ログを確認してください。"},
   {"20-second TEST completed.","20秒TESTが完了しました。"},{"Sending finished.","送信が終了しました。"},
   {"First-run setup: downloading official FFmpeg (about 211 MB)…","初回準備: 公式FFmpegを取得しています (約211 MB)…"},
   {"First-run setup: downloading FFmpeg {0}%","初回準備: FFmpegダウンロード {0}%"},
   {"First-run setup: verifying SHA-256 and extracting…","初回準備: SHA-256照合・展開中…"},
   {"The official FFmpeg ZIP SHA-256 does not match. Installation was cancelled.","公式FFmpeg ZIPのSHA-256が一致しません。配置を中止しました。"},
   {"FFmpeg is ready.","FFmpegの準備が完了しました。"}
  };
  sealed class SourceText {public string Value;public SourceText(string value){Value=value;}}
  public static string T(string language,string english,params object[] args) {
   string value;string format=language=="JPN"&&Japanese.TryGetValue(english,out value)?value:english;
   return args.Length==0?format:string.Format(CultureInfo.InvariantCulture,format,args);
  }
  public static void Apply(Control root,string language) {
   if(root is Label||root is Button||root is CheckBox||root is Form) {
    var source=root.Tag as SourceText;
    if(source==null&&Japanese.ContainsKey(root.Text)){source=new SourceText(root.Text);root.Tag=source;}
    if(source!=null)root.Text=T(language,source.Value);
   }
   foreach(Control child in root.Controls)Apply(child,language);
   if(root is Form&&!root.Text.EndsWith(" "+Theme.Version,StringComparison.Ordinal))root.Text+=" "+Theme.Version;
  }
 }
}
