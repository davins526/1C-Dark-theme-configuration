using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Dark1C {
    internal static class Payload {
        static readonly object gate=new object();
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
                byte[] dll=Resource("Dark1C.Payload.dll"),image=Resource("Dark1C.Payload.png");
                if(dll==null||image==null)
                    throw new InvalidDataException("В EXE отсутствуют встроенные DLL/PNG. Загрузка непроверенной DLL из соседнего файла запрещена.");
                string key=Hash(dll)+"-"+Hash(image).Substring(0,16);
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dark1C","runtime",key);
                Directory.CreateDirectory(folder);
                Extract(Path.Combine(folder,"Dark1C.Native.dll"),dll);
                Extract(Path.Combine(folder,"background.png"),image);
                return folder;
            }}
        }
        // Keep a verified read handle open across LoadLibraryW. FileShare.Read
        // rejects writers/deletion of this file while the loader is using it.
        // This is defense in depth, not a trust boundary for an elevated app.
        internal static FileStream OpenVerifiedDll(string path) {
            byte[] expected=Resource("Dark1C.Payload.dll");
            if(expected==null)throw new InvalidDataException("Нет встроенного эталона DLL.");
            var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            try {
                string actual;using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
                if(actual!=Hash(expected))throw new InvalidDataException("DLL изменилась перед загрузкой. Подключение отменено.");
                file.Position=0;return file;
            }catch{file.Dispose();throw;}
        }
        internal static string Background {get{return Path.Combine(RuntimeDirectory,"background.png");}}
    }
}
