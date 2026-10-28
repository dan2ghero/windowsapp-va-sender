using System;
using System.Runtime.InteropServices;
namespace WindowsAppSender {
 static class Compatibility {
  public static bool CanAttempt(int build){return build>=19041;}
  public static void EnsureSupported(){if(!CanAttempt(Environment.OSVersion.Version.Build))throw new PlatformNotSupportedException("This app requires Windows 10 version 2004 (build 19041) or later. Process audio availability is checked at runtime.");}
  public static string Description {get{return "Windows build "+Environment.OSVersion.Version.Build+"; CLR "+Environment.Version+"; x64="+Environment.Is64BitProcess+"; "+typeof(Compatibility).Assembly.GetName().Name+" "+typeof(Compatibility).Assembly.GetName().Version.ToString(3);}}
 }
}
