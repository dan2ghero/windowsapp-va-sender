using System;
using System.IO;
using System.Security.Cryptography;
namespace WindowsAppSender {
 static class NativeBuild {
  public const string Sha256="29762173db97945518de2244b4657c2f228a82e9313b5295f53c0a6f9c142e53";
  public static void Validate(string path) {
   using(var hash=SHA256.Create())using(var stream=File.OpenRead(path)) {
    string actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    if(actual!=Sha256)throw new InvalidDataException("AUTO SYNC: FFmpeg does not match this app. Extract the complete ZIP into a new folder.");
   }
  }
 }
}
