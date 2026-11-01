using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Dark1C {
    internal static class Payload {
        static readonly object gate=new object();
        static string runtime;
        static byte[] Resource(string name) {
            using(var stream=typeof(Payload).Assembly.GetManifestResourceStream(name)) {
                if(stream==null)return null;
                using(var memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}
            }
        }
        static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static void Extract(string path,byte[] bytes) {
            if(File.Exists(path)) {
                if(Hash(File.ReadAllBytes(path))!=Hash(bytes))throw new IOException("Файл Dark1C повреждён: "+path);
                return;
            }
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllBytes(temporary,bytes);
                try{File.Move(temporary,path);}
                catch(IOException){if(!File.Exists(path)||Hash(File.ReadAllBytes(path))!=Hash(bytes))throw;}
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static string RuntimeDirectory {
            get {lock(gate){
                if(runtime!=null)return runtime;
                byte[] dll=Resource("Dark1C.Payload.dll"),image=Resource("Dark1C.Payload.png");
                if(dll==null||image==null)
                    return Path.GetDirectoryName(typeof(Payload).Assembly.Location);
                string key=Hash(dll)+"-"+Hash(image).Substring(0,16);
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dark1C","runtime",key);
                Directory.CreateDirectory(folder);
                Extract(Path.Combine(folder,"Dark1C.Native.dll"),dll);
                Extract(Path.Combine(folder,"background.png"),image);
                runtime=folder;return runtime;
            }}
        }
        internal static string Background {get{return Path.Combine(RuntimeDirectory,"background.png");}}
    }
}
